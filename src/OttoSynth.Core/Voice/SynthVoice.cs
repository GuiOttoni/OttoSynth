using System;
using System.Numerics;
using OttoSynth.Core.DSP.Envelopes;
using OttoSynth.Core.DSP.Filters;
using OttoSynth.Core.DSP.Modulation;
using OttoSynth.Core.DSP.Oscillators;
using OttoSynth.Core.DSP.Utils;

namespace OttoSynth.Core.Voice;

/// <summary>
/// Full synthesizer voice with 3 oscillators, noise, 2 filters, 3 envelopes,
/// 3 LFOs, and modulation matrix.
/// Designed for zero-allocation audio processing.
/// </summary>
public sealed class SynthVoice
{
    /// <summary>Voice states for the voice manager.</summary>
    public enum VoiceState { Idle, Active, Releasing, Stealing }

    /// <summary>Routing mode between two oscillators.</summary>
    public enum OscRouting
    {
        Mix,      // Independent — no cross-modulation (default)
        FM,       // Frequency modulation: modulator signal shifts carrier pitch (exponential FM)
        RingMod,  // Ring modulation: carrier output is multiplied by modulator output
    }

    /// <summary>Filter routing options.</summary>
    public enum FilterRouting { Serial, Parallel, Split }

    /// <summary>Portamento/glide mode.</summary>
    public enum GlideMode { Off, Always, Legato }

    // ─── Routing matrix entry ────────────────────────────────────
    private struct OscRouteEntry { public OscRouting Mode; public double Depth; }

    // ─── Components ─────────────────────────────────────────────

    // 3 oscillators (each with unison capability)
    private readonly WavetableOscillator _osc1;
    private readonly WavetableOscillator _osc2;
    private readonly WavetableOscillator _osc3;
    private readonly NoiseOscillator _noise;

    // Unison engines — active when voice count > 1
    private readonly UnisonEngine _unison1;
    private readonly UnisonEngine _unison2;
    private readonly UnisonEngine _unison3;

    // Active oscillator sources — element [i] points to _oscN or _unisonN for OSC i (0-indexed).
    // Switching happens in SetUnison(); the audio loop reads only from this array.
    private readonly IOscillatorSource[] _oscSources;

    // 2 stereo filter pairs (L + R per filter)
    private readonly StateVariableFilter _filter1;
    private readonly StateVariableFilter _filter1R;
    private readonly StateVariableFilter _filter2;
    private readonly StateVariableFilter _filter2R;

    // 3 envelopes: ENV1=Amp, ENV2=Filter, ENV3=Free
    private readonly AdsrEnvelope _envAmp;
    private readonly AdsrEnvelope _envFilter;
    private readonly AdsrEnvelope _envFree;

    // 3 LFOs
    private readonly LfoGenerator _lfo1;
    private readonly LfoGenerator _lfo2;
    private readonly LfoGenerator _lfo3;

    // Modulation matrix (per-voice instance)
    private readonly ModMatrix _modMatrix;

    // ─── Mixer levels/enable (0-indexed, mirror of public Osc1Level/Enabled etc.) ─
    private readonly double[] _oscLevels  = new double[3];
    private readonly bool[]   _oscEnabled = new bool[3];
    private double _noiseLevel;
    private bool   _noiseEnabled;

    // ─── Filter settings ────────────────────────────────────────
    private FilterRouting _filterRouting;
    private double _filterEnvAmount;

    // ─── Base parameter values (user-set; modulation is applied on top) ─────────
    private double _filter1BaseCutoff    = 20000.0;
    private double _filter2BaseCutoff    = 20000.0;
    private double _filter1BaseResonance = 0.0;
    private double _filter2BaseResonance = 0.0;
    private double _filter1BaseDrive     = 0.0;
    private double _filter2BaseDrive     = 0.0;

    public void SetFilter1Base(double cutoff, double resonance, double drive)
    {
        _filter1BaseCutoff    = Math.Clamp(cutoff,     20.0, 20000.0);
        _filter1BaseResonance = Math.Clamp(resonance,   0.0,     1.0);
        _filter1BaseDrive     = Math.Clamp(drive,       0.0,     1.0);
    }

    public void SetFilter2Base(double cutoff, double resonance, double drive)
    {
        _filter2BaseCutoff    = Math.Clamp(cutoff,     20.0, 20000.0);
        _filter2BaseResonance = Math.Clamp(resonance,   0.0,     1.0);
        _filter2BaseDrive     = Math.Clamp(drive,       0.0,     1.0);
    }

    // ─── N×N Oscillator routing matrix ──────────────────────────
    // _routingMatrix[mod0, car0]: how OSC (mod0+1) modulates OSC (car0+1). Diagonal unused.
    private readonly OscRouteEntry[,] _routingMatrix = new OscRouteEntry[3, 3];
    // Processing order computed by Kahn's topological sort; audio loop reads this.
    private readonly int[] _processingOrder = new int[] { 0, 1, 2 };

    // ─── Base wavetable positions (prevents modulation drift accumulation) ──────
    private readonly double[] _oscBasePositions = new double[3];

    // ─── Pre-allocated audio buffers ────────────────────────────
    private readonly double[] _osc1BufL, _osc1BufR;
    private readonly double[] _osc2BufL, _osc2BufR;
    private readonly double[] _osc3BufL, _osc3BufR;
    private readonly double[] _fmMonoBuf;
    private readonly double[] _noiseBufL, _noiseBufR;
    private readonly double[] _mixLeft, _mixRight;
    private readonly double[] _envAmpBuffer, _envFilterBuffer, _envFreeBuffer;
    private readonly double[] _lfo1Buffer, _lfo2Buffer, _lfo3Buffer;

    // Indexed aliases into the per-OSC stereo buffers — same arrays, no copy.
    private readonly double[][] _oscBufsL;
    private readonly double[][] _oscBufsR;

    // Pre-level/pan mono signal per OSC — written by Process(rawMono), used for pan-independent FM.
    private readonly double[][] _oscRawBufs;

    // ─── Portamento ─────────────────────────────────────────────
    private double _sampleRate = 44100.0;
    private double _glideTime  = 0.0;
    private GlideMode _glideMode = GlideMode.Off;
    private double _currentFrequency = 440.0;

    // ─── State ──────────────────────────────────────────────────
    private VoiceState _state;
    private int    _noteNumber;
    private int    _velocity;
    private double _velocityGain;
    private long   _noteOnTimestamp;
    private double _noteFrequency;
    private double _noteRandom;

    private static readonly Random _rng = new();

    // ─── Mod destination lookup tables ──────────────────────────
    private static readonly ModDestination[] OscLevelDest =
        { ModDestination.Osc1Level, ModDestination.Osc2Level, ModDestination.Osc3Level };
    private static readonly ModDestination[] OscPitchDest =
        { ModDestination.Osc1Pitch, ModDestination.Osc2Pitch, ModDestination.Osc3Pitch };
    private static readonly ModDestination[] OscWtPosDest =
        { ModDestination.Osc1WavetablePos, ModDestination.Osc2WavetablePos, ModDestination.Osc3WavetablePos };

    // ─── Public Properties ──────────────────────────────────────

    public VoiceState State          => _state;
    public int        NoteNumber     => _noteNumber;
    public int        Velocity       => _velocity;
    public long       NoteOnTimestamp=> _noteOnTimestamp;
    public bool       IsActive       => _state != VoiceState.Idle;
    public double     CurrentAmplitude => _envAmp.CurrentValue * _velocityGain;

    // Component accessors
    public WavetableOscillator Osc1     => _osc1;
    public WavetableOscillator Osc2     => _osc2;
    public WavetableOscillator Osc3     => _osc3;
    public NoiseOscillator     Noise    => _noise;
    public UnisonEngine        Unison1  => _unison1;
    public UnisonEngine        Unison2  => _unison2;
    public UnisonEngine        Unison3  => _unison3;

    // Active source accessors (point to single osc or unison engine)
    public IOscillatorSource Osc1Source => _oscSources[0];
    public IOscillatorSource Osc2Source => _oscSources[1];
    public IOscillatorSource Osc3Source => _oscSources[2];

    public StateVariableFilter Filter1    => _filter1;
    public StateVariableFilter Filter2    => _filter2;
    public AdsrEnvelope        EnvAmp     => _envAmp;
    public AdsrEnvelope        EnvFilter  => _envFilter;
    public AdsrEnvelope        EnvFree    => _envFree;
    public LfoGenerator        Lfo1       => _lfo1;
    public LfoGenerator        Lfo2       => _lfo2;
    public LfoGenerator        Lfo3       => _lfo3;
    public ModMatrix           ModMatrix  => _modMatrix;

    // Backward-compatible accessor
    public WavetableOscillator Oscillator  => _osc1;
    public AdsrEnvelope        AmpEnvelope => _envAmp;

    // Mixer
    public double Osc1Level { get => _oscLevels[0]; set => _oscLevels[0] = Math.Clamp(value, 0.0, 1.0); }
    public double Osc2Level { get => _oscLevels[1]; set => _oscLevels[1] = Math.Clamp(value, 0.0, 1.0); }
    public double Osc3Level { get => _oscLevels[2]; set => _oscLevels[2] = Math.Clamp(value, 0.0, 1.0); }
    public bool   Osc1Enabled { get => _oscEnabled[0]; set => _oscEnabled[0] = value; }
    public bool   Osc2Enabled { get => _oscEnabled[1]; set => _oscEnabled[1] = value; }
    public bool   Osc3Enabled { get => _oscEnabled[2]; set => _oscEnabled[2] = value; }
    public double NoiseLevel  { get => _noiseLevel;  set => _noiseLevel  = Math.Clamp(value, 0.0, 1.0); }
    public bool   NoiseEnabled{ get => _noiseEnabled; set => _noiseEnabled = value; }

    // Filter
    public FilterRouting Routing          { get => _filterRouting;   set => _filterRouting   = value; }
    public double        FilterEnvAmount  { get => _filterEnvAmount; set => _filterEnvAmount = Math.Clamp(value, -1.0, 1.0); }

    // Portamento
    public double    GlideTime { get => _glideTime;  set => _glideTime  = Math.Clamp(value, 0.0, 2.0); }
    public GlideMode Glide     { get => _glideMode;  set => _glideMode  = value; }

    // ─── Routing matrix API ─────────────────────────────────────

    /// <summary>
    /// Sets the routing from <paramref name="modulator"/> to <paramref name="carrier"/> (both 1-indexed).
    /// Rejects the change if it would create a dependency cycle.
    /// </summary>
    public void SetOscillatorRouting(int modulator, int carrier, OscRouting mode, double depth)
    {
        int m = modulator - 1;
        int c = carrier - 1;
        if (m < 0 || m > 2 || c < 0 || c > 2 || m == c) return;

        var oldEntry = _routingMatrix[m, c];
        _routingMatrix[m, c].Mode  = mode;
        _routingMatrix[m, c].Depth = Math.Clamp(depth, 0.0, 1.0);

        if (!TryRecomputeProcessingOrder())
            _routingMatrix[m, c] = oldEntry; // revert — cycle detected
    }

    /// <summary>Returns the routing mode for a modulator→carrier pair (1-indexed).</summary>
    public OscRouting GetOscRouting(int modulator, int carrier)
        => _routingMatrix[modulator - 1, carrier - 1].Mode;

    /// <summary>Returns the FM depth for a modulator→carrier pair (1-indexed).</summary>
    public double GetOscFmDepth(int modulator, int carrier)
        => _routingMatrix[modulator - 1, carrier - 1].Depth;

    // ─── Constructor ────────────────────────────────────────────

    public SynthVoice(int maxBufferSize = 1024)
    {
        _osc1 = new WavetableOscillator();
        _osc2 = new WavetableOscillator();
        _osc3 = new WavetableOscillator();
        _noise = new NoiseOscillator();

        _unison1 = new UnisonEngine(maxBufferSize);
        _unison2 = new UnisonEngine(maxBufferSize);
        _unison3 = new UnisonEngine(maxBufferSize);
        _unison1.SetLevel(1.0);
        _unison2.SetLevel(1.0);
        _unison3.SetLevel(1.0);

        _filter1  = new StateVariableFilter();
        _filter1R = new StateVariableFilter();
        _filter2  = new StateVariableFilter();
        _filter2R = new StateVariableFilter();

        _envAmp    = new AdsrEnvelope();
        _envFilter = new AdsrEnvelope();
        _envFree   = new AdsrEnvelope();

        _lfo1 = new LfoGenerator();
        _lfo2 = new LfoGenerator();
        _lfo3 = new LfoGenerator();

        _modMatrix = new ModMatrix();

        // Sources start as the single oscillators; SetUnison() may switch to unison engines
        _oscSources = new IOscillatorSource[] { _osc1, _osc2, _osc3 };

        // Pre-allocate all audio buffers
        _osc1BufL = new double[maxBufferSize]; _osc1BufR = new double[maxBufferSize];
        _osc2BufL = new double[maxBufferSize]; _osc2BufR = new double[maxBufferSize];
        _osc3BufL = new double[maxBufferSize]; _osc3BufR = new double[maxBufferSize];
        _fmMonoBuf = new double[maxBufferSize];
        _noiseBufL = new double[maxBufferSize]; _noiseBufR = new double[maxBufferSize];
        _mixLeft   = new double[maxBufferSize]; _mixRight  = new double[maxBufferSize];
        _envAmpBuffer    = new double[maxBufferSize];
        _envFilterBuffer = new double[maxBufferSize];
        _envFreeBuffer   = new double[maxBufferSize];
        _lfo1Buffer = new double[maxBufferSize];
        _lfo2Buffer = new double[maxBufferSize];
        _lfo3Buffer = new double[maxBufferSize];

        // Indexed aliases — same arrays, no allocation
        _oscBufsL = new[] { _osc1BufL, _osc2BufL, _osc3BufL };
        _oscBufsR = new[] { _osc1BufR, _osc2BufR, _osc3BufR };
        _oscRawBufs = new[]
        {
            new double[maxBufferSize],
            new double[maxBufferSize],
            new double[maxBufferSize],
        };

        // Default mixer state
        _oscLevels[0]  = 0.8; _oscEnabled[0] = true;
        _oscLevels[1]  = 0.0; _oscEnabled[1] = false;
        _oscLevels[2]  = 0.0; _oscEnabled[2] = false;
        _noiseLevel    = 0.0; _noiseEnabled  = false;
        _filterRouting = FilterRouting.Serial;
        _filterEnvAmount = 0.0;

        _envFilter.AttackTime   = 0.001;
        _envFilter.DecayTime    = 0.5;
        _envFilter.SustainLevel = 0.3;
        _envFilter.ReleaseTime  = 0.3;

        _state       = VoiceState.Idle;
        _noteNumber  = -1;
        _velocity    = 0;
        _velocityGain = 0.0;
        _noteRandom  = 0.0;
    }

    // ─── Setup ──────────────────────────────────────────────────

    public void SetSampleRate(double sampleRate)
    {
        _sampleRate = sampleRate;
        _osc1.SetSampleRate(sampleRate); _unison1.SetSampleRate(sampleRate);
        _osc2.SetSampleRate(sampleRate); _unison2.SetSampleRate(sampleRate);
        _osc3.SetSampleRate(sampleRate); _unison3.SetSampleRate(sampleRate);
        _filter1.SetSampleRate(sampleRate);  _filter1R.SetSampleRate(sampleRate);
        _filter2.SetSampleRate(sampleRate);  _filter2R.SetSampleRate(sampleRate);
        _envAmp.SetSampleRate(sampleRate);   _envFilter.SetSampleRate(sampleRate);
        _envFree.SetSampleRate(sampleRate);
        _lfo1.SetSampleRate(sampleRate); _lfo2.SetSampleRate(sampleRate);
        _lfo3.SetSampleRate(sampleRate);
    }

    public void SetWavetable(int oscIndex, double[][] wavetable, bool hasMipmap)
    {
        switch (oscIndex)
        {
            case 1: _osc1.SetWavetable(wavetable, hasMipmap); _unison1.SetWavetable(wavetable, hasMipmap); break;
            case 2: _osc2.SetWavetable(wavetable, hasMipmap); _unison2.SetWavetable(wavetable, hasMipmap); break;
            case 3: _osc3.SetWavetable(wavetable, hasMipmap); _unison3.SetWavetable(wavetable, hasMipmap); break;
        }
    }

    public void SetWavetable(double[][] wavetable, bool hasMipmap) => SetWavetable(1, wavetable, hasMipmap);

    /// <summary>Configures unison for oscillator 1-3. VoiceCount=1 reverts to single oscillator.</summary>
    public void SetUnison(int oscIndex, int voiceCount, double detuneCents, double spread)
    {
        voiceCount = Math.Clamp(voiceCount, 1, UnisonEngine.MaxUnisonVoices);
        int idx = oscIndex - 1;
        if (idx < 0 || idx > 2) return;

        var (rawOsc, unison) = oscIndex switch
        {
            1 => (_osc1, _unison1),
            2 => (_osc2, _unison2),
            _ => (_osc3, _unison3)
        };

        unison.VoiceCount   = voiceCount;
        unison.DetuneSpread = detuneCents;
        unison.StereoSpread = spread;

        if (voiceCount > 1)
        {
            SyncUnisonParams(rawOsc, unison);
            unison.SetFrequency(_currentFrequency);
            unison.SetLevel(1.0);
            _oscSources[idx] = unison;
        }
        else
        {
            _oscSources[idx] = rawOsc;
        }
    }

    private static void SyncUnisonParams(WavetableOscillator osc, UnisonEngine unison)
    {
        unison.Warp             = osc.Warp;
        unison.WarpAmount       = osc.WarpAmount;
        unison.CoarseTune       = osc.CoarseTune;
        unison.FineTune         = osc.FineTune;
        unison.WavetablePosition = osc.WavetablePosition;
    }

    /// <summary>
    /// Sets oscillator tuning/position/warp/pan on the active source.
    /// Also caches the wavetable position as the modulation base to prevent drift.
    /// </summary>
    public void SetOscillatorTuning(int oscIdx, int coarseTune, double fineTune,
        double wavetablePosition, double warpAmount, double pan)
    {
        int i = oscIdx - 1;
        if (i < 0 || i > 2) return;
        var src = _oscSources[i];
        src.CoarseTune        = coarseTune;
        src.FineTune          = fineTune;
        src.WavetablePosition = wavetablePosition;
        src.WarpAmount        = warpAmount;
        src.Pan               = pan;

        // Keep the raw WavetableOscillator in sync for SyncUnisonParams
        var rawOsc = i == 0 ? _osc1 : i == 1 ? _osc2 : _osc3;
        if (!ReferenceEquals(src, rawOsc))
        {
            rawOsc.CoarseTune        = coarseTune;
            rawOsc.FineTune          = fineTune;
            rawOsc.WavetablePosition = wavetablePosition;
            rawOsc.WarpAmount        = warpAmount;
            rawOsc.Pan               = pan;
        }

        _oscBasePositions[i] = wavetablePosition;
    }

    /// <summary>Sets the warp mode on the active oscillator source.</summary>
    public void SetOscillatorWarpMode(int oscIdx, WavetableOscillator.WaveWarp warp)
    {
        int i = oscIdx - 1;
        if (i < 0 || i > 2) return;
        _oscSources[i].Warp = warp;
        (i == 0 ? _osc1 : i == 1 ? _osc2 : _osc3).Warp = warp;
    }

    // ─── Note events ────────────────────────────────────────────

    public void NoteOn(int noteNumber, int velocity, long timestamp)
    {
        bool wasActive = _state != VoiceState.Idle;
        _noteNumber    = noteNumber;
        _velocity      = velocity;
        _velocityGain  = velocity / 127.0;
        _noteOnTimestamp = timestamp;
        _noteFrequency = MathUtils.MidiNoteToFrequency(noteNumber);
        _noteRandom    = _rng.NextDouble();

        bool shouldGlide = _glideMode == GlideMode.Always ||
                           (_glideMode == GlideMode.Legato && wasActive);
        if (!shouldGlide || _glideTime < 0.001)
            _currentFrequency = _noteFrequency;

        _osc1.SetFrequency(_currentFrequency); _unison1.SetFrequency(_currentFrequency);
        _osc2.SetFrequency(_currentFrequency); _unison2.SetFrequency(_currentFrequency);
        _osc3.SetFrequency(_currentFrequency); _unison3.SetFrequency(_currentFrequency);

        _osc1.ResetPhase(); _osc2.ResetPhase(); _osc3.ResetPhase();
        _unison1.NoteOn(); _unison2.NoteOn(); _unison3.NoteOn();

        _filter1.SetNoteFrequency(_noteFrequency); _filter1R.SetNoteFrequency(_noteFrequency);
        _filter2.SetNoteFrequency(_noteFrequency); _filter2R.SetNoteFrequency(_noteFrequency);
        _filter1.ResetState(); _filter1R.ResetState();
        _filter2.ResetState(); _filter2R.ResetState();

        _envAmp.NoteOn(); _envFilter.NoteOn(); _envFree.NoteOn();
        _lfo1.NoteOn(); _lfo2.NoteOn(); _lfo3.NoteOn();

        _state = VoiceState.Active;
    }

    public void NoteOff()
    {
        if (_state == VoiceState.Idle) return;
        _envAmp.NoteOff(); _envFilter.NoteOff(); _envFree.NoteOff();
        _state = VoiceState.Releasing;
    }

    public void ForceSteal(double fadeTimeMs = 3.0)
    {
        _state = VoiceState.Stealing;
        _envAmp.ForceRelease(fadeTimeMs);
    }

    public void Reset()
    {
        _state       = VoiceState.Idle;
        _noteNumber  = -1;
        _velocity    = 0;
        _velocityGain = 0.0;
        _noteRandom  = 0.0;
        _envAmp.Reset(); _envFilter.Reset(); _envFree.Reset();
        _lfo1.Reset();   _lfo2.Reset();      _lfo3.Reset();
    }

    // ─── Audio processing ────────────────────────────────────────

    /// <summary>
    /// Full signal chain: Envelopes/LFOs → ModMatrix → Oscillators (N×N routing) → Filter → Amp → Out.
    /// </summary>
    public void Process(double[] outputLeft, double[] outputRight, int sampleCount)
    {
        if (_state == VoiceState.Idle) return;

        // ── 1. Envelopes ─────────────────────────────────────────
        bool ampActive = _envAmp.Process(_envAmpBuffer, sampleCount);
        if (!ampActive) { _state = VoiceState.Idle; _noteNumber = -1; return; }

        _envFilter.Process(_envFilterBuffer, sampleCount);
        _envFree.Process(_envFreeBuffer,     sampleCount);

        // ── 2. LFOs ──────────────────────────────────────────────
        _lfo1.Process(_lfo1Buffer, sampleCount);
        _lfo2.Process(_lfo2Buffer, sampleCount);
        _lfo3.Process(_lfo3Buffer, sampleCount);

        // ── 3. Mod matrix ─────────────────────────────────────────
        _modMatrix.SetVoiceSources(
            AverageBuffer(_envAmpBuffer,    sampleCount),
            AverageBuffer(_envFilterBuffer, sampleCount),
            AverageBuffer(_envFreeBuffer,   sampleCount),
            AverageBuffer(_lfo1Buffer,      sampleCount),
            AverageBuffer(_lfo2Buffer,      sampleCount),
            AverageBuffer(_lfo3Buffer,      sampleCount),
            _velocityGain, _noteNumber, _noteRandom);
        _modMatrix.Process();

        // ── 4. Portamento glide ───────────────────────────────────
        if (_glideMode != GlideMode.Off && _glideTime > 0.001 &&
            Math.Abs(_currentFrequency - _noteFrequency) > 0.01)
        {
            double coeff  = 1.0 - Math.Exp(-(double)sampleCount / (_sampleRate * _glideTime));
            double logCurr = Math.Log(_currentFrequency);
            double logTgt  = Math.Log(_noteFrequency);
            _currentFrequency = Math.Exp(logCurr + (logTgt - logCurr) * coeff);
        }
        else
        {
            _currentFrequency = _noteFrequency;
        }

        // ── 5. Apply mod matrix to parameters ────────────────────
        ApplyModulation();

        // ── 6. Generate oscillators in topological order ──────────
        // Each OSC writes into its dedicated buffer at its own internal level+pan.
        // Mix level (_oscLevels[]) is applied separately during accumulation, so
        // the raw buffers can be used as clean modulation sources for FM/RM.

        for (int k = 0; k < 3; k++)
        {
            Array.Clear(_oscBufsL[k],   0, sampleCount);
            Array.Clear(_oscBufsR[k],   0, sampleCount);
            Array.Clear(_oscRawBufs[k], 0, sampleCount);
        }

        for (int step = 0; step < 3; step++)
        {
            int c = _processingOrder[step];

            bool inMix      = _oscEnabled[c] && _oscLevels[c] > 0.001;
            bool needsAudio = inMix || IsUsedAsModulator(c);
            if (!needsAudio) continue;

            // Accumulate FM from all FM modulators using their pan-independent raw mono signal.
            Array.Clear(_fmMonoBuf, 0, sampleCount);
            bool hasFm = false;
            for (int m = 0; m < 3; m++)
            {
                if (m == c) continue;
                var entry = _routingMatrix[m, c];
                if (entry.Mode == OscRouting.FM && entry.Depth > 0.001)
                {
                    double d    = entry.Depth;
                    var rawBuf  = _oscRawBufs[m];
                    var vd      = new Vector<double>(d);
                    int vs      = Vector<double>.Count;
                    int i       = 0;
                    for (; i + vs <= sampleCount; i += vs)
                        (new Vector<double>(_fmMonoBuf, i) + new Vector<double>(rawBuf, i) * vd)
                            .CopyTo(_fmMonoBuf, i);
                    for (; i < sampleCount; i++)
                        _fmMonoBuf[i] += rawBuf[i] * d;
                    hasFm = true;
                }
            }

            // Generate this OSC's audio; capture raw mono for use as FM source.
            var rawOut = _oscRawBufs[c];
            if (hasFm)
                _oscSources[c].ProcessWithFM(_oscBufsL[c], _oscBufsR[c], rawOut, _fmMonoBuf, 1.0, sampleCount);
            else
                _oscSources[c].Process(_oscBufsL[c], _oscBufsR[c], rawOut, sampleCount);

            // Apply ring mod from all RM modulators of c
            for (int m = 0; m < 3; m++)
            {
                if (m == c || _routingMatrix[m, c].Mode != OscRouting.RingMod) continue;
                var cL = _oscBufsL[c]; var cR = _oscBufsR[c];
                var mL = _oscBufsL[m]; var mR = _oscBufsR[m];
                for (int i = 0; i < sampleCount; i++)
                {
                    cL[i] *= mL[i];
                    cR[i] *= mR[i];
                }
            }
        }

        // ── Noise ─────────────────────────────────────────────────
        double noiseGain = 0.0;
        Array.Clear(_noiseBufL, 0, sampleCount);
        Array.Clear(_noiseBufR, 0, sampleCount);
        if (_noiseEnabled && _noiseLevel > 0.001)
        {
            noiseGain = _modMatrix.ApplyMod(ModDestination.NoiseLevel, _noiseLevel);
            _noise.Process(_noiseBufL, _noiseBufR, sampleCount);
        }

        // ── Mix — apply per-OSC levels and accumulate ─────────────
        // Pre-compute mix gains (includes mod-matrix modulation)
        double mg0 = _oscEnabled[0] && _oscLevels[0] > 0.001
            ? _modMatrix.ApplyMod(OscLevelDest[0], _oscLevels[0]) : 0.0;
        double mg1 = _oscEnabled[1] && _oscLevels[1] > 0.001
            ? _modMatrix.ApplyMod(OscLevelDest[1], _oscLevels[1]) : 0.0;
        double mg2 = _oscEnabled[2] && _oscLevels[2] > 0.001
            ? _modMatrix.ApplyMod(OscLevelDest[2], _oscLevels[2]) : 0.0;

        // SIMD mix: process Vector<double>.Count samples per iteration, scalar tail for remainder.
        {
            var vMg0 = new Vector<double>(mg0);
            var vMg1 = new Vector<double>(mg1);
            var vMg2 = new Vector<double>(mg2);
            var vNg  = new Vector<double>(noiseGain);
            int vs   = Vector<double>.Count;
            int i    = 0;
            for (; i + vs <= sampleCount; i += vs)
            {
                (new Vector<double>(_osc1BufL, i) * vMg0 + new Vector<double>(_osc2BufL, i) * vMg1
               + new Vector<double>(_osc3BufL, i) * vMg2 + new Vector<double>(_noiseBufL, i) * vNg)
                    .CopyTo(_mixLeft, i);
                (new Vector<double>(_osc1BufR, i) * vMg0 + new Vector<double>(_osc2BufR, i) * vMg1
               + new Vector<double>(_osc3BufR, i) * vMg2 + new Vector<double>(_noiseBufR, i) * vNg)
                    .CopyTo(_mixRight, i);
            }
            for (; i < sampleCount; i++)
            {
                _mixLeft[i]  = _osc1BufL[i] * mg0 + _osc2BufL[i] * mg1 + _osc3BufL[i] * mg2 + _noiseBufL[i] * noiseGain;
                _mixRight[i] = _osc1BufR[i] * mg0 + _osc2BufR[i] * mg1 + _osc3BufR[i] * mg2 + _noiseBufR[i] * noiseGain;
            }
        }

        // ── 7. Filters ────────────────────────────────────────────
        ApplyFilterEnvModulation(sampleCount);
        _filter1R.SyncParamsFrom(_filter1);
        _filter2R.SyncParamsFrom(_filter2);
        _filter1.Process(_mixLeft,  _mixLeft,  sampleCount);
        _filter1R.Process(_mixRight, _mixRight, sampleCount);
        if (_filterRouting == FilterRouting.Serial)
        {
            _filter2.Process(_mixLeft,  _mixLeft,  sampleCount);
            _filter2R.Process(_mixRight, _mixRight, sampleCount);
        }

        // ── 8. Amp envelope + output mix ─────────────────────────
        double velGain = _velocityGain;
        for (int i = 0; i < sampleCount; i++)
        {
            double gain = _envAmpBuffer[i] * velGain;
            outputLeft[i]  += _mixLeft[i]  * gain;
            outputRight[i] += _mixRight[i] * gain;
        }
    }

    // ─── Modulation application ──────────────────────────────────

    private void ApplyModulation()
    {
        // Oscillator pitch and wavetable position
        for (int i = 0; i < 3; i++)
        {
            _oscSources[i].SetFrequency(_currentFrequency *
                MathUtils.SemitonesToFrequencyRatio(_modMatrix.GetModValue(OscPitchDest[i])));

            double wtMod = _modMatrix.GetModValue(OscWtPosDest[i]);
            _oscSources[i].WavetablePosition = Math.Clamp(_oscBasePositions[i] + wtMod, 0.0, 1.0);
        }

        // Filter 1
        double f1CutMod = _modMatrix.GetModValue(ModDestination.Filter1Cutoff);
        double f1ResMod = _modMatrix.GetModValue(ModDestination.Filter1Resonance);
        double f1DrvMod = _modMatrix.GetModValue(ModDestination.Filter1Drive);
        _filter1.Cutoff    = Math.Clamp(_filter1BaseCutoff    * Math.Pow(2.0, f1CutMod), 20.0, 20000.0);
        _filter1.Resonance = Math.Clamp(_filter1BaseResonance + f1ResMod, 0.0, 0.98);
        _filter1.Drive     = Math.Clamp(_filter1BaseDrive     + f1DrvMod, 0.0, 1.0);

        // Filter 2
        double f2CutMod = _modMatrix.GetModValue(ModDestination.Filter2Cutoff);
        double f2ResMod = _modMatrix.GetModValue(ModDestination.Filter2Resonance);
        double f2DrvMod = _modMatrix.GetModValue(ModDestination.Filter2Drive);
        _filter2.Cutoff    = Math.Clamp(_filter2BaseCutoff    * Math.Pow(2.0, f2CutMod), 20.0, 20000.0);
        _filter2.Resonance = Math.Clamp(_filter2BaseResonance + f2ResMod, 0.0, 0.98);
        _filter2.Drive     = Math.Clamp(_filter2BaseDrive     + f2DrvMod, 0.0, 1.0);

        // LFO rate modulation
        ApplyLfoRateMod(_lfo1, ModDestination.Lfo1Rate);
        ApplyLfoRateMod(_lfo2, ModDestination.Lfo2Rate);
        ApplyLfoRateMod(_lfo3, ModDestination.Lfo3Rate);
    }

    private void ApplyLfoRateMod(LfoGenerator lfo, ModDestination dest)
    {
        double mod = _modMatrix.GetModValue(dest);
        if (Math.Abs(mod) > 0.001)
            lfo.Rate = Math.Clamp(lfo.Rate + mod, 0.01, 30.0);
    }

    private void ApplyFilterEnvModulation(int sampleCount)
    {
        if (Math.Abs(_filterEnvAmount) < 0.001) return;
        double avgEnv    = AverageBuffer(_envFilterBuffer, sampleCount);
        double modOctaves = _filterEnvAmount * avgEnv * 4.0;
        _filter1.Cutoff  = Math.Clamp(_filter1BaseCutoff * Math.Pow(2.0, modOctaves), 20.0, 20000.0);
    }

    // ─── Routing helpers ─────────────────────────────────────────

    /// <summary>Returns true if OSC <paramref name="mod0"/> (0-indexed) modulates any other OSC.</summary>
    private bool IsUsedAsModulator(int mod0)
    {
        for (int c = 0; c < 3; c++)
            if (c != mod0 && _routingMatrix[mod0, c].Mode != OscRouting.Mix)
                return true;
        return false;
    }

    /// <summary>
    /// Runs Kahn's topological sort on the current routing matrix.
    /// On success writes the new order to _processingOrder and returns true.
    /// On cycle detection returns false without modifying _processingOrder.
    /// Zero-allocation (uses stack arrays).
    /// </summary>
    private bool TryRecomputeProcessingOrder()
    {
        // in-degree = number of FM/RM modulators for each OSC
        int d0 = 0, d1 = 0, d2 = 0;
        for (int m = 0; m < 3; m++)
        {
            if (m != 0 && _routingMatrix[m, 0].Mode != OscRouting.Mix) d0++;
            if (m != 1 && _routingMatrix[m, 1].Mode != OscRouting.Mix) d1++;
            if (m != 2 && _routingMatrix[m, 2].Mode != OscRouting.Mix) d2++;
        }

        // Queue of nodes with in-degree == 0 (processed as stack via head/tail)
        int[] queue = new int[3];
        int qHead = 0, qTail = 0;
        int[] deg = new int[] { d0, d1, d2 };
        for (int i = 0; i < 3; i++)
            if (deg[i] == 0) queue[qTail++] = i;

        int[] newOrder = new int[3];
        int   orderIdx = 0;

        while (qHead < qTail)
        {
            int node = queue[qHead++];
            newOrder[orderIdx++] = node;
            for (int car = 0; car < 3; car++)
            {
                if (node != car && _routingMatrix[node, car].Mode != OscRouting.Mix)
                {
                    if (--deg[car] == 0) queue[qTail++] = car;
                }
            }
        }

        if (orderIdx < 3) return false; // cycle

        _processingOrder[0] = newOrder[0];
        _processingOrder[1] = newOrder[1];
        _processingOrder[2] = newOrder[2];
        return true;
    }

    // ─── Helpers ─────────────────────────────────────────────────

    private static double AverageBuffer(double[] buffer, int count)
    {
        double sum = 0.0;
        for (int i = 0; i < count; i++) sum += buffer[i];
        return sum / count;
    }
}

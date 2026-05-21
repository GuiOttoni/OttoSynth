using System;
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
    public enum VoiceState
    {
        Idle,
        Active,
        Releasing,
        Stealing
    }

    /// <summary>Filter routing options.</summary>
    public enum FilterRouting
    {
        Serial,   // OSC → Filter1 → Filter2
        Parallel, // OSC → (Filter1 + Filter2)
        Split     // OSC1→Filter1, OSC2+3→Filter2
    }

    /// <summary>Portamento/glide mode.</summary>
    public enum GlideMode { Off, Always, Legato }

    // ─── Components ─────────────────────────────────────────────

    // 3 oscillators (each with unison capability)
    private readonly WavetableOscillator _osc1;
    private readonly WavetableOscillator _osc2;
    private readonly WavetableOscillator _osc3;
    private readonly NoiseOscillator _noise;

    // 2 filters
    private readonly StateVariableFilter _filter1;
    private readonly StateVariableFilter _filter2;

    // 3 envelopes: ENV1=Amp, ENV2=Filter, ENV3=Free
    private readonly AdsrEnvelope _envAmp;
    private readonly AdsrEnvelope _envFilter;
    private readonly AdsrEnvelope _envFree;

    // 3 LFOs
    private readonly LfoGenerator _lfo1;
    private readonly LfoGenerator _lfo2;
    private readonly LfoGenerator _lfo3;

    // Modulation matrix (per-voice instance, shares routes via reference)
    private readonly ModMatrix _modMatrix;

    // ─── Mixer Levels (base values, before modulation) ──────────
    private double _osc1Level, _osc2Level, _osc3Level, _noiseLevel;
    private bool _osc1Enabled, _osc2Enabled, _osc3Enabled, _noiseEnabled;

    // ─── Filter Settings ────────────────────────────────────────
    private FilterRouting _filterRouting;
    private double _filterEnvAmount; // How much ENV2 modulates filter cutoff

    // ─── Base parameter values (before modulation) ──────────────
    // These are the "user-set" values. Modulation (env, LFO, mod matrix) is applied
    // on top of these in ApplyFilterEnvModulation and ApplyModulation.
    private double _filter1BaseCutoff = 20000.0;
    private double _filter2BaseCutoff = 20000.0;
    private double _filter1BaseResonance = 0.0;
    private double _filter2BaseResonance = 0.0;
    private double _filter1BaseDrive = 0.0;
    private double _filter2BaseDrive = 0.0;

    /// <summary>
    /// Sets the BASE values for filter 1 (the values the user requested via UI).
    /// Modulation (env, LFO) is applied on top of these — they are NOT the live cutoff.
    /// </summary>
    public void SetFilter1Base(double cutoff, double resonance, double drive)
    {
        _filter1BaseCutoff = Math.Clamp(cutoff, 20.0, 20000.0);
        _filter1BaseResonance = Math.Clamp(resonance, 0.0, 1.0);
        _filter1BaseDrive = Math.Clamp(drive, 0.0, 1.0);
    }

    /// <summary>Sets the BASE values for filter 2.</summary>
    public void SetFilter2Base(double cutoff, double resonance, double drive)
    {
        _filter2BaseCutoff = Math.Clamp(cutoff, 20.0, 20000.0);
        _filter2BaseResonance = Math.Clamp(resonance, 0.0, 1.0);
        _filter2BaseDrive = Math.Clamp(drive, 0.0, 1.0);
    }

    // ─── Pre-allocated temp buffers ─────────────────────────────
    private readonly double[] _tempLeft;
    private readonly double[] _tempRight;
    private readonly double[] _mixLeft;
    private readonly double[] _mixRight;
    private readonly double[] _envAmpBuffer;
    private readonly double[] _envFilterBuffer;
    private readonly double[] _envFreeBuffer;
    private readonly double[] _lfo1Buffer;
    private readonly double[] _lfo2Buffer;
    private readonly double[] _lfo3Buffer;

    // ─── Portamento ─────────────────────────────────────────────
    private double _sampleRate = 44100.0;
    private double _glideTime = 0.0;        // seconds; 0 = off
    private GlideMode _glideMode = GlideMode.Off;
    private double _currentFrequency = 440.0; // actual freq sent to oscillators

    // ─── State ──────────────────────────────────────────────────
    private VoiceState _state;
    private int _noteNumber;
    private int _velocity;
    private double _velocityGain;
    private long _noteOnTimestamp;
    private double _noteFrequency;  // target frequency
    private double _noteRandom;

    // Random for per-note randomization
    private static readonly Random _rng = new();

    // ─── Public Properties ──────────────────────────────────────

    public VoiceState State => _state;
    public int NoteNumber => _noteNumber;
    public int Velocity => _velocity;
    public long NoteOnTimestamp => _noteOnTimestamp;
    public bool IsActive => _state != VoiceState.Idle;
    public double CurrentAmplitude => _envAmp.CurrentValue * _velocityGain;

    // Component accessors
    public WavetableOscillator Osc1 => _osc1;
    public WavetableOscillator Osc2 => _osc2;
    public WavetableOscillator Osc3 => _osc3;
    public NoiseOscillator Noise => _noise;
    public StateVariableFilter Filter1 => _filter1;
    public StateVariableFilter Filter2 => _filter2;
    public AdsrEnvelope EnvAmp => _envAmp;
    public AdsrEnvelope EnvFilter => _envFilter;
    public AdsrEnvelope EnvFree => _envFree;
    public LfoGenerator Lfo1 => _lfo1;
    public LfoGenerator Lfo2 => _lfo2;
    public LfoGenerator Lfo3 => _lfo3;
    public ModMatrix ModMatrix => _modMatrix;

    // Backward-compatible accessors (Phase 1 code)
    public WavetableOscillator Oscillator => _osc1;
    public AdsrEnvelope AmpEnvelope => _envAmp;

    // Mixer
    public double Osc1Level { get => _osc1Level; set => _osc1Level = Math.Clamp(value, 0.0, 1.0); }
    public double Osc2Level { get => _osc2Level; set => _osc2Level = Math.Clamp(value, 0.0, 1.0); }
    public double Osc3Level { get => _osc3Level; set => _osc3Level = Math.Clamp(value, 0.0, 1.0); }
    public double NoiseLevel { get => _noiseLevel; set => _noiseLevel = Math.Clamp(value, 0.0, 1.0); }
    public bool Osc1Enabled { get => _osc1Enabled; set => _osc1Enabled = value; }
    public bool Osc2Enabled { get => _osc2Enabled; set => _osc2Enabled = value; }
    public bool Osc3Enabled { get => _osc3Enabled; set => _osc3Enabled = value; }
    public bool NoiseEnabled { get => _noiseEnabled; set => _noiseEnabled = value; }

    // Filter routing
    public FilterRouting Routing { get => _filterRouting; set => _filterRouting = value; }
    public double FilterEnvAmount { get => _filterEnvAmount; set => _filterEnvAmount = Math.Clamp(value, -1.0, 1.0); }

    // Portamento
    public double GlideTime { get => _glideTime; set => _glideTime = Math.Clamp(value, 0.0, 2.0); }
    public GlideMode Glide  { get => _glideMode; set => _glideMode = value; }

    public SynthVoice(int maxBufferSize = 1024)
    {
        // Oscillators
        _osc1 = new WavetableOscillator();
        _osc2 = new WavetableOscillator();
        _osc3 = new WavetableOscillator();
        _noise = new NoiseOscillator();

        // Filters
        _filter1 = new StateVariableFilter();
        _filter2 = new StateVariableFilter();

        // Envelopes
        _envAmp = new AdsrEnvelope();
        _envFilter = new AdsrEnvelope();
        _envFree = new AdsrEnvelope();

        // LFOs
        _lfo1 = new LfoGenerator();
        _lfo2 = new LfoGenerator();
        _lfo3 = new LfoGenerator();

        // Modulation matrix
        _modMatrix = new ModMatrix();

        // Pre-allocate all buffers
        _tempLeft = new double[maxBufferSize];
        _tempRight = new double[maxBufferSize];
        _mixLeft = new double[maxBufferSize];
        _mixRight = new double[maxBufferSize];
        _envAmpBuffer = new double[maxBufferSize];
        _envFilterBuffer = new double[maxBufferSize];
        _envFreeBuffer = new double[maxBufferSize];
        _lfo1Buffer = new double[maxBufferSize];
        _lfo2Buffer = new double[maxBufferSize];
        _lfo3Buffer = new double[maxBufferSize];

        // Default mixer state
        _osc1Level = 0.8;
        _osc2Level = 0.0;
        _osc3Level = 0.0;
        _noiseLevel = 0.0;
        _osc1Enabled = true;
        _osc2Enabled = false;
        _osc3Enabled = false;
        _noiseEnabled = false;
        _filterRouting = FilterRouting.Serial;
        _filterEnvAmount = 0.0;

        // Default filter envelope
        _envFilter.AttackTime = 0.001;
        _envFilter.DecayTime = 0.5;
        _envFilter.SustainLevel = 0.3;
        _envFilter.ReleaseTime = 0.3;

        _state = VoiceState.Idle;
        _noteNumber = -1;
        _velocity = 0;
        _velocityGain = 0.0;
        _noteRandom = 0.0;
    }

    /// <summary>Sets the sample rate for all components.</summary>
    public void SetSampleRate(double sampleRate)
    {
        _sampleRate = sampleRate;
        _osc1.SetSampleRate(sampleRate);
        _osc2.SetSampleRate(sampleRate);
        _osc3.SetSampleRate(sampleRate);
        _filter1.SetSampleRate(sampleRate);
        _filter2.SetSampleRate(sampleRate);
        _envAmp.SetSampleRate(sampleRate);
        _envFilter.SetSampleRate(sampleRate);
        _envFree.SetSampleRate(sampleRate);
        _lfo1.SetSampleRate(sampleRate);
        _lfo2.SetSampleRate(sampleRate);
        _lfo3.SetSampleRate(sampleRate);
    }

    /// <summary>Loads a wavetable into the specified oscillator (1-3).</summary>
    public void SetWavetable(int oscIndex, double[][] wavetable, bool hasMipmap)
    {
        switch (oscIndex)
        {
            case 1: _osc1.SetWavetable(wavetable, hasMipmap); break;
            case 2: _osc2.SetWavetable(wavetable, hasMipmap); break;
            case 3: _osc3.SetWavetable(wavetable, hasMipmap); break;
        }
    }

    /// <summary>Legacy compatibility: sets wavetable on OSC1.</summary>
    public void SetWavetable(double[][] wavetable, bool hasMipmap)
    {
        SetWavetable(1, wavetable, hasMipmap);
    }

    /// <summary>Triggers a note on this voice.</summary>
    public void NoteOn(int noteNumber, int velocity, long timestamp)
    {
        bool wasActive = _state != VoiceState.Idle;

        _noteNumber = noteNumber;
        _velocity = velocity;
        _velocityGain = velocity / 127.0;
        _noteOnTimestamp = timestamp;
        _noteFrequency = MathUtils.MidiNoteToFrequency(noteNumber);
        _noteRandom = _rng.NextDouble();

        // Portamento: snap or glide depending on mode + previous state
        bool shouldGlide = _glideMode == GlideMode.Always ||
                           (_glideMode == GlideMode.Legato && wasActive);
        if (!shouldGlide || _glideTime < 0.001)
            _currentFrequency = _noteFrequency;
        // else: keep _currentFrequency as-is → will glide from previous note

        // Set all oscillator frequencies to starting point
        _osc1.SetFrequency(_currentFrequency);
        _osc2.SetFrequency(_currentFrequency);
        _osc3.SetFrequency(_currentFrequency);

        // Reset phases
        _osc1.ResetPhase();
        _osc2.ResetPhase();
        _osc3.ResetPhase();

        // Set filter note frequency (for key tracking)
        _filter1.SetNoteFrequency(_noteFrequency);
        _filter2.SetNoteFrequency(_noteFrequency);
        _filter1.ResetState();
        _filter2.ResetState();

        // Trigger all envelopes
        _envAmp.NoteOn();
        _envFilter.NoteOn();
        _envFree.NoteOn();

        // Trigger LFOs
        _lfo1.NoteOn();
        _lfo2.NoteOn();
        _lfo3.NoteOn();

        _state = VoiceState.Active;
    }

    /// <summary>Releases the note on this voice (note off).</summary>
    public void NoteOff()
    {
        if (_state == VoiceState.Idle) return;

        _envAmp.NoteOff();
        _envFilter.NoteOff();
        _envFree.NoteOff();
        _state = VoiceState.Releasing;
    }

    /// <summary>Forces a rapid release for voice stealing (anti-click fadeout).</summary>
    public void ForceSteal(double fadeTimeMs = 3.0)
    {
        _state = VoiceState.Stealing;
        _envAmp.ForceRelease(fadeTimeMs);
    }

    /// <summary>Resets the voice to idle state.</summary>
    public void Reset()
    {
        _state = VoiceState.Idle;
        _noteNumber = -1;
        _velocity = 0;
        _velocityGain = 0.0;
        _noteRandom = 0.0;
        _envAmp.Reset();
        _envFilter.Reset();
        _envFree.Reset();
        _lfo1.Reset();
        _lfo2.Reset();
        _lfo3.Reset();
    }

    /// <summary>
    /// Processes audio for this voice and mixes into the output buffer.
    /// Full signal chain: Envelopes/LFOs → ModMatrix → Oscillators → Mixer → Filter → Amp → Output.
    /// </summary>
    public void Process(double[] outputLeft, double[] outputRight, int sampleCount)
    {
        if (_state == VoiceState.Idle) return;

        // ─── 1. Process Envelopes ───────────────────────────────
        bool ampActive = _envAmp.Process(_envAmpBuffer, sampleCount);

        if (!ampActive)
        {
            _state = VoiceState.Idle;
            _noteNumber = -1;
            return;
        }

        _envFilter.Process(_envFilterBuffer, sampleCount);
        _envFree.Process(_envFreeBuffer, sampleCount);

        // ─── 2. Process LFOs ────────────────────────────────────
        _lfo1.Process(_lfo1Buffer, sampleCount);
        _lfo2.Process(_lfo2Buffer, sampleCount);
        _lfo3.Process(_lfo3Buffer, sampleCount);

        // ─── 3. Process ModMatrix (per-block averages) ──────────
        // Compute average values for this block
        double avgEnv1 = AverageBuffer(_envAmpBuffer, sampleCount);
        double avgEnv2 = AverageBuffer(_envFilterBuffer, sampleCount);
        double avgEnv3 = AverageBuffer(_envFreeBuffer, sampleCount);
        double avgLfo1 = AverageBuffer(_lfo1Buffer, sampleCount);
        double avgLfo2 = AverageBuffer(_lfo2Buffer, sampleCount);
        double avgLfo3 = AverageBuffer(_lfo3Buffer, sampleCount);

        _modMatrix.SetVoiceSources(
            avgEnv1, avgEnv2, avgEnv3,
            avgLfo1, avgLfo2, avgLfo3,
            _velocityGain, _noteNumber, _noteRandom);

        _modMatrix.Process();

        // ─── 4. Update portamento glide (per-block, log-space interpolation) ─
        if (_glideMode != GlideMode.Off && _glideTime > 0.001 &&
            Math.Abs(_currentFrequency - _noteFrequency) > 0.01)
        {
            double coeff = 1.0 - Math.Exp(-(double)sampleCount / (_sampleRate * _glideTime));
            double logCurr = Math.Log(_currentFrequency);
            double logTgt  = Math.Log(_noteFrequency);
            _currentFrequency = Math.Exp(logCurr + (logTgt - logCurr) * coeff);
        }
        else
        {
            _currentFrequency = _noteFrequency;
        }

        // ─── 5. Apply ModMatrix to parameters ───────────────────
        ApplyModulation();

        // ─── 6. Mix Oscillators into mono buffer ────────────────
        Array.Clear(_mixLeft, 0, sampleCount);

        // OSC1
        if (_osc1Enabled && _osc1Level > 0.001)
        {
            double modLevel = _modMatrix.ApplyMod(ModDestination.Osc1Level, _osc1Level);
            Array.Clear(_tempLeft, 0, sampleCount);
            Array.Clear(_tempRight, 0, sampleCount);
            _osc1.Process(_tempLeft, _tempRight, sampleCount);
            for (int i = 0; i < sampleCount; i++)
                _mixLeft[i] += (_tempLeft[i] + _tempRight[i]) * 0.5 * modLevel;
        }

        // OSC2
        if (_osc2Enabled && _osc2Level > 0.001)
        {
            double modLevel = _modMatrix.ApplyMod(ModDestination.Osc2Level, _osc2Level);
            Array.Clear(_tempLeft, 0, sampleCount);
            Array.Clear(_tempRight, 0, sampleCount);
            _osc2.Process(_tempLeft, _tempRight, sampleCount);
            for (int i = 0; i < sampleCount; i++)
                _mixLeft[i] += (_tempLeft[i] + _tempRight[i]) * 0.5 * modLevel;
        }

        // OSC3
        if (_osc3Enabled && _osc3Level > 0.001)
        {
            double modLevel = _modMatrix.ApplyMod(ModDestination.Osc3Level, _osc3Level);
            Array.Clear(_tempLeft, 0, sampleCount);
            Array.Clear(_tempRight, 0, sampleCount);
            _osc3.Process(_tempLeft, _tempRight, sampleCount);
            for (int i = 0; i < sampleCount; i++)
                _mixLeft[i] += (_tempLeft[i] + _tempRight[i]) * 0.5 * modLevel;
        }

        // Noise
        if (_noiseEnabled && _noiseLevel > 0.001)
        {
            double modLevel = _modMatrix.ApplyMod(ModDestination.NoiseLevel, _noiseLevel);
            Array.Clear(_tempLeft, 0, sampleCount);
            Array.Clear(_tempRight, 0, sampleCount);
            _noise.Process(_tempLeft, _tempRight, sampleCount);
            for (int i = 0; i < sampleCount; i++)
                _mixLeft[i] += (_tempLeft[i] + _tempRight[i]) * 0.5 * modLevel;
        }

        // ─── 7. Apply Filter(s) on mono signal ─────────────────
        // Filter envelope modulation (legacy, works alongside mod matrix)
        ApplyFilterEnvModulation(sampleCount);

        // Process filter(s) on the mono mix (_mixLeft)
        _filter1.Process(_mixLeft, _mixLeft, sampleCount);

        if (_filterRouting == FilterRouting.Serial)
        {
            _filter2.Process(_mixLeft, _mixLeft, sampleCount);
        }

        // ─── 8. Apply Amp Envelope + Velocity and mix to stereo output
        double velGain = _velocityGain;
        for (int i = 0; i < sampleCount; i++)
        {
            double gain = _envAmpBuffer[i] * velGain;
            double sample = _mixLeft[i] * gain;
            outputLeft[i] += sample;
            outputRight[i] += sample;
        }
    }

    // ─── Modulation Application ─────────────────────────────────

    /// <summary>
    /// Applies modulation matrix outputs to all relevant parameters.
    /// Called once per block after ModMatrix.Process().
    /// </summary>
    private void ApplyModulation()
    {
        // Oscillator pitch: always apply from _currentFrequency (supports portamento + mod)
        _osc1.SetFrequency(_currentFrequency *
            MathUtils.SemitonesToFrequencyRatio(_modMatrix.GetModValue(ModDestination.Osc1Pitch)));
        _osc2.SetFrequency(_currentFrequency *
            MathUtils.SemitonesToFrequencyRatio(_modMatrix.GetModValue(ModDestination.Osc2Pitch)));
        _osc3.SetFrequency(_currentFrequency *
            MathUtils.SemitonesToFrequencyRatio(_modMatrix.GetModValue(ModDestination.Osc3Pitch)));

        // Wavetable position modulation
        double wt1Mod = _modMatrix.GetModValue(ModDestination.Osc1WavetablePos);
        if (Math.Abs(wt1Mod) > 0.001)
            _osc1.WavetablePosition = Math.Clamp(_osc1.WavetablePosition + wt1Mod, 0.0, 1.0);
        double wt2Mod = _modMatrix.GetModValue(ModDestination.Osc2WavetablePos);
        if (Math.Abs(wt2Mod) > 0.001)
            _osc2.WavetablePosition = Math.Clamp(_osc2.WavetablePosition + wt2Mod, 0.0, 1.0);
        double wt3Mod = _modMatrix.GetModValue(ModDestination.Osc3WavetablePos);
        if (Math.Abs(wt3Mod) > 0.001)
            _osc3.WavetablePosition = Math.Clamp(_osc3.WavetablePosition + wt3Mod, 0.0, 1.0);

        // Filter modulation: always reset to base first, then apply mod offset.
        // Filter cutoff: logarithmic — mod value is in octaves.
        double f1CutoffMod = _modMatrix.GetModValue(ModDestination.Filter1Cutoff);
        double f1ResMod = _modMatrix.GetModValue(ModDestination.Filter1Resonance);
        double f1DriveMod = _modMatrix.GetModValue(ModDestination.Filter1Drive);
        _filter1.Cutoff = Math.Clamp(_filter1BaseCutoff * Math.Pow(2.0, f1CutoffMod), 20.0, 20000.0);
        _filter1.Resonance = Math.Clamp(_filter1BaseResonance + f1ResMod, 0.0, 0.98);
        _filter1.Drive = Math.Clamp(_filter1BaseDrive + f1DriveMod, 0.0, 1.0);

        double f2CutoffMod = _modMatrix.GetModValue(ModDestination.Filter2Cutoff);
        double f2ResMod = _modMatrix.GetModValue(ModDestination.Filter2Resonance);
        double f2DriveMod = _modMatrix.GetModValue(ModDestination.Filter2Drive);
        _filter2.Cutoff = Math.Clamp(_filter2BaseCutoff * Math.Pow(2.0, f2CutoffMod), 20.0, 20000.0);
        _filter2.Resonance = Math.Clamp(_filter2BaseResonance + f2ResMod, 0.0, 0.98);
        _filter2.Drive = Math.Clamp(_filter2BaseDrive + f2DriveMod, 0.0, 1.0);

        // LFO rate modulation
        double lfo1RateMod = _modMatrix.GetModValue(ModDestination.Lfo1Rate);
        if (Math.Abs(lfo1RateMod) > 0.001)
            _lfo1.Rate = Math.Clamp(_lfo1.Rate + lfo1RateMod, 0.01, 30.0);

        double lfo2RateMod = _modMatrix.GetModValue(ModDestination.Lfo2Rate);
        if (Math.Abs(lfo2RateMod) > 0.001)
            _lfo2.Rate = Math.Clamp(_lfo2.Rate + lfo2RateMod, 0.01, 30.0);

        double lfo3RateMod = _modMatrix.GetModValue(ModDestination.Lfo3Rate);
        if (Math.Abs(lfo3RateMod) > 0.001)
            _lfo3.Rate = Math.Clamp(_lfo3.Rate + lfo3RateMod, 0.01, 30.0);
    }

    // ─── Helpers ─────────────────────────────────────────────────

    /// <summary>Mixes source into destination with a level multiplier.</summary>
    private static void MixInto(double[] destL, double[] destR,
        double[] srcL, double[] srcR, double level, int count)
    {
        for (int i = 0; i < count; i++)
        {
            destL[i] += srcL[i] * level;
            destR[i] += srcR[i] * level;
        }
    }

    /// <summary>Computes the average of a buffer.</summary>
    private static double AverageBuffer(double[] buffer, int count)
    {
        double sum = 0.0;
        for (int i = 0; i < count; i++)
            sum += buffer[i];
        return sum / count;
    }

    /// <summary>
    /// Applies the filter envelope to the filter cutoff per-block.
    /// This stacks ON TOP of ApplyModulation's effect, so we read the current
    /// (already-modulated) live cutoff and modulate further.
    /// Uses 4 octaves max range (instead of 7) so the cutoff control stays useful
    /// at extreme envelope amounts.
    /// </summary>
    private void ApplyFilterEnvModulation(int sampleCount)
    {
        if (Math.Abs(_filterEnvAmount) < 0.001) return;

        double avgEnvFilter = AverageBuffer(_envFilterBuffer, sampleCount);

        // Filter env modulates AROUND the base cutoff (set by user via UI).
        // envAmount in [-1,+1], envValue in [0,1], range = 4 octaves
        double modOctaves = _filterEnvAmount * avgEnvFilter * 4.0;
        double modCutoff = Math.Clamp(_filter1BaseCutoff * Math.Pow(2.0, modOctaves), 20.0, 20000.0);
        _filter1.Cutoff = modCutoff;
    }
}

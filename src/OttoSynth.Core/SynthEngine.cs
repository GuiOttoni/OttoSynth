using System;
using System.Collections.Generic;
using OttoSynth.Core.Diagnostics;
using OttoSynth.Core.DSP;
using OttoSynth.Core.DSP.Effects;
using OttoSynth.Core.DSP.Filters;
using OttoSynth.Core.DSP.Modulation;
using OttoSynth.Core.DSP.Oscillators;
using OttoSynth.Core.DSP.Utils;
using OttoSynth.Core.Midi;
using OttoSynth.Core.Voice;

namespace OttoSynth.Core;

/// <summary>
/// Main synthesizer engine. Orchestrates voices, MIDI input, and audio output.
/// This is the central class that the VST3 plugin and standalone app interact with.
/// </summary>
public sealed class SynthEngine
{
    private readonly VoiceManager _voiceManager;
    private readonly MacroControls _macros;
    private readonly EffectsChain _effectsChain;
    private readonly DcBlocker _dcBlocker;

    // Global parameters
    private double _masterVolume;
    private double _pitchBendRange;
    private double _modWheelValue;
    private double _glideTime;
    private SynthVoice.GlideMode _glideMode;

    // Engine state
    private double _sampleRate;
    private int    _bufferSize;
    private long   _samplePosition;

    // Tempo
    public int Bpm { get; set; } = 120;

    // Arpeggiator and step sequencer
    public ArpeggiatorEngine Arpeggiator { get; } = new();
    public SequencerEngine   Sequencer   { get; } = new();

    // ─── MIDI CC → Macro mapping ────────────────────────────────
    // Each macro (0-3) can be assigned to a MIDI CC number.
    // Value 255 means "not mapped". Written only on MIDI/UI thread; read on audio thread.
    private readonly byte[] _macroCcNumbers = new byte[4] { 255, 255, 255, 255 };
    private volatile int _learningMacroIndex = -1; // -1 = not learning
    private Action<int, byte>? _onCcLearned;       // (macroIndex, ccNumber) — called on MIDI thread

    /// <summary>True while waiting for the next incoming CC to assign to a macro.</summary>
    public bool IsLearningCc => _learningMacroIndex >= 0;

    /// <summary>
    /// Puts the engine into CC-learn mode for the given macro (0-3).
    /// The next incoming CC event will be permanently assigned to that macro.
    /// <paramref name="onLearned"/> is invoked on the MIDI thread when assignment completes.
    /// </summary>
    public void LearnMacroCc(int macroIndex, Action<int, byte>? onLearned = null)
    {
        if (macroIndex < 0 || macroIndex > 3) return;
        _learningMacroIndex = macroIndex;
        _onCcLearned        = onLearned;
    }

    /// <summary>Cancels any active CC-learn operation.</summary>
    public void CancelLearn()
    {
        _learningMacroIndex = -1;
        _onCcLearned        = null;
    }

    /// <summary>Manually assigns a CC number (0-127) to a macro (0-3). 255 = unmap.</summary>
    public void MapMacroCc(int macroIndex, byte ccNumber)
    {
        if (macroIndex < 0 || macroIndex > 3) return;
        _macroCcNumbers[macroIndex] = ccNumber;
    }

    /// <summary>Returns the CC number assigned to a macro (0-3), or 255 if not mapped.</summary>
    public byte GetMacroCcNumber(int macroIndex)
        => (macroIndex >= 0 && macroIndex <= 3) ? _macroCcNumbers[macroIndex] : (byte)255;

    // Wavetable storage
    private readonly Dictionary<string, double[][]> _wavetables;
    private string _currentWavetableName;
    private readonly string[] _oscWavetableNames = ["Saw", "Sine", "Triangle"];

    /// <summary>The voice manager for this engine.</summary>
    public VoiceManager VoiceManager => _voiceManager;

    /// <summary>The macro controls for this engine.</summary>
    public MacroControls Macros => _macros;

    /// <summary>The global effects chain.</summary>
    public EffectsChain Effects => _effectsChain;

    /// <summary>Master volume (0..1).</summary>
    public double MasterVolume
    {
        get => _masterVolume;
        set => _masterVolume = MathUtils.Clamp01(value);
    }

    /// <summary>Pitch bend range in semitones.</summary>
    public double PitchBendRange
    {
        get => _pitchBendRange;
        set => _pitchBendRange = Math.Clamp(value, 0.0, 24.0);
    }

    /// <summary>Current sample rate.</summary>
    public double SampleRate => _sampleRate;

    /// <summary>Current buffer size.</summary>
    public int BufferSize => _bufferSize;

    /// <summary>Current mod wheel value (0..1).</summary>
    public double ModWheelValue => _modWheelValue;

    /// <summary>Name of the wavetable loaded in OSC1 (legacy compat).</summary>
    public string CurrentWavetableName => _currentWavetableName;

    /// <summary>Returns the wavetable name for a specific oscillator (1-indexed).</summary>
    public string GetOscWavetableName(int oscIndex) =>
        (oscIndex >= 1 && oscIndex <= 3) ? _oscWavetableNames[oscIndex - 1] : "Saw";

    /// <summary>Available wavetable names.</summary>
    public IReadOnlyCollection<string> WavetableNames => _wavetables.Keys;

    /// <summary>Current glide (portamento) time in seconds.</summary>
    public double GlideTime => _glideTime;

    /// <summary>Current glide mode.</summary>
    public SynthVoice.GlideMode GlideMode => _glideMode;

    public SynthEngine(int maxVoices = 16, int maxBufferSize = 1024)
    {
        _voiceManager = new VoiceManager(maxVoices, maxBufferSize);
        _macros = new MacroControls();
        _effectsChain = new EffectsChain();
        _dcBlocker = new DcBlocker();

        _masterVolume = 0.8;
        _pitchBendRange = 2.0;
        _modWheelValue = 0.0;
        _sampleRate = 44100.0;
        _bufferSize = 256;

        // Share macros with all voices
        _voiceManager.SetMacros(_macros);

        // Initialize basic wavetables
        _wavetables = new Dictionary<string, double[][]>();
        _currentWavetableName = "Saw";

        LoadDefaultWavetables();
    }

    /// <summary>
    /// Initializes the engine with the given sample rate and buffer size.
    /// Called by the VST3 host or standalone app.
    /// </summary>
    public void Initialize(double sampleRate, int bufferSize)
    {
        _sampleRate = sampleRate;
        _bufferSize = bufferSize;

        _voiceManager.SetSampleRate(sampleRate);
        _effectsChain.SetSampleRate(sampleRate);
        _dcBlocker.SetSampleRate(sampleRate);

        // Regenerate wavetables for the correct sample rate
        LoadDefaultWavetables();
        SelectWavetable(_currentWavetableName);

    }

    /// <summary>
    /// Selects a wavetable by name for OSC1 (backward compatible).
    /// </summary>
    public void SelectWavetable(string name)
    {
        if (_wavetables.TryGetValue(name, out var wavetable))
        {
            _currentWavetableName = name;
            bool hasMipmap = name != "Sine";
            _voiceManager.SetWavetable(1, wavetable, hasMipmap);
        }
    }

    /// <summary>
    /// Selects a wavetable by name for a specific oscillator (1-3).
    /// </summary>
    public void SelectWavetable(int oscIndex, string name)
    {
        if (_wavetables.TryGetValue(name, out var wavetable))
        {
            bool hasMipmap = name != "Sine";
            _voiceManager.SetWavetable(oscIndex, wavetable, hasMipmap);
            if (oscIndex >= 1 && oscIndex <= 3)
                _oscWavetableNames[oscIndex - 1] = name;
        }
    }

    /// <summary>
    /// Loads a wavetable from a WAV file and assigns it to an oscillator (1-3).
    /// The wavetable name defaults to the file name without extension.
    /// Returns the name used (can be passed back to <see cref="SelectWavetable"/>).
    /// Throws <see cref="InvalidDataException"/> if the file is invalid.
    /// </summary>
    public string LoadWavetableFromFile(string filePath, int oscIndex)
    {
        var frames = WavetableLoader.Load(filePath);
        string name = Path.GetFileNameWithoutExtension(filePath);

        // Avoid name collisions with built-in wavetables by appending a counter
        string uniqueName = name;
        int suffix = 1;
        while (_wavetables.ContainsKey(uniqueName) && !IsUserWavetable(uniqueName))
            uniqueName = $"{name} ({++suffix})";

        _wavetables[uniqueName] = frames;
        _voiceManager.SetWavetable(oscIndex, frames, hasMipmap: false);
        if (oscIndex >= 1 && oscIndex <= 3)
            _oscWavetableNames[oscIndex - 1] = uniqueName;
        return uniqueName;
    }

    private static readonly System.Collections.Generic.HashSet<string> _builtinNames =
        new(["Saw","Sine","Square","Triangle","Pulse25","Pulse10","SoftSaw","BrightSaw",
             "OddHarmonics","EvenHarmonics","HarmonicSeries","Organ","Violin","Bell",
             "HalfSine","Staircase","WarmPad","Spectral"]);

    private static bool IsUserWavetable(string name) => !_builtinNames.Contains(name);

    /// <summary>
    /// Sets the amplitude envelope parameters for all voices.
    /// </summary>
    public void SetEnvelope(double attack, double decay, double sustain, double release)
    {
        _voiceManager.SetEnvelopeParameters(attack, decay, sustain, release);
    }

    /// <summary>
    /// Sets the filter envelope (ENV2) parameters.
    /// </summary>
    public void SetFilterEnvelope(double attack, double decay, double sustain, double release)
    {
        _voiceManager.SetFilterEnvelopeParameters(attack, decay, sustain, release);
    }

    /// <summary>
    /// Sets filter parameters for Filter 1 or 2.
    /// </summary>
    public void SetFilter(int filterIndex, StateVariableFilter.FilterMode mode,
        double cutoff, double resonance, double drive = 0.0, bool is24dB = false)
    {
        try
        {
            _voiceManager.SetFilterParameters(filterIndex, mode, cutoff, resonance, drive, is24dB);
        }
        catch (Exception ex)
        {
            Logger.Error("SynthEngine.SetFilter", ex,
                $"filter={filterIndex} mode={mode} cutoff={cutoff:F1} res={resonance:F2}");
        }
    }

    /// <summary>Sets formant filter parameters for Filter 1 or 2.</summary>
    public void SetFormantParams(int filterIndex, double vowel, double shift)
    {
        _voiceManager.SetFormantParameters(filterIndex, vowel, shift);
    }

    /// <summary>
    /// Sets the filter envelope modulation amount.
    /// </summary>
    public void SetFilterEnvAmount(double amount)
    {
        _voiceManager.SetFilterEnvAmount(amount);
    }

    /// <summary>Sets the filter routing mode.</summary>
    public void SetFilterRouting(SynthVoice.FilterRouting routing)
    {
        _voiceManager.SetFilterRouting(routing);
    }

    /// <summary>Sets portamento (glide) time and mode across all voices.</summary>
    public void SetPortamento(double glideTime, SynthVoice.GlideMode mode)
    {
        _glideTime = glideTime;
        _glideMode = mode;
        _voiceManager.SetPortamento(glideTime, mode);
    }

    /// <summary>Sets the sustain pedal state (CC#64).</summary>
    public void SetSustainPedal(bool on)
    {
        _voiceManager.SetSustainPedal(on);
    }

    /// <summary>
    /// Sets LFO parameters.
    /// </summary>
    public void SetLfo(int lfoIndex, LfoGenerator.LfoShape shape,
        double rate, double depth, bool retrigger = true)
    {
        _voiceManager.SetLfoParameters(lfoIndex, shape, rate, depth, retrigger);
    }

    /// <summary>
    /// Sets the oscillator mix level and enabled state.
    /// </summary>
    public void SetOscillatorMix(int oscIndex, double level, bool enabled)
    {
        _voiceManager.SetOscillatorMix(oscIndex, level, enabled);
    }

    /// <summary>Sets per-voice oscillator parameters (tune, position, warp, pan) for all voices.</summary>
    public void SetOscillatorParams(int oscIndex, int coarseTune, double fineTune,
        double position, double warpAmount, double pan)
        => _voiceManager.SetOscillatorParams(oscIndex, coarseTune, fineTune, position, warpAmount, pan);

    /// <summary>Sets the unison configuration for a specific oscillator (1-3).</summary>
    public void SetOscillatorUnison(int oscIndex, int voiceCount, double detuneCents, double spread)
        => _voiceManager.SetOscillatorUnison(oscIndex, voiceCount, detuneCents, spread);

    /// <summary>Sets the free envelope (ENV3) parameters across all voices.</summary>
    public void SetFreeEnvelope(double attack, double decay, double sustain, double release)
        => _voiceManager.SetFreeEnvelopeParameters(attack, decay, sustain, release);

    /// <summary>Sets the warp type for a specific oscillator across all voices.</summary>
    public void SetOscillatorWarp(int oscIndex, WavetableOscillator.WaveWarp warp)
        => _voiceManager.SetOscillatorWarp(oscIndex, warp);

    /// <summary>Sets the routing from modulator to carrier (both 1-indexed, 1-3).</summary>
    public void SetOscillatorRouting(int modulator, int carrier, SynthVoice.OscRouting routing, double depth)
        => _voiceManager.SetOscillatorRouting(modulator, carrier, routing, depth);

    /// <summary>Returns the routing mode for a modulator→carrier pair (1-indexed).</summary>
    public SynthVoice.OscRouting GetOscillatorRouting(int modulator, int carrier)
        => _voiceManager.GetOscillatorRouting(modulator, carrier);

    /// <summary>Returns the FM depth for a modulator→carrier pair (1-indexed).</summary>
    public double GetOscillatorFmDepth(int modulator, int carrier)
        => _voiceManager.GetOscillatorFmDepth(modulator, carrier);

    // ─── Modulation Matrix API ──────────────────────────────────

    /// <summary>Adds a modulation route (Source → Destination with amount).</summary>
    public int AddModRoute(ModSource source, ModDestination destination, double amount)
    {
        return _voiceManager.AddModRoute(source, destination, amount);
    }

    /// <summary>Removes a modulation route by index.</summary>
    public void RemoveModRoute(int routeIndex)
    {
        _voiceManager.RemoveModRoute(routeIndex);
    }

    /// <summary>Sets the amount of an existing modulation route.</summary>
    public void SetModRouteAmount(int routeIndex, double amount)
    {
        _voiceManager.SetModRouteAmount(routeIndex, amount);
    }

    /// <summary>Clears all modulation routes.</summary>
    public void ClearModRoutes()
    {
        _voiceManager.ClearModRoutes();
    }

    /// <summary>Sets a macro control value (0-3, 0..1).</summary>
    public void SetMacro(int index, double value)
    {
        _macros[index] = value;
    }

    /// <summary>
    /// Processes a MIDI event.
    /// </summary>
    public void ProcessMidiEvent(MidiEvent midiEvent)
    {
        try
        {
            switch (midiEvent.Type)
            {
                case MidiEvent.EventType.NoteOn:
                    if (midiEvent.Data2 == 0)
                    {
                        // Some controllers send NoteOn with velocity 0 as NoteOff
                        if (Arpeggiator.Enabled) Arpeggiator.NoteOff(midiEvent.Data1);
                        else _voiceManager.NoteOff(midiEvent.Data1);
                        Logger.Trace("MIDI", $"NoteOff (vel0) note={midiEvent.Data1}");
                    }
                    else
                    {
                        if (Arpeggiator.Enabled) Arpeggiator.NoteOn(midiEvent.Data1);
                        else _voiceManager.NoteOn(midiEvent.Data1, midiEvent.Data2);
                        Logger.Trace("MIDI", $"NoteOn  note={midiEvent.Data1} vel={midiEvent.Data2}");
                    }
                    break;

                case MidiEvent.EventType.NoteOff:
                    if (Arpeggiator.Enabled) Arpeggiator.NoteOff(midiEvent.Data1);
                    else _voiceManager.NoteOff(midiEvent.Data1);
                    Logger.Trace("MIDI", $"NoteOff note={midiEvent.Data1}");
                    break;

                case MidiEvent.EventType.PitchBend:
                    _voiceManager.SetPitchBend(midiEvent.PitchBendValue / 8192.0);
                    break;

                case MidiEvent.EventType.ModWheel:
                    _modWheelValue = midiEvent.Data2 / 127.0;
                    _voiceManager.SetModWheel(_modWheelValue);
                    break;

                case MidiEvent.EventType.ControlChange:
                    Logger.Trace("MIDI", $"CC #{midiEvent.Data1} = {midiEvent.Data2}");
                    HandleControlChange(midiEvent.Data1, midiEvent.Data2);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("SynthEngine.ProcessMidiEvent", ex,
                $"Type={midiEvent.Type} D1={midiEvent.Data1} D2={midiEvent.Data2}");
        }
    }

    /// <summary>
    /// Processes a block of audio. Main audio processing method.
    /// Called by the VST3 host or standalone audio callback.
    /// </summary>
    /// <param name="outputLeft">Left channel output buffer.</param>
    /// <param name="outputRight">Right channel output buffer.</param>
    /// <param name="sampleCount">Number of samples to process.</param>
    public void ProcessAudio(double[] outputLeft, double[] outputRight, int sampleCount)
    {
        try
        {
            // Defensive bounds check
            if (outputLeft == null || outputRight == null || sampleCount <= 0)
                return;
            if (sampleCount > outputLeft.Length) sampleCount = outputLeft.Length;
            if (sampleCount > outputRight.Length) sampleCount = outputRight.Length;

            // Clear output
            Array.Clear(outputLeft, 0, sampleCount);
            Array.Clear(outputRight, 0, sampleCount);

            // Tick arpeggiator and sequencer (generate NoteOn/NoteOff into VoiceManager)
            Arpeggiator.Tick(_samplePosition, sampleCount, (int)_sampleRate, Bpm,
                (n, v) => _voiceManager.NoteOn(n, v),
                n       => _voiceManager.NoteOff(n));
            Sequencer.Tick(_samplePosition, sampleCount, (int)_sampleRate, Bpm,
                (n, v) => _voiceManager.NoteOn(n, v),
                n       => _voiceManager.NoteOff(n));
            _samplePosition += sampleCount;

            // Process all active voices (they mix into the output)
            _voiceManager.Process(outputLeft, outputRight, sampleCount);

            // Apply global effects chain
            _effectsChain.Process(outputLeft, outputRight, sampleCount);

            // Remove DC offset that may have accumulated through non-linear effects
            _dcBlocker.ProcessStereo(outputLeft, outputRight, sampleCount);

            // Apply master volume
            if (Math.Abs(_masterVolume - 1.0) > 0.0001)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    outputLeft[i] *= _masterVolume;
                    outputRight[i] *= _masterVolume;
                }
            }

            // Soft clip + NaN guard to prevent hard clipping or denormal explosions
            for (int i = 0; i < sampleCount; i++)
            {
                double l = outputLeft[i];
                double r = outputRight[i];
                if (double.IsNaN(l) || double.IsInfinity(l)) l = 0;
                if (double.IsNaN(r) || double.IsInfinity(r)) r = 0;
                outputLeft[i] = MathUtils.SoftClip(l);
                outputRight[i] = MathUtils.SoftClip(r);
            }
        }
        catch (Exception ex)
        {
            // Fail-safe: silence the output and log. Never crash the audio thread.
            Logger.Error("SynthEngine.ProcessAudio", ex);
            if (outputLeft != null) Array.Clear(outputLeft, 0, Math.Min(sampleCount, outputLeft.Length));
            if (outputRight != null) Array.Clear(outputRight, 0, Math.Min(sampleCount, outputRight.Length));
        }
    }

    /// <summary>
    /// Convenience method: processes MIDI events and audio in one call.
    /// </summary>
    public void Process(Span<MidiEvent> midiEvents, double[] outputLeft, double[] outputRight, int sampleCount)
    {
        // Process MIDI events (all at start of buffer for Phase 1)
        for (int i = 0; i < midiEvents.Length; i++)
        {
            ProcessMidiEvent(midiEvents[i]);
        }

        // Process audio
        ProcessAudio(outputLeft, outputRight, sampleCount);
    }

    /// <summary>All notes off / panic.</summary>
    public void AllNotesOff()
    {
        _voiceManager.AllNotesOff();
    }

    /// <summary>Hard reset.</summary>
    public void Reset()
    {
        _voiceManager.Reset();
        _modWheelValue = 0.0;
    }

    private void LoadDefaultWavetables()
    {
        double sr = _sampleRate;
        _wavetables["Sine"]          = BasicWavetables.GenerateSine();
        _wavetables["Saw"]           = BasicWavetables.GenerateSaw(sampleRate: sr);
        _wavetables["Square"]        = BasicWavetables.GenerateSquare(sampleRate: sr);
        _wavetables["Triangle"]      = BasicWavetables.GenerateTriangle(sampleRate: sr);
        _wavetables["Pulse 25%"]     = BasicWavetables.GeneratePulse25(sampleRate: sr);
        _wavetables["Pulse 10%"]     = BasicWavetables.GeneratePulse10(sampleRate: sr);
        _wavetables["Soft Saw"]      = BasicWavetables.GenerateSoftSaw(sampleRate: sr);
        _wavetables["Bright Saw"]    = BasicWavetables.GenerateBrightSaw(sampleRate: sr);
        _wavetables["Odd Harmonics"] = BasicWavetables.GenerateOddHarmonics(sampleRate: sr);
        _wavetables["Even Harmonics"]= BasicWavetables.GenerateEvenHarmonics(sampleRate: sr);
        _wavetables["Harmonic Series"]= BasicWavetables.GenerateHarmonicSeries(sampleRate: sr);
        _wavetables["Organ"]         = BasicWavetables.GenerateOrgan(sampleRate: sr);
        _wavetables["Violin"]        = BasicWavetables.GenerateViolin(sampleRate: sr);
        _wavetables["Bell"]          = BasicWavetables.GenerateBell(sampleRate: sr);
        _wavetables["Half Sine"]     = BasicWavetables.GenerateHalfSine(sampleRate: sr);
        _wavetables["Staircase"]     = BasicWavetables.GenerateStaircase(sampleRate: sr);
        _wavetables["Warm Pad"]      = BasicWavetables.GenerateWarmPad(sampleRate: sr);
        _wavetables["Spectral"]      = BasicWavetables.GenerateSpectral(sampleRate: sr);
    }

    private void HandleControlChange(byte ccNumber, byte value)
    {
        // ── MIDI Learn: capture next incoming CC for macro assignment ──────────
        int learningIdx = _learningMacroIndex;
        if (learningIdx >= 0)
        {
            _macroCcNumbers[learningIdx] = ccNumber;
            _learningMacroIndex = -1;
            var cb = _onCcLearned;
            _onCcLearned = null;
            cb?.Invoke(learningIdx, ccNumber); // fires on MIDI thread; caller marshals to UI
        }

        // ── CC → Macro routing (user-assigned) ────────────────────────────────
        double normalized = value / 127.0;
        for (int i = 0; i < 4; i++)
            if (_macroCcNumbers[i] == ccNumber)
                _macros[i] = normalized;

        // ── Standard / hardwired CCs ──────────────────────────────────────────
        switch (ccNumber)
        {
            case 1:  // Mod Wheel (also handled via MidiEvent.ModWheel — guard for raw CC path)
                _modWheelValue = normalized;
                _voiceManager.SetModWheel(normalized);
                break;
            case 64: // Sustain Pedal
                SetSustainPedal(value >= 64);
                break;
            case 120: // All Sound Off
            case 123: // All Notes Off
                AllNotesOff();
                break;
        }
    }
}

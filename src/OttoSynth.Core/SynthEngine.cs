using System;
using System.Collections.Generic;
using OttoSynth.Core.Diagnostics;
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
    private readonly AudioBuffer _outputBuffer;
    private readonly MacroControls _macros;
    private readonly EffectsChain _effectsChain;
    private readonly DcBlocker _dcBlocker;

    // Global parameters
    private double _masterVolume;
    private double _pitchBendSemitones;
    private double _pitchBendRange;
    private double _modWheelValue;
    private double _glideTime;
    private SynthVoice.GlideMode _glideMode;

    // Engine state
    private double _sampleRate;
    private int _bufferSize;
    private bool _isInitialized;

    // Wavetable storage
    private readonly Dictionary<string, double[][]> _wavetables;
    private string _currentWavetableName;

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

    /// <summary>Name of the currently loaded wavetable.</summary>
    public string CurrentWavetableName => _currentWavetableName;

    /// <summary>Available wavetable names.</summary>
    public IReadOnlyCollection<string> WavetableNames => _wavetables.Keys;

    /// <summary>Current glide (portamento) time in seconds.</summary>
    public double GlideTime => _glideTime;

    /// <summary>Current glide mode.</summary>
    public SynthVoice.GlideMode GlideMode => _glideMode;

    public SynthEngine(int maxVoices = 16, int maxBufferSize = 1024)
    {
        _voiceManager = new VoiceManager(maxVoices, maxBufferSize);
        _outputBuffer = new AudioBuffer(maxBufferSize);
        _macros = new MacroControls();
        _effectsChain = new EffectsChain();
        _dcBlocker = new DcBlocker();

        _masterVolume = 0.8;
        _pitchBendRange = 2.0;
        _pitchBendSemitones = 0.0;
        _modWheelValue = 0.0;
        _sampleRate = 44100.0;
        _bufferSize = 256;
        _isInitialized = false;

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

        _isInitialized = true;
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
        }
    }

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
                        _voiceManager.NoteOff(midiEvent.Data1);
                        Logger.Trace("MIDI", $"NoteOff (vel0) note={midiEvent.Data1}");
                    }
                    else
                    {
                        _voiceManager.NoteOn(midiEvent.Data1, midiEvent.Data2);
                        Logger.Trace("MIDI", $"NoteOn  note={midiEvent.Data1} vel={midiEvent.Data2}");
                    }
                    break;

                case MidiEvent.EventType.NoteOff:
                    _voiceManager.NoteOff(midiEvent.Data1);
                    Logger.Trace("MIDI", $"NoteOff note={midiEvent.Data1}");
                    break;

                case MidiEvent.EventType.PitchBend:
                    _pitchBendSemitones = MathUtils.PitchBendToSemitones(
                        midiEvent.PitchBendValue, _pitchBendRange);
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
        _pitchBendSemitones = 0.0;
        _modWheelValue = 0.0;
    }

    private void LoadDefaultWavetables()
    {
        _wavetables["Sine"] = BasicWavetables.GenerateSine();
        _wavetables["Saw"] = BasicWavetables.GenerateSaw(sampleRate: _sampleRate);
        _wavetables["Square"] = BasicWavetables.GenerateSquare(sampleRate: _sampleRate);
        _wavetables["Triangle"] = BasicWavetables.GenerateTriangle(sampleRate: _sampleRate);
    }

    private void HandleControlChange(byte ccNumber, byte value)
    {
        switch (ccNumber)
        {
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

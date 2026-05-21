using System;
using System.Collections.Generic;
using AudioPlugSharp;
using OttoSynth.Core;
using OttoSynth.Core.Midi;

namespace OttoSynth.Plugin;

/// <summary>
/// AudioPlugSharp entry point for OttoSynth.
/// Inherits AudioPluginBase and exposes the synthesizer engine as a VST3 plugin.
/// </summary>
public class OttoSynthPlugin : AudioPluginBase
{
    // ─── Engine ──────────────────────────────────────────────────
    private readonly SynthEngine _engine;
    private readonly Dictionary<int, OttoParameter> _parametersById = new();

    public SynthEngine Engine => _engine;

    public OttoSynthPlugin()
    {
        _engine = new SynthEngine(maxVoices: 16, maxBufferSize: 2048);

        // Set plugin metadata
        Company = "OttoSynth";
        Website = "https://github.com/ottosynth";
        Contact = "ottosynth@example.com";
        PluginName = "OttoSynth";
        PluginCategory = "Instrument|Synth";
        PluginVersion = "1.0.0";
        PluginID = 0x0A7C8ED1C2464B7AUL;
        HasUserInterface = false;
    }

    public override void Initialize()
    {
        base.Initialize();

        // I/O ports: stereo out (instrument plugin)
        InputPorts = new AudioIOPort[] { };
        OutputPorts = new AudioIOPort[]
        {
            new AudioIOPortManaged("Stereo Output", EAudioChannelConfiguration.Stereo)
        };

        // Build parameter list
        AddParam(OttoParameterId.MasterVolume, "Master Volume", 0.0, 1.0, 0.8);
        AddParam(OttoParameterId.FilterCutoff, "Filter Cutoff", 20.0, 20000.0, 20000.0, rangePower: 4.0);
        AddParam(OttoParameterId.FilterResonance, "Filter Resonance", 0.0, 1.0, 0.0);
        AddParam(OttoParameterId.FilterDrive, "Filter Drive", 0.0, 1.0, 0.0);
        AddParam(OttoParameterId.Attack, "Amp Attack", 0.001, 10.0, 0.01, rangePower: 3.0);
        AddParam(OttoParameterId.Decay, "Amp Decay", 0.001, 10.0, 0.3, rangePower: 3.0);
        AddParam(OttoParameterId.Sustain, "Amp Sustain", 0.0, 1.0, 0.7);
        AddParam(OttoParameterId.Release, "Amp Release", 0.001, 10.0, 0.5, rangePower: 3.0);

        AddParam(OttoParameterId.Osc1Level, "OSC1 Level", 0.0, 1.0, 0.8);
        AddParam(OttoParameterId.Osc2Level, "OSC2 Level", 0.0, 1.0, 0.0);
        AddParam(OttoParameterId.Osc3Level, "OSC3 Level", 0.0, 1.0, 0.0);
        AddParam(OttoParameterId.NoiseLevel, "Noise Level", 0.0, 1.0, 0.0);

        AddParam(OttoParameterId.Macro1, "Macro 1", 0.0, 1.0, 0.0);
        AddParam(OttoParameterId.Macro2, "Macro 2", 0.0, 1.0, 0.0);
        AddParam(OttoParameterId.Macro3, "Macro 3", 0.0, 1.0, 0.0);
        AddParam(OttoParameterId.Macro4, "Macro 4", 0.0, 1.0, 0.0);
    }

    private void AddParam(int id, string name, double min, double max, double def, double rangePower = 1.0)
    {
        var p = new OttoParameter
        {
            ID = id.ToString(),
            Name = name,
            MinValue = min,
            MaxValue = max,
            DefaultValue = def,
            RangePower = rangePower,
            EditValue = def,
            ProcessValue = def
        };
        AddParameter(p);
        _parametersById[id] = p;
    }

    public override void InitializeProcessing()
    {
        base.InitializeProcessing();
        _engine.Initialize(Host.SampleRate, (int)Host.MaxAudioBufferSize);
    }

    public override void SetMaxAudioBufferSize(uint maxSamples, EAudioBitsPerSample bitsPerSample)
    {
        base.SetMaxAudioBufferSize(maxSamples, bitsPerSample);
        _engine.Initialize(Host.SampleRate, (int)maxSamples);
    }

    public override void HandleNoteOn(int channel, int noteNumber, float velocity, int sampleOffset)
    {
        _engine.ProcessMidiEvent(MidiEvent.NoteOn((byte)noteNumber, (byte)(velocity * 127f)));
    }

    public override void HandleNoteOff(int channel, int noteNumber, float velocity, int sampleOffset)
    {
        _engine.ProcessMidiEvent(MidiEvent.NoteOff((byte)noteNumber, (byte)(velocity * 127f)));
    }

    public override void HandleParameterChange(AudioPluginParameter parameter, double newValue, int sampleOffset)
    {
        base.HandleParameterChange(parameter, newValue, sampleOffset);
        if (int.TryParse(parameter.ID, out int id))
            ApplyParameter(id, newValue);
    }

    private void ApplyParameter(int id, double v)
    {
        switch (id)
        {
            case OttoParameterId.MasterVolume: _engine.MasterVolume = v; break;
            case OttoParameterId.FilterCutoff:
                _engine.SetFilter(1, OttoSynth.Core.DSP.Filters.StateVariableFilter.FilterMode.LowPass,
                    v, GetParamValue(OttoParameterId.FilterResonance), GetParamValue(OttoParameterId.FilterDrive));
                break;
            case OttoParameterId.FilterResonance:
            case OttoParameterId.FilterDrive:
                _engine.SetFilter(1, OttoSynth.Core.DSP.Filters.StateVariableFilter.FilterMode.LowPass,
                    GetParamValue(OttoParameterId.FilterCutoff),
                    GetParamValue(OttoParameterId.FilterResonance),
                    GetParamValue(OttoParameterId.FilterDrive));
                break;
            case OttoParameterId.Attack:
            case OttoParameterId.Decay:
            case OttoParameterId.Sustain:
            case OttoParameterId.Release:
                _engine.SetEnvelope(
                    GetParamValue(OttoParameterId.Attack),
                    GetParamValue(OttoParameterId.Decay),
                    GetParamValue(OttoParameterId.Sustain),
                    GetParamValue(OttoParameterId.Release));
                break;
            case OttoParameterId.Osc1Level: _engine.SetOscillatorMix(1, v, v > 0.001); break;
            case OttoParameterId.Osc2Level: _engine.SetOscillatorMix(2, v, v > 0.001); break;
            case OttoParameterId.Osc3Level: _engine.SetOscillatorMix(3, v, v > 0.001); break;
            case OttoParameterId.NoiseLevel:
                foreach (var voice in _engine.VoiceManager.Voices)
                { voice.NoiseLevel = v; voice.NoiseEnabled = v > 0.001; }
                break;
            case OttoParameterId.Macro1: _engine.SetMacro(0, v); break;
            case OttoParameterId.Macro2: _engine.SetMacro(1, v); break;
            case OttoParameterId.Macro3: _engine.SetMacro(2, v); break;
            case OttoParameterId.Macro4: _engine.SetMacro(3, v); break;
        }
    }

    private double GetParamValue(int id)
        => _parametersById.TryGetValue(id, out var p) ? p.ProcessValue : 0.0;

    public override void Process()
    {
        // AudioPlugSharp passes the host event queue via base.Process().
        base.Process();

        // Output: pull from stereo output port
        var output = OutputPorts[0] as AudioIOPortManaged;
        if (output == null) return;

        var buffers = output.GetAudioBuffers();
        if (buffers == null || buffers.Length < 2) return;

        int sampleCount = (int)Host.CurrentAudioBufferSize;

        // Process audio (writes into temp buffers)
        _engine.ProcessAudio(buffers[0], buffers[1], sampleCount);
    }
}

/// <summary>
/// Parameter ID constants matching the order in OttoSynthPlugin.Initialize().
/// These map to VST3 parameter indices.
/// </summary>
internal static class OttoParameterId
{
    public const int MasterVolume = 1;
    public const int FilterCutoff = 10;
    public const int FilterResonance = 11;
    public const int FilterDrive = 12;
    public const int Attack = 20;
    public const int Decay = 21;
    public const int Sustain = 22;
    public const int Release = 23;
    public const int Osc1Level = 30;
    public const int Osc2Level = 31;
    public const int Osc3Level = 32;
    public const int NoiseLevel = 33;
    public const int Macro1 = 40;
    public const int Macro2 = 41;
    public const int Macro3 = 42;
    public const int Macro4 = 43;
}

/// <summary>Convenience subclass with sane default ValueFormat.</summary>
internal sealed class OttoParameter : AudioPluginParameter
{
    public OttoParameter()
    {
        ValueFormat = "F2";
    }
}

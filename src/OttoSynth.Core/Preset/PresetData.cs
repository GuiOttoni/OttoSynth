using System;
using System.Collections.Generic;

namespace OttoSynth.Core.Preset;

/// <summary>
/// Complete snapshot of all OttoSynth parameters.
/// Used for saving/loading presets to/from JSON files.
/// </summary>
public sealed class PresetData
{
    // ─── Metadata ───────────────────────────────────────────────
    public string Name { get; set; } = "Init";
    public string Author { get; set; } = "Factory";
    public string Category { get; set; } = "Init";
    public List<string> Tags { get; set; } = new();
    public string Description { get; set; } = "";
    public string Version { get; set; } = "1.0";
    public string CreatedDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");

    // ─── Global ─────────────────────────────────────────────────
    public double MasterVolume { get; set; } = 0.8;
    public double PitchBendRange { get; set; } = 2.0;
    public double GlideTime { get; set; } = 0.0;
    public string GlideMode { get; set; } = "Off";

    // ─── Oscillators (1-3) ──────────────────────────────────────
    public OscillatorData Osc1 { get; set; } = new();
    public OscillatorData Osc2 { get; set; } = new();
    public OscillatorData Osc3 { get; set; } = new();

    // ─── Noise ──────────────────────────────────────────────────
    public NoiseData Noise { get; set; } = new();

    // ─── Filters (1-2) ──────────────────────────────────────────
    public FilterData Filter1 { get; set; } = new();
    public FilterData Filter2 { get; set; } = new();
    public string FilterRouting { get; set; } = "Serial";
    public double FilterEnvAmount { get; set; } = 0.0;

    // ─── Envelopes ──────────────────────────────────────────────
    public EnvelopeData EnvAmp { get; set; } = new() { Attack = 0.01, Decay = 0.3, Sustain = 0.7, Release = 0.5 };
    public EnvelopeData EnvFilter { get; set; } = new() { Attack = 0.001, Decay = 0.5, Sustain = 0.3, Release = 0.3 };
    public EnvelopeData EnvFree { get; set; } = new() { Attack = 0.01, Decay = 0.3, Sustain = 0.7, Release = 0.5 };

    // ─── LFOs ───────────────────────────────────────────────────
    public LfoData Lfo1 { get; set; } = new();
    public LfoData Lfo2 { get; set; } = new();
    public LfoData Lfo3 { get; set; } = new();

    // ─── Modulation Matrix ──────────────────────────────────────
    public List<ModRouteData> ModRoutes { get; set; } = new();

    // ─── Macros ─────────────────────────────────────────────────
    public double[] Macros { get; set; } = new double[4];

    // ─── Effects ────────────────────────────────────────────────
    public List<EffectData> Effects { get; set; } = new();
}

public sealed class OscillatorData
{
    public string Wavetable { get; set; } = "Saw";
    public double Level { get; set; } = 0.8;
    public bool Enabled { get; set; } = true;
    public double Pan { get; set; } = 0.0;
    public int CoarseTune { get; set; } = 0;
    public double FineTune { get; set; } = 0.0;
    public double WavetablePosition { get; set; } = 0.0;
    public int UnisonVoices { get; set; } = 1;
    public double UnisonDetune { get; set; } = 0.3;
    public double UnisonSpread { get; set; } = 0.5;
}

public sealed class NoiseData
{
    public double Level { get; set; } = 0.0;
    public bool Enabled { get; set; } = false;
    public string Type { get; set; } = "White"; // White, Pink
}

public sealed class FilterData
{
    public string Mode { get; set; } = "LP"; // LP, HP, BP, Notch
    public double Cutoff { get; set; } = 20000.0;
    public double Resonance { get; set; } = 0.0;
    public double Drive { get; set; } = 0.0;
    public bool Is24dB { get; set; } = false;
    public double KeyTracking { get; set; } = 0.0;
}

public sealed class EnvelopeData
{
    public double Attack { get; set; } = 0.01;
    public double Decay { get; set; } = 0.3;
    public double Sustain { get; set; } = 0.7;
    public double Release { get; set; } = 0.5;
    public double AttackCurve { get; set; } = 0.0;
    public double DecayCurve { get; set; } = 0.0;
    public double ReleaseCurve { get; set; } = 0.0;
}

public sealed class LfoData
{
    public string Shape { get; set; } = "Sine"; // Sine, Triangle, SawUp, SawDown, Square, SampleHold
    public double Rate { get; set; } = 1.0;
    public double Depth { get; set; } = 1.0;
    public double Phase { get; set; } = 0.0;
    public bool Retrigger { get; set; } = true;
    public string Sync { get; set; } = "Free"; // Free, 1/1, 1/2, 1/4, ...
}

public sealed class ModRouteData
{
    public string Source { get; set; } = "None";
    public string Destination { get; set; } = "None";
    public double Amount { get; set; } = 0.0;
    public bool Active { get; set; } = true;
}

public sealed class EffectData
{
    public string Type { get; set; } = ""; // "Distortion", "Delay", "Reverb", etc.
    public bool Bypass { get; set; } = false;
    public double Mix { get; set; } = 1.0;
    /// <summary>Effect-specific parameters as a key/value dictionary.</summary>
    public Dictionary<string, double> Parameters { get; set; } = new();
    /// <summary>Effect-specific string parameters (e.g. Distortion type).</summary>
    public Dictionary<string, string> StringParameters { get; set; } = new();
}

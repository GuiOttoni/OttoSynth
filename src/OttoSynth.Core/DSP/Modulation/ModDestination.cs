namespace OttoSynth.Core.DSP.Modulation;

/// <summary>
/// All available modulation destinations in the synthesizer.
/// Each destination maps to a specific parameter that can be modulated.
/// </summary>
public enum ModDestination : byte
{
    None = 0,

    // ── Oscillator 1 ────────────────────────────────────────
    Osc1Pitch = 1,            // ±48 semitones
    Osc1WavetablePos = 2,     // 0..1
    Osc1Level = 3,            // 0..1
    Osc1Pan = 4,              // -1..+1

    // ── Oscillator 2 ────────────────────────────────────────
    Osc2Pitch = 10,
    Osc2WavetablePos = 11,
    Osc2Level = 12,
    Osc2Pan = 13,

    // ── Oscillator 3 ────────────────────────────────────────
    Osc3Pitch = 20,
    Osc3WavetablePos = 21,
    Osc3Level = 22,
    Osc3Pan = 23,

    // ── Noise ───────────────────────────────────────────────
    NoiseLevel = 30,

    // ── Filter 1 ────────────────────────────────────────────
    Filter1Cutoff = 40,       // 20..20000 Hz (log scale)
    Filter1Resonance = 41,    // 0..1
    Filter1Drive = 42,        // 0..1

    // ── Filter 2 ────────────────────────────────────────────
    Filter2Cutoff = 50,
    Filter2Resonance = 51,
    Filter2Drive = 52,

    // ── LFO ─────────────────────────────────────────────────
    Lfo1Rate = 60,            // 0.01..30 Hz
    Lfo1Depth = 61,           // 0..1
    Lfo2Rate = 62,
    Lfo2Depth = 63,
    Lfo3Rate = 64,
    Lfo3Depth = 65,

    // ── Global ──────────────────────────────────────────────
    MasterVolume = 70,        // 0..1
}

/// <summary>
/// Metadata about a modulation destination: its range and default value.
/// Used by the ModMatrix to clamp modulated values.
/// </summary>
public readonly struct ModDestinationInfo
{
    public readonly double MinValue;
    public readonly double MaxValue;
    public readonly double DefaultValue;
    public readonly bool IsLogarithmic; // Filter cutoff uses log scaling

    public ModDestinationInfo(double min, double max, double defaultValue, bool isLog = false)
    {
        MinValue = min;
        MaxValue = max;
        DefaultValue = defaultValue;
        IsLogarithmic = isLog;
    }

    /// <summary>Gets the info for a given destination.</summary>
    public static ModDestinationInfo GetInfo(ModDestination dest) => dest switch
    {
        // Oscillator pitch: bipolar semitone offset
        ModDestination.Osc1Pitch or ModDestination.Osc2Pitch or ModDestination.Osc3Pitch
            => new(-48.0, 48.0, 0.0),

        // Wavetable position: 0..1
        ModDestination.Osc1WavetablePos or ModDestination.Osc2WavetablePos or ModDestination.Osc3WavetablePos
            => new(0.0, 1.0, 0.0),

        // Oscillator/noise level: 0..1
        ModDestination.Osc1Level or ModDestination.Osc2Level or ModDestination.Osc3Level or ModDestination.NoiseLevel
            => new(0.0, 1.0, 0.8),

        // Pan: -1..+1
        ModDestination.Osc1Pan or ModDestination.Osc2Pan or ModDestination.Osc3Pan
            => new(-1.0, 1.0, 0.0),

        // Filter cutoff: 20..20000 Hz (log)
        ModDestination.Filter1Cutoff or ModDestination.Filter2Cutoff
            => new(20.0, 20000.0, 20000.0, isLog: true),

        // Filter resonance: 0..1
        ModDestination.Filter1Resonance or ModDestination.Filter2Resonance
            => new(0.0, 1.0, 0.0),

        // Filter drive: 0..1
        ModDestination.Filter1Drive or ModDestination.Filter2Drive
            => new(0.0, 1.0, 0.0),

        // LFO rate: 0.01..30 Hz
        ModDestination.Lfo1Rate or ModDestination.Lfo2Rate or ModDestination.Lfo3Rate
            => new(0.01, 30.0, 1.0),

        // LFO depth: 0..1
        ModDestination.Lfo1Depth or ModDestination.Lfo2Depth or ModDestination.Lfo3Depth
            => new(0.0, 1.0, 1.0),

        // Master volume: 0..1
        ModDestination.MasterVolume => new(0.0, 1.0, 0.8),

        _ => new(0.0, 1.0, 0.0),
    };
}

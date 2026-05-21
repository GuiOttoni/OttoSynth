using System.Collections.Generic;

namespace OttoSynth.Core.Preset;

/// <summary>
/// Built-in factory presets that showcase the synthesizer's capabilities.
/// These can be installed by the host application on first run.
/// </summary>
public static class FactoryPresets
{
    /// <summary>Returns the initial "blank" preset.</summary>
    public static PresetData Init()
    {
        var p = new PresetData
        {
            Name = "Init",
            Category = "Init",
            Author = "Factory",
            Description = "Default initialization preset",
            Tags = new List<string> { "init", "default" }
        };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.8;
        p.Osc2.Enabled = false;
        p.Osc3.Enabled = false;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 20000.0;
        return p;
    }

    /// <summary>Returns all built-in factory presets.</summary>
    public static IReadOnlyList<PresetData> All()
    {
        return new List<PresetData>
        {
            Init(),

            // ── BASS ──
            BuildBass("Sub Bass", 0.0, 250, 0.0),
            BuildBass("Reese", 0.5, 800, 0.4),
            BuildBass("Wobble", 0.3, 1200, 0.7),
            BuildBass("Acid", 0.7, 1500, 0.85),
            BuildBass("FM Bass", 0.0, 600, 0.5),
            BuildBass("Pluck Bass", 0.0, 900, 0.3),
            BuildBass("Saw Bass", 0.0, 1400, 0.2),
            BuildBass("Square Bass", 0.0, 1100, 0.1),

            // ── LEAD ──
            BuildLead("Supersaw", "Saw", 1500, 0.4),
            BuildLead("Trance Lead", "Saw", 3000, 0.6),
            BuildLead("Pluck Lead", "Triangle", 4000, 0.2),
            BuildLead("Detuned Lead", "Saw", 2500, 0.5),
            BuildLead("Square Lead", "Square", 3500, 0.3),
            BuildLead("Vintage Lead", "Saw", 5000, 0.7),
            BuildLead("Chip Lead", "Square", 8000, 0.0),
            BuildLead("Soft Lead", "Sine", 6000, 0.0),

            // ── PAD ──
            BuildPad("Warm Pad", "Saw", 2000, 1.5),
            BuildPad("Evolving Pad", "Triangle", 3000, 2.5),
            BuildPad("Choir Pad", "Sine", 4000, 1.8),
            BuildPad("Shimmer Pad", "Triangle", 5000, 3.0),
            BuildPad("Strings Pad", "Saw", 2500, 1.2),
            BuildPad("Dark Pad", "Saw", 800, 2.0),
            BuildPad("Bright Pad", "Square", 6000, 1.0),
            BuildPad("Glass Pad", "Sine", 8000, 2.2),

            // ── PLUCK ──
            BuildPluck("Bell", "Sine", 8000),
            BuildPluck("Marimba", "Sine", 4000),
            BuildPluck("Kalimba", "Triangle", 6000),
            BuildPluck("Pizzicato", "Saw", 5000),
            BuildPluck("Mute Pluck", "Triangle", 2500),
            BuildPluck("Bright Pluck", "Saw", 9000),

            // ── KEYS ──
            BuildKeys("Electric Piano", "Sine", 5000),
            BuildKeys("Organ", "Square", 8000),
            BuildKeys("Clav", "Saw", 6000),
            BuildKeys("Rhodes", "Triangle", 4500),
            BuildKeys("Wurli", "Triangle", 5500),

            // ── FX ──
            BuildFx("Riser", "Saw", true),
            BuildFx("Sweep", "Saw", false),
            BuildFx("Noise Texture", "Sine", false),
            BuildFx("Impact", "Square", false),
            BuildFx("Drone", "Saw", false),

            // ── STRINGS ──
            BuildStrings("Analog Strings", 2.0),
            BuildStrings("Cinematic Strings", 3.5),
            BuildStrings("Slow Attack Strings", 4.0),
            BuildStrings("Solo Strings", 1.5),
            BuildStrings("Ensemble Strings", 2.5),

            // ── AMBIENT ──
            BuildAmbient("Atmosphere", 5.0),
            BuildAmbient("Ethereal", 6.0),
            BuildAmbient("Texture", 4.0),
            BuildAmbient("Drone Pad", 7.0),
            BuildAmbient("Deep Space", 8.0),
        };
    }

    private static PresetData BuildBass(string name, double position, double cutoff, double resonance)
    {
        var p = new PresetData { Name = name, Category = "Bass" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.85;
        p.Osc1.CoarseTune = -12;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = cutoff;
        p.Filter1.Resonance = resonance;
        p.Filter1.Is24dB = true;
        p.EnvAmp.Attack = 0.001;
        p.EnvAmp.Decay = 0.2;
        p.EnvAmp.Sustain = 0.85;
        p.EnvAmp.Release = 0.1;
        p.EnvFilter.Attack = 0.001;
        p.EnvFilter.Decay = 0.3;
        p.EnvFilter.Sustain = 0.4;
        p.EnvFilter.Release = 0.1;
        p.FilterEnvAmount = 0.6;
        return p;
    }

    private static PresetData BuildLead(string name, string waveform, double cutoff, double resonance)
    {
        var p = new PresetData { Name = name, Category = "Lead" };
        p.Osc1.Wavetable = waveform;
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.7;
        p.Osc2.Wavetable = waveform;
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.6;
        p.Osc2.FineTune = 7.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = cutoff;
        p.Filter1.Resonance = resonance;
        p.EnvAmp.Attack = 0.005;
        p.EnvAmp.Decay = 0.2;
        p.EnvAmp.Sustain = 0.8;
        p.EnvAmp.Release = 0.3;
        return p;
    }

    private static PresetData BuildPad(string name, string waveform, double cutoff, double attack)
    {
        var p = new PresetData { Name = name, Category = "Pad" };
        p.Osc1.Wavetable = waveform;
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.7;
        p.Osc2.Wavetable = waveform;
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.6;
        p.Osc2.FineTune = 3.0;
        p.Osc3.Wavetable = waveform;
        p.Osc3.Enabled = true;
        p.Osc3.Level = 0.5;
        p.Osc3.FineTune = -4.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = cutoff;
        p.EnvAmp.Attack = attack;
        p.EnvAmp.Decay = 0.5;
        p.EnvAmp.Sustain = 0.85;
        p.EnvAmp.Release = 3.5;
        return p;
    }

    private static PresetData BuildPluck(string name, string waveform, double cutoff)
    {
        var p = new PresetData { Name = name, Category = "Pluck" };
        p.Osc1.Wavetable = waveform;
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.8;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = cutoff;
        p.EnvAmp.Attack = 0.001;
        p.EnvAmp.Decay = 0.4;
        p.EnvAmp.Sustain = 0.0;
        p.EnvAmp.Release = 0.5;
        return p;
    }

    private static PresetData BuildKeys(string name, string waveform, double cutoff)
    {
        var p = new PresetData { Name = name, Category = "Keys" };
        p.Osc1.Wavetable = waveform;
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.8;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = cutoff;
        p.EnvAmp.Attack = 0.005;
        p.EnvAmp.Decay = 1.0;
        p.EnvAmp.Sustain = 0.4;
        p.EnvAmp.Release = 0.6;
        return p;
    }

    private static PresetData BuildFx(string name, string waveform, bool sweepUp)
    {
        var p = new PresetData { Name = name, Category = "FX" };
        p.Osc1.Wavetable = waveform;
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.7;
        p.Noise.Enabled = true;
        p.Noise.Level = 0.4;
        p.Filter1.Mode = sweepUp ? "HighPass" : "LowPass";
        p.Filter1.Cutoff = sweepUp ? 100.0 : 8000.0;
        p.Filter1.Resonance = 0.5;
        p.EnvAmp.Attack = 1.0;
        p.EnvAmp.Decay = 2.0;
        p.EnvAmp.Sustain = 0.5;
        p.EnvAmp.Release = 2.0;
        return p;
    }

    private static PresetData BuildStrings(string name, double attack)
    {
        var p = new PresetData { Name = name, Category = "Strings" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.7;
        p.Osc2.Wavetable = "Saw";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.6;
        p.Osc2.FineTune = 5.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 4000.0;
        p.EnvAmp.Attack = attack;
        p.EnvAmp.Decay = 0.5;
        p.EnvAmp.Sustain = 0.8;
        p.EnvAmp.Release = 1.5;
        return p;
    }

    private static PresetData BuildAmbient(string name, double release)
    {
        var p = new PresetData { Name = name, Category = "Ambient" };
        p.Osc1.Wavetable = "Sine";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.7;
        p.Osc2.Wavetable = "Triangle";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.5;
        p.Osc2.FineTune = 7.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 3000.0;
        p.EnvAmp.Attack = 3.0;
        p.EnvAmp.Decay = 1.0;
        p.EnvAmp.Sustain = 0.7;
        p.EnvAmp.Release = release;
        return p;
    }
}

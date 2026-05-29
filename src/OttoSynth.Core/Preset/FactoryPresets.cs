using System.Collections.Generic;

namespace OttoSynth.Core.Preset;

public static class FactoryPresets
{
    // ── Effect/route helpers ─────────────────────────────────────

    private static ModRouteData Route(string src, string dst, double amount) =>
        new() { Source = src, Destination = dst, Amount = amount, Active = true };

    private static EffectData Reverb(double mix, double size = 0.7, double decay = 0.8,
        double damping = 0.5, double width = 0.8) =>
        new() { Type = "Reverb", Mix = mix, Parameters = new()
            { ["Size"] = size, ["Decay"] = decay, ["Damping"] = damping,
              ["PreDelay"] = 0.02, ["Width"] = width } };

    private static EffectData Chorus(double mix, double rate = 0.5, double depth = 0.3,
        double feedback = 0.2) =>
        new() { Type = "Chorus", Mix = mix, Parameters = new()
            { ["Rate"] = rate, ["Depth"] = depth, ["Feedback"] = feedback } };

    private static EffectData Delay(double mix, double timeLeft = 0.375, double timeRight = 0.5,
        double feedback = 0.35, bool pingPong = false, double damping = 0.3) =>
        new() { Type = "Delay", Mix = mix, Parameters = new()
            { ["TimeLeft"] = timeLeft, ["TimeRight"] = timeRight, ["Feedback"] = feedback,
              ["PingPong"] = pingPong ? 1.0 : 0.0, ["Damping"] = damping } };

    private static EffectData Overdrive(double mix, double drive = 0.4, double outputGain = 0.8) =>
        new() { Type = "Distortion", Mix = mix,
            Parameters = new() { ["Drive"] = drive, ["OutputGain"] = outputGain },
            StringParameters = new() { ["Type"] = "Overdrive" } };

    // ── Init ─────────────────────────────────────────────────────

    public static PresetData Init()
    {
        var p = new PresetData
        {
            Name = "Init", Category = "Init", Author = "Factory",
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

    public static IReadOnlyList<PresetData> All() => new List<PresetData>
    {
        Init(),

        // ── BASS ──
        BuildBass_SubBass(),
        BuildBass_Reese(),
        BuildBass_Wobble(),
        BuildBass_Acid(),
        BuildBass_FMBass(),
        BuildBass_PluckBass(),
        BuildBass_SawBass(),
        BuildBass_SquareBass(),

        // ── LEAD ──
        BuildLead_Supersaw(),
        BuildLead_TranceLead(),
        BuildLead_PluckLead(),
        BuildLead_DetunedLead(),
        BuildLead_SquareLead(),
        BuildLead_VintageLead(),
        BuildLead_ChipLead(),
        BuildLead_SoftLead(),

        // ── SYNTH ──
        BuildSynth_JpPoly(),
        BuildSynth_ObBrass(),
        BuildSynth_MoogStyle(),
        BuildSynth_PwmSweep(),
        BuildSynth_DetuneCloud(),
        BuildSynth_TranceStab(),
        BuildSynth_ClassicPoly(),

        // ── PAD ──
        BuildPad_WarmPad(),
        BuildPad_EvolvingPad(),
        BuildPad_ChoirPad(),
        BuildPad_ShimmerPad(),
        BuildPad_StringsPad(),
        BuildPad_DarkPad(),
        BuildPad_BrightPad(),
        BuildPad_GlassPad(),

        // ── PLUCK ──
        BuildPluck_Bell(),
        BuildPluck_Marimba(),
        BuildPluck_Kalimba(),
        BuildPluck_Pizzicato(),
        BuildPluck_MutePluck(),
        BuildPluck_BrightPluck(),

        // ── KEYS ──
        BuildKeys_ElectricPiano(),
        BuildKeys_Organ(),
        BuildKeys_Clav(),
        BuildKeys_Rhodes(),
        BuildKeys_Wurli(),

        // ── FX ──
        BuildFx_Riser(),
        BuildFx_Sweep(),
        BuildFx_NoiseTexture(),
        BuildFx_Impact(),
        BuildFx_Drone(),

        // ── STRINGS ──
        BuildStrings_Analog(),
        BuildStrings_Cinematic(),
        BuildStrings_SlowAttack(),
        BuildStrings_Solo(),
        BuildStrings_Ensemble(),

        // ── AMBIENT ──
        BuildAmbient_Atmosphere(),
        BuildAmbient_Ethereal(),
        BuildAmbient_Texture(),
        BuildAmbient_DronePad(),
        BuildAmbient_DeepSpace(),
    };

    // ── BASS ────────────────────────────────────────────────────

    private static PresetData BuildBass_SubBass()
    {
        var p = new PresetData { Name = "Sub Bass", Category = "Bass" };
        p.Osc1.Wavetable = "Sine";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.9;
        p.Osc1.CoarseTune = -12;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 300;
        p.Filter1.Resonance = 0.1;
        p.Filter1.Is24dB = true;
        p.EnvAmp.Attack = 0.003;
        p.EnvAmp.Decay = 0.15;
        p.EnvAmp.Sustain = 0.9;
        p.EnvAmp.Release = 0.15;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 0.5;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo1", "Osc1Level", 0.04));
        p.Effects.Add(Reverb(0.1, size: 0.3, decay: 0.4, damping: 0.8));
        return p;
    }

    private static PresetData BuildBass_Reese()
    {
        var p = new PresetData { Name = "Reese", Category = "Bass" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.8;
        p.Osc1.CoarseTune = -12;
        p.Osc2.Wavetable = "Saw";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.75;
        p.Osc2.CoarseTune = -12;
        p.Osc2.FineTune = -14.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 800;
        p.Filter1.Resonance = 0.4;
        p.Filter1.Is24dB = true;
        p.EnvAmp.Attack = 0.002;
        p.EnvAmp.Decay = 0.2;
        p.EnvAmp.Sustain = 0.85;
        p.EnvAmp.Release = 0.15;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 0.8;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo1", "Filter1Cutoff", 0.2));
        p.Effects.Add(Chorus(0.3, rate: 0.3, depth: 0.15));
        return p;
    }

    private static PresetData BuildBass_Wobble()
    {
        var p = new PresetData { Name = "Wobble", Category = "Bass" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.85;
        p.Osc1.CoarseTune = -12;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 200;
        p.Filter1.Resonance = 0.75;
        p.Filter1.Is24dB = true;
        p.EnvAmp.Attack = 0.001;
        p.EnvAmp.Decay = 0.2;
        p.EnvAmp.Sustain = 0.9;
        p.EnvAmp.Release = 0.1;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 2.0;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo1", "Filter1Cutoff", 0.6));
        p.ModRoutes.Add(Route("ModWheel", "Lfo1Rate", 0.5));
        p.Effects.Add(Delay(0.2, timeLeft: 0.25, timeRight: 0.25, feedback: 0.3, pingPong: true));
        return p;
    }

    private static PresetData BuildBass_Acid()
    {
        var p = new PresetData { Name = "Acid", Category = "Bass" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.9;
        p.Osc1.CoarseTune = -12;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 400;
        p.Filter1.Resonance = 0.85;
        p.Filter1.Is24dB = true;
        p.EnvAmp.Attack = 0.001;
        p.EnvAmp.Decay = 0.15;
        p.EnvAmp.Sustain = 0.7;
        p.EnvAmp.Release = 0.08;
        p.EnvFilter.Attack = 0.001;
        p.EnvFilter.Decay = 0.25;
        p.EnvFilter.Sustain = 0.1;
        p.EnvFilter.Release = 0.1;
        p.FilterEnvAmount = 0.8;
        p.GlideTime = 0.05;
        p.GlideMode = "Legato";
        p.Effects.Add(Delay(0.25, timeLeft: 0.375, timeRight: 0.375, feedback: 0.4));
        p.Effects.Add(Overdrive(0.15, drive: 0.3));
        return p;
    }

    private static PresetData BuildBass_FMBass()
    {
        var p = new PresetData { Name = "FM Bass", Category = "Bass" };
        p.Osc1.Wavetable = "Sine";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.9;
        p.Osc1.CoarseTune = -12;
        p.Osc2.Wavetable = "Sine";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.6;
        p.Osc2.CoarseTune = -12;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 1800;
        p.Filter1.Resonance = 0.3;
        p.EnvAmp.Attack = 0.001;
        p.EnvAmp.Decay = 0.4;
        p.EnvAmp.Sustain = 0.5;
        p.EnvAmp.Release = 0.2;
        p.EnvFilter.Attack = 0.001;
        p.EnvFilter.Decay = 0.3;
        p.EnvFilter.Sustain = 0.0;
        p.EnvFilter.Release = 0.2;
        p.FilterEnvAmount = 0.7;
        p.ModRoutes.Add(Route("Velocity", "Filter1Cutoff", 0.3));
        return p;
    }

    private static PresetData BuildBass_PluckBass()
    {
        var p = new PresetData { Name = "Pluck Bass", Category = "Bass" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.85;
        p.Osc1.CoarseTune = -12;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 3500;
        p.Filter1.Resonance = 0.35;
        p.Filter1.Is24dB = true;
        p.EnvAmp.Attack = 0.001;
        p.EnvAmp.Decay = 0.35;
        p.EnvAmp.Sustain = 0.0;
        p.EnvAmp.Release = 0.3;
        p.EnvFilter.Attack = 0.001;
        p.EnvFilter.Decay = 0.25;
        p.EnvFilter.Sustain = 0.0;
        p.EnvFilter.Release = 0.2;
        p.FilterEnvAmount = 0.6;
        p.ModRoutes.Add(Route("Velocity", "Osc1Level", 0.25));
        p.Effects.Add(Reverb(0.15, size: 0.3, decay: 0.3));
        return p;
    }

    private static PresetData BuildBass_SawBass()
    {
        var p = new PresetData { Name = "Saw Bass", Category = "Bass" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.85;
        p.Osc1.CoarseTune = -12;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 1400;
        p.Filter1.Resonance = 0.2;
        p.Filter1.Is24dB = true;
        p.EnvAmp.Attack = 0.003;
        p.EnvAmp.Decay = 0.2;
        p.EnvAmp.Sustain = 0.85;
        p.EnvAmp.Release = 0.1;
        p.ModRoutes.Add(Route("Velocity", "Filter1Cutoff", 0.2));
        return p;
    }

    private static PresetData BuildBass_SquareBass()
    {
        var p = new PresetData { Name = "Square Bass", Category = "Bass" };
        p.Osc1.Wavetable = "Square";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.8;
        p.Osc1.CoarseTune = -12;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 1100;
        p.Filter1.Resonance = 0.1;
        p.EnvAmp.Attack = 0.003;
        p.EnvAmp.Decay = 0.25;
        p.EnvAmp.Sustain = 0.8;
        p.EnvAmp.Release = 0.12;
        p.Effects.Add(Chorus(0.2, rate: 0.4, depth: 0.15));
        return p;
    }

    // ── LEAD ────────────────────────────────────────────────────

    private static PresetData BuildLead_Supersaw()
    {
        var p = new PresetData { Name = "Supersaw", Category = "Lead" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.75;
        p.Osc1.UnisonVoices = 7;
        p.Osc1.UnisonDetune = 0.4;
        p.Osc1.UnisonSpread = 0.8;
        p.Osc2.Wavetable = "Saw";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.6;
        p.Osc2.FineTune = 5.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 5000;
        p.Filter1.Resonance = 0.3;
        p.EnvAmp.Attack = 0.01;
        p.EnvAmp.Decay = 0.2;
        p.EnvAmp.Sustain = 0.85;
        p.EnvAmp.Release = 0.5;
        p.Effects.Add(Chorus(0.4, rate: 0.4, depth: 0.25));
        p.Effects.Add(Reverb(0.25, size: 0.5, decay: 0.6));
        return p;
    }

    private static PresetData BuildLead_TranceLead()
    {
        var p = new PresetData { Name = "Trance Lead", Category = "Lead" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.8;
        p.Osc2.Wavetable = "Saw";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.7;
        p.Osc2.FineTune = 7.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 6000;
        p.Filter1.Resonance = 0.5;
        p.EnvAmp.Attack = 0.005;
        p.EnvAmp.Decay = 0.15;
        p.EnvAmp.Sustain = 0.9;
        p.EnvAmp.Release = 0.3;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 5.0;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("ModWheel", "Lfo1Depth", 0.8));
        p.ModRoutes.Add(Route("Lfo1", "Osc1Pitch", 0.1));
        p.Effects.Add(Delay(0.3, timeLeft: 0.375, timeRight: 0.5, feedback: 0.4, pingPong: true));
        p.Effects.Add(Reverb(0.2, size: 0.4, decay: 0.5));
        return p;
    }

    private static PresetData BuildLead_PluckLead()
    {
        var p = new PresetData { Name = "Pluck Lead", Category = "Lead" };
        p.Osc1.Wavetable = "Triangle";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.8;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 6000;
        p.Filter1.Resonance = 0.4;
        p.EnvAmp.Attack = 0.001;
        p.EnvAmp.Decay = 0.3;
        p.EnvAmp.Sustain = 0.2;
        p.EnvAmp.Release = 0.4;
        p.EnvFilter.Attack = 0.001;
        p.EnvFilter.Decay = 0.25;
        p.EnvFilter.Sustain = 0.0;
        p.EnvFilter.Release = 0.2;
        p.FilterEnvAmount = 0.5;
        p.Effects.Add(Reverb(0.3, size: 0.5, decay: 0.6));
        p.Effects.Add(Delay(0.2, timeLeft: 0.375, timeRight: 0.375, feedback: 0.3));
        return p;
    }

    private static PresetData BuildLead_DetunedLead()
    {
        var p = new PresetData { Name = "Detuned Lead", Category = "Lead" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.75;
        p.Osc1.UnisonVoices = 3;
        p.Osc1.UnisonDetune = 0.35;
        p.Osc1.UnisonSpread = 0.6;
        p.Osc2.Wavetable = "Saw";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.65;
        p.Osc2.FineTune = -8.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 4500;
        p.Filter1.Resonance = 0.4;
        p.EnvAmp.Attack = 0.008;
        p.EnvAmp.Decay = 0.2;
        p.EnvAmp.Sustain = 0.8;
        p.EnvAmp.Release = 0.4;
        p.Effects.Add(Chorus(0.35, rate: 0.5, depth: 0.2));
        p.Effects.Add(Reverb(0.2, size: 0.4, decay: 0.5));
        return p;
    }

    private static PresetData BuildLead_SquareLead()
    {
        var p = new PresetData { Name = "Square Lead", Category = "Lead" };
        p.Osc1.Wavetable = "Square";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.75;
        p.Osc2.Wavetable = "Square";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.6;
        p.Osc2.CoarseTune = 12;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 5000;
        p.Filter1.Resonance = 0.3;
        p.EnvAmp.Attack = 0.005;
        p.EnvAmp.Decay = 0.15;
        p.EnvAmp.Sustain = 0.85;
        p.EnvAmp.Release = 0.3;
        p.Effects.Add(Delay(0.25, timeLeft: 0.375, timeRight: 0.375, feedback: 0.35));
        return p;
    }

    private static PresetData BuildLead_VintageLead()
    {
        var p = new PresetData { Name = "Vintage Lead", Category = "Lead" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.8;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 5000;
        p.Filter1.Resonance = 0.6;
        p.EnvAmp.Attack = 0.01;
        p.EnvAmp.Decay = 0.2;
        p.EnvAmp.Sustain = 0.75;
        p.EnvAmp.Release = 0.4;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 5.5;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("ModWheel", "Lfo1Depth", 1.0));
        p.ModRoutes.Add(Route("Lfo1", "Osc1Pitch", 0.08));
        p.Effects.Add(Reverb(0.3, size: 0.55, decay: 0.6, damping: 0.4));
        p.Effects.Add(Chorus(0.2, rate: 0.3, depth: 0.2));
        return p;
    }

    private static PresetData BuildLead_ChipLead()
    {
        var p = new PresetData { Name = "Chip Lead", Category = "Lead" };
        p.Osc1.Wavetable = "Square";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.8;
        p.Osc2.Wavetable = "Square";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.5;
        p.Osc2.CoarseTune = 12;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 8000;
        p.Filter1.Resonance = 0.0;
        p.EnvAmp.Attack = 0.001;
        p.EnvAmp.Decay = 0.1;
        p.EnvAmp.Sustain = 0.9;
        p.EnvAmp.Release = 0.1;
        p.Effects.Add(new EffectData { Type = "BitCrusher", Mix = 0.8,
            Parameters = new() { ["BitDepth"] = 8.0, ["SampleRateDiv"] = 4.0 } });
        p.Effects.Add(Delay(0.3, timeLeft: 0.1875, timeRight: 0.25, feedback: 0.5));
        return p;
    }

    private static PresetData BuildLead_SoftLead()
    {
        var p = new PresetData { Name = "Soft Lead", Category = "Lead" };
        p.Osc1.Wavetable = "Sine";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.8;
        p.Osc2.Wavetable = "Triangle";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.4;
        p.Osc2.FineTune = 5.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 6000;
        p.Filter1.Resonance = 0.0;
        p.EnvAmp.Attack = 0.02;
        p.EnvAmp.Decay = 0.3;
        p.EnvAmp.Sustain = 0.8;
        p.EnvAmp.Release = 0.5;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 5.0;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("ModWheel", "Lfo1Depth", 0.8));
        p.ModRoutes.Add(Route("Lfo1", "Osc1Pitch", 0.07));
        p.Effects.Add(Chorus(0.35, rate: 0.4, depth: 0.2));
        p.Effects.Add(Reverb(0.4, size: 0.6, decay: 0.7));
        return p;
    }

    // ── SYNTH ───────────────────────────────────────────────────

    private static PresetData BuildSynth_JpPoly()
    {
        var p = new PresetData { Name = "JP Poly", Category = "Synth" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.75;
        p.Osc1.UnisonVoices = 3;
        p.Osc1.UnisonDetune = 0.25;
        p.Osc2.Wavetable = "Square";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.6;
        p.Osc2.FineTune = 3.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 3500;
        p.Filter1.Resonance = 0.35;
        p.EnvAmp.Attack = 0.01;
        p.EnvAmp.Decay = 0.4;
        p.EnvAmp.Sustain = 0.75;
        p.EnvAmp.Release = 0.8;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 0.4;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo1", "Filter1Cutoff", 0.15));
        p.Effects.Add(Chorus(0.5, rate: 0.6, depth: 0.3));
        p.Effects.Add(Reverb(0.3, size: 0.6, decay: 0.65, width: 0.9));
        return p;
    }

    private static PresetData BuildSynth_ObBrass()
    {
        var p = new PresetData { Name = "OB Brass", Category = "Synth" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.8;
        p.Osc1.UnisonVoices = 2;
        p.Osc1.UnisonDetune = 0.2;
        p.Osc2.Wavetable = "Saw";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.7;
        p.Osc2.FineTune = -5.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 2000;
        p.Filter1.Resonance = 0.5;
        p.EnvAmp.Attack = 0.02;
        p.EnvAmp.Decay = 0.3;
        p.EnvAmp.Sustain = 0.7;
        p.EnvAmp.Release = 0.4;
        p.EnvFilter.Attack = 0.02;
        p.EnvFilter.Decay = 0.35;
        p.EnvFilter.Sustain = 0.5;
        p.EnvFilter.Release = 0.4;
        p.FilterEnvAmount = 0.45;
        p.Effects.Add(Reverb(0.25, size: 0.5, decay: 0.55));
        return p;
    }

    private static PresetData BuildSynth_MoogStyle()
    {
        var p = new PresetData { Name = "Moog Style", Category = "Synth" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.85;
        p.Osc2.Wavetable = "Square";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.5;
        p.Osc2.CoarseTune = -12;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 1200;
        p.Filter1.Resonance = 0.65;
        p.Filter1.Is24dB = true;
        p.EnvAmp.Attack = 0.005;
        p.EnvAmp.Decay = 0.3;
        p.EnvAmp.Sustain = 0.7;
        p.EnvAmp.Release = 0.4;
        p.EnvFilter.Attack = 0.003;
        p.EnvFilter.Decay = 0.5;
        p.EnvFilter.Sustain = 0.3;
        p.EnvFilter.Release = 0.3;
        p.FilterEnvAmount = 0.55;
        p.ModRoutes.Add(Route("Velocity", "Filter1Cutoff", 0.3));
        p.GlideTime = 0.08;
        p.GlideMode = "Legato";
        p.Effects.Add(Delay(0.2, timeLeft: 0.375, timeRight: 0.5, feedback: 0.35));
        return p;
    }

    private static PresetData BuildSynth_PwmSweep()
    {
        var p = new PresetData { Name = "PWM Sweep", Category = "Synth" };
        p.Osc1.Wavetable = "Square";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.8;
        p.Osc1.UnisonVoices = 2;
        p.Osc1.UnisonDetune = 0.15;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 4000;
        p.Filter1.Resonance = 0.2;
        p.EnvAmp.Attack = 0.015;
        p.EnvAmp.Decay = 0.4;
        p.EnvAmp.Sustain = 0.75;
        p.EnvAmp.Release = 0.6;
        p.Lfo1.Shape = "Triangle";
        p.Lfo1.Rate = 0.7;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo1", "Osc1WavetablePos", 0.45));
        p.Lfo2.Shape = "Sine";
        p.Lfo2.Rate = 0.15;
        p.Lfo2.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo2", "Filter1Cutoff", 0.3));
        p.Effects.Add(Chorus(0.4, rate: 0.5, depth: 0.3));
        p.Effects.Add(Reverb(0.35, size: 0.65, decay: 0.75));
        return p;
    }

    private static PresetData BuildSynth_DetuneCloud()
    {
        var p = new PresetData { Name = "Detune Cloud", Category = "Synth" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.7;
        p.Osc1.UnisonVoices = 7;
        p.Osc1.UnisonDetune = 0.5;
        p.Osc1.UnisonSpread = 1.0;
        p.Osc2.Wavetable = "Saw";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.5;
        p.Osc2.UnisonVoices = 5;
        p.Osc2.UnisonDetune = 0.45;
        p.Osc2.UnisonSpread = 0.9;
        p.Osc2.FineTune = 2.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 5500;
        p.Filter1.Resonance = 0.15;
        p.EnvAmp.Attack = 0.05;
        p.EnvAmp.Decay = 0.5;
        p.EnvAmp.Sustain = 0.85;
        p.EnvAmp.Release = 1.0;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 0.25;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo1", "Filter1Cutoff", 0.2));
        p.Effects.Add(Chorus(0.35, rate: 0.4, depth: 0.3));
        p.Effects.Add(Reverb(0.4, size: 0.7, decay: 0.8, width: 1.0));
        return p;
    }

    private static PresetData BuildSynth_TranceStab()
    {
        var p = new PresetData { Name = "Trance Stab", Category = "Synth" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.8;
        p.Osc1.UnisonVoices = 3;
        p.Osc1.UnisonDetune = 0.3;
        p.Osc2.Wavetable = "Saw";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.7;
        p.Osc2.FineTune = 7.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 3000;
        p.Filter1.Resonance = 0.6;
        p.Filter1.Is24dB = true;
        p.EnvAmp.Attack = 0.003;
        p.EnvAmp.Decay = 0.15;
        p.EnvAmp.Sustain = 0.5;
        p.EnvAmp.Release = 0.2;
        p.EnvFilter.Attack = 0.002;
        p.EnvFilter.Decay = 0.2;
        p.EnvFilter.Sustain = 0.1;
        p.EnvFilter.Release = 0.15;
        p.FilterEnvAmount = 0.5;
        p.Effects.Add(Delay(0.3, timeLeft: 0.25, timeRight: 0.375, feedback: 0.45, pingPong: true));
        p.Effects.Add(Reverb(0.2, size: 0.4, decay: 0.5));
        return p;
    }

    private static PresetData BuildSynth_ClassicPoly()
    {
        var p = new PresetData { Name = "Classic Poly", Category = "Synth" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.75;
        p.Osc2.Wavetable = "Saw";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.65;
        p.Osc2.FineTune = 6.0;
        p.Osc3.Wavetable = "Square";
        p.Osc3.Enabled = true;
        p.Osc3.Level = 0.4;
        p.Osc3.CoarseTune = -12;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 2800;
        p.Filter1.Resonance = 0.3;
        p.EnvAmp.Attack = 0.012;
        p.EnvAmp.Decay = 0.35;
        p.EnvAmp.Sustain = 0.7;
        p.EnvAmp.Release = 0.6;
        p.EnvFilter.Attack = 0.01;
        p.EnvFilter.Decay = 0.4;
        p.EnvFilter.Sustain = 0.4;
        p.EnvFilter.Release = 0.4;
        p.FilterEnvAmount = 0.3;
        p.Effects.Add(Chorus(0.3, rate: 0.4, depth: 0.2));
        p.Effects.Add(Reverb(0.25, size: 0.5, decay: 0.55));
        return p;
    }

    // ── PAD ─────────────────────────────────────────────────────

    private static PresetData BuildPad_WarmPad()
    {
        var p = new PresetData { Name = "Warm Pad", Category = "Pad" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.7;
        p.Osc2.Wavetable = "Saw";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.6;
        p.Osc2.FineTune = 3.0;
        p.Osc3.Wavetable = "Triangle";
        p.Osc3.Enabled = true;
        p.Osc3.Level = 0.5;
        p.Osc3.FineTune = -5.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 2000;
        p.Filter1.Resonance = 0.1;
        p.EnvAmp.Attack = 1.5;
        p.EnvAmp.Decay = 0.5;
        p.EnvAmp.Sustain = 0.85;
        p.EnvAmp.Release = 2.5;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 0.3;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo1", "Filter1Cutoff", 0.1));
        p.Effects.Add(Chorus(0.45, rate: 0.4, depth: 0.25));
        p.Effects.Add(Reverb(0.45, size: 0.75, decay: 0.85, width: 0.9));
        return p;
    }

    private static PresetData BuildPad_EvolvingPad()
    {
        var p = new PresetData { Name = "Evolving Pad", Category = "Pad" };
        p.Osc1.Wavetable = "Triangle";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.75;
        p.Osc2.Wavetable = "Saw";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.55;
        p.Osc2.FineTune = 4.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 2500;
        p.Filter1.Resonance = 0.2;
        p.EnvAmp.Attack = 2.5;
        p.EnvAmp.Decay = 0.8;
        p.EnvAmp.Sustain = 0.75;
        p.EnvAmp.Release = 3.5;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 0.1;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo1", "Osc1WavetablePos", 0.4));
        p.ModRoutes.Add(Route("Lfo1", "Filter1Cutoff", 0.25));
        p.Lfo2.Shape = "Triangle";
        p.Lfo2.Rate = 0.07;
        p.Lfo2.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo2", "Osc2WavetablePos", 0.35));
        p.Effects.Add(Reverb(0.55, size: 0.85, decay: 0.9, width: 1.0));
        p.Effects.Add(Chorus(0.3, rate: 0.2, depth: 0.2));
        return p;
    }

    private static PresetData BuildPad_ChoirPad()
    {
        var p = new PresetData { Name = "Choir Pad", Category = "Pad" };
        p.Osc1.Wavetable = "Sine";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.75;
        p.Osc1.UnisonVoices = 4;
        p.Osc1.UnisonDetune = 0.2;
        p.Osc1.UnisonSpread = 0.8;
        p.Osc2.Wavetable = "Triangle";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.5;
        p.Osc2.FineTune = 7.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 3500;
        p.Filter1.Resonance = 0.15;
        p.EnvAmp.Attack = 1.8;
        p.EnvAmp.Decay = 1.0;
        p.EnvAmp.Sustain = 0.8;
        p.EnvAmp.Release = 3.0;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 0.25;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo1", "Osc1Pan", 0.3));
        p.Effects.Add(Reverb(0.5, size: 0.8, decay: 0.9, width: 1.0));
        p.Effects.Add(Chorus(0.4, rate: 0.35, depth: 0.3));
        return p;
    }

    private static PresetData BuildPad_ShimmerPad()
    {
        var p = new PresetData { Name = "Shimmer Pad", Category = "Pad" };
        p.Osc1.Wavetable = "Triangle";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.7;
        p.Osc2.Wavetable = "Sine";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.6;
        p.Osc2.CoarseTune = 12;
        p.Osc2.FineTune = 5.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 5000;
        p.Filter1.Resonance = 0.2;
        p.EnvAmp.Attack = 3.0;
        p.EnvAmp.Decay = 0.8;
        p.EnvAmp.Sustain = 0.8;
        p.EnvAmp.Release = 4.5;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 0.15;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo1", "Osc2WavetablePos", 0.5));
        p.Effects.Add(Reverb(0.6, size: 0.9, decay: 0.95, damping: 0.3, width: 1.0));
        p.Effects.Add(Chorus(0.35, rate: 0.3, depth: 0.25));
        return p;
    }

    private static PresetData BuildPad_StringsPad()
    {
        var p = new PresetData { Name = "Strings Pad", Category = "Pad" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.7;
        p.Osc1.UnisonVoices = 4;
        p.Osc1.UnisonDetune = 0.3;
        p.Osc2.Wavetable = "Saw";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.6;
        p.Osc2.FineTune = 5.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 3000;
        p.Filter1.Resonance = 0.1;
        p.EnvAmp.Attack = 1.2;
        p.EnvAmp.Decay = 0.4;
        p.EnvAmp.Sustain = 0.85;
        p.EnvAmp.Release = 2.0;
        p.Effects.Add(Chorus(0.4, rate: 0.5, depth: 0.25));
        p.Effects.Add(Reverb(0.35, size: 0.65, decay: 0.75));
        return p;
    }

    private static PresetData BuildPad_DarkPad()
    {
        var p = new PresetData { Name = "Dark Pad", Category = "Pad" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.75;
        p.Osc2.Wavetable = "Square";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.55;
        p.Osc2.CoarseTune = -12;
        p.Osc2.FineTune = -3.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 700;
        p.Filter1.Resonance = 0.25;
        p.Filter1.Is24dB = true;
        p.EnvAmp.Attack = 2.0;
        p.EnvAmp.Decay = 0.6;
        p.EnvAmp.Sustain = 0.8;
        p.EnvAmp.Release = 3.0;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 0.08;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo1", "Filter1Cutoff", 0.15));
        p.Effects.Add(Reverb(0.5, size: 0.8, decay: 0.9, damping: 0.6));
        p.Effects.Add(Overdrive(0.1, drive: 0.2));
        return p;
    }

    private static PresetData BuildPad_BrightPad()
    {
        var p = new PresetData { Name = "Bright Pad", Category = "Pad" };
        p.Osc1.Wavetable = "Square";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.7;
        p.Osc2.Wavetable = "Saw";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.6;
        p.Osc2.FineTune = 8.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 7000;
        p.Filter1.Resonance = 0.2;
        p.EnvAmp.Attack = 1.0;
        p.EnvAmp.Decay = 0.4;
        p.EnvAmp.Sustain = 0.85;
        p.EnvAmp.Release = 2.0;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 0.5;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo1", "Filter1Cutoff", 0.12));
        p.Effects.Add(Chorus(0.35, rate: 0.5, depth: 0.2));
        p.Effects.Add(Reverb(0.3, size: 0.6, decay: 0.7, damping: 0.3));
        return p;
    }

    private static PresetData BuildPad_GlassPad()
    {
        var p = new PresetData { Name = "Glass Pad", Category = "Pad" };
        p.Osc1.Wavetable = "Sine";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.75;
        p.Osc2.Wavetable = "Sine";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.6;
        p.Osc2.CoarseTune = 7;
        p.Osc2.FineTune = 2.0;
        p.Osc3.Wavetable = "Triangle";
        p.Osc3.Enabled = true;
        p.Osc3.Level = 0.45;
        p.Osc3.CoarseTune = 12;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 8000;
        p.Filter1.Resonance = 0.3;
        p.EnvAmp.Attack = 2.2;
        p.EnvAmp.Decay = 0.6;
        p.EnvAmp.Sustain = 0.75;
        p.EnvAmp.Release = 3.5;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 0.12;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo1", "Osc1WavetablePos", 0.3));
        p.Effects.Add(Reverb(0.55, size: 0.85, decay: 0.9, damping: 0.2, width: 1.0));
        return p;
    }

    // ── PLUCK ───────────────────────────────────────────────────

    private static PresetData BuildPluck_Bell()
    {
        var p = new PresetData { Name = "Bell", Category = "Pluck" };
        p.Osc1.Wavetable = "Sine";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.8;
        p.Osc2.Wavetable = "Sine";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.5;
        p.Osc2.CoarseTune = 7;
        p.Osc3.Wavetable = "Sine";
        p.Osc3.Enabled = true;
        p.Osc3.Level = 0.3;
        p.Osc3.CoarseTune = 19;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 8000;
        p.Filter1.Resonance = 0.1;
        p.EnvAmp.Attack = 0.001;
        p.EnvAmp.Decay = 2.0;
        p.EnvAmp.Sustain = 0.0;
        p.EnvAmp.Release = 1.5;
        p.ModRoutes.Add(Route("Velocity", "Osc1Level", 0.3));
        p.Effects.Add(Reverb(0.4, size: 0.7, decay: 0.8, damping: 0.2));
        return p;
    }

    private static PresetData BuildPluck_Marimba()
    {
        var p = new PresetData { Name = "Marimba", Category = "Pluck" };
        p.Osc1.Wavetable = "Sine";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.85;
        p.Osc2.Wavetable = "Triangle";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.4;
        p.Osc2.CoarseTune = 12;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 5000;
        p.Filter1.Resonance = 0.2;
        p.EnvAmp.Attack = 0.001;
        p.EnvAmp.Decay = 0.6;
        p.EnvAmp.Sustain = 0.0;
        p.EnvAmp.Release = 0.5;
        p.Effects.Add(Reverb(0.25, size: 0.4, decay: 0.5, damping: 0.4));
        return p;
    }

    private static PresetData BuildPluck_Kalimba()
    {
        var p = new PresetData { Name = "Kalimba", Category = "Pluck" };
        p.Osc1.Wavetable = "Triangle";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.85;
        p.Osc2.Wavetable = "Sine";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.3;
        p.Osc2.CoarseTune = 12;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 6000;
        p.Filter1.Resonance = 0.15;
        p.EnvAmp.Attack = 0.001;
        p.EnvAmp.Decay = 0.8;
        p.EnvAmp.Sustain = 0.0;
        p.EnvAmp.Release = 0.7;
        p.Effects.Add(Reverb(0.3, size: 0.5, decay: 0.6, damping: 0.3));
        return p;
    }

    private static PresetData BuildPluck_Pizzicato()
    {
        var p = new PresetData { Name = "Pizzicato", Category = "Pluck" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.8;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 4000;
        p.Filter1.Resonance = 0.25;
        p.Filter1.Is24dB = true;
        p.EnvAmp.Attack = 0.001;
        p.EnvAmp.Decay = 0.25;
        p.EnvAmp.Sustain = 0.0;
        p.EnvAmp.Release = 0.2;
        p.EnvFilter.Attack = 0.001;
        p.EnvFilter.Decay = 0.15;
        p.EnvFilter.Sustain = 0.0;
        p.EnvFilter.Release = 0.1;
        p.FilterEnvAmount = 0.5;
        p.ModRoutes.Add(Route("Velocity", "Filter1Cutoff", 0.3));
        p.Effects.Add(Reverb(0.2, size: 0.35, decay: 0.35));
        return p;
    }

    private static PresetData BuildPluck_MutePluck()
    {
        var p = new PresetData { Name = "Mute Pluck", Category = "Pluck" };
        p.Osc1.Wavetable = "Triangle";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.85;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 1500;
        p.Filter1.Resonance = 0.1;
        p.EnvAmp.Attack = 0.001;
        p.EnvAmp.Decay = 0.18;
        p.EnvAmp.Sustain = 0.0;
        p.EnvAmp.Release = 0.15;
        p.EnvFilter.Attack = 0.001;
        p.EnvFilter.Decay = 0.1;
        p.EnvFilter.Sustain = 0.0;
        p.EnvFilter.Release = 0.08;
        p.FilterEnvAmount = 0.4;
        return p;
    }

    private static PresetData BuildPluck_BrightPluck()
    {
        var p = new PresetData { Name = "Bright Pluck", Category = "Pluck" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.8;
        p.Osc2.Wavetable = "Square";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.5;
        p.Osc2.CoarseTune = 12;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 9000;
        p.Filter1.Resonance = 0.35;
        p.EnvAmp.Attack = 0.001;
        p.EnvAmp.Decay = 0.35;
        p.EnvAmp.Sustain = 0.0;
        p.EnvAmp.Release = 0.3;
        p.EnvFilter.Attack = 0.001;
        p.EnvFilter.Decay = 0.2;
        p.EnvFilter.Sustain = 0.0;
        p.EnvFilter.Release = 0.15;
        p.FilterEnvAmount = 0.4;
        p.Effects.Add(Reverb(0.25, size: 0.5, decay: 0.55));
        p.Effects.Add(Delay(0.15, timeLeft: 0.1875, timeRight: 0.25, feedback: 0.3));
        return p;
    }

    // ── KEYS ────────────────────────────────────────────────────

    private static PresetData BuildKeys_ElectricPiano()
    {
        var p = new PresetData { Name = "Electric Piano", Category = "Keys" };
        p.Osc1.Wavetable = "Sine";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.8;
        p.Osc2.Wavetable = "Triangle";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.4;
        p.Osc2.CoarseTune = 7;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 5000;
        p.Filter1.Resonance = 0.1;
        p.EnvAmp.Attack = 0.002;
        p.EnvAmp.Decay = 1.5;
        p.EnvAmp.Sustain = 0.3;
        p.EnvAmp.Release = 0.8;
        p.ModRoutes.Add(Route("Velocity", "Osc1Level", 0.3));
        p.Effects.Add(Chorus(0.3, rate: 0.5, depth: 0.2));
        p.Effects.Add(Reverb(0.2, size: 0.4, decay: 0.45));
        return p;
    }

    private static PresetData BuildKeys_Organ()
    {
        var p = new PresetData { Name = "Organ", Category = "Keys" };
        p.Osc1.Wavetable = "Square";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.8;
        p.Osc2.Wavetable = "Square";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.6;
        p.Osc2.CoarseTune = 12;
        p.Osc3.Wavetable = "Square";
        p.Osc3.Enabled = true;
        p.Osc3.Level = 0.4;
        p.Osc3.CoarseTune = 19;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 8000;
        p.Filter1.Resonance = 0.0;
        p.EnvAmp.Attack = 0.002;
        p.EnvAmp.Decay = 0.0;
        p.EnvAmp.Sustain = 1.0;
        p.EnvAmp.Release = 0.03;
        p.Effects.Add(Reverb(0.2, size: 0.4, decay: 0.4));
        return p;
    }

    private static PresetData BuildKeys_Clav()
    {
        var p = new PresetData { Name = "Clav", Category = "Keys" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.8;
        p.Filter1.Mode = "BandPass";
        p.Filter1.Cutoff = 1800;
        p.Filter1.Resonance = 0.5;
        p.EnvAmp.Attack = 0.001;
        p.EnvAmp.Decay = 0.4;
        p.EnvAmp.Sustain = 0.3;
        p.EnvAmp.Release = 0.2;
        p.EnvFilter.Attack = 0.001;
        p.EnvFilter.Decay = 0.2;
        p.EnvFilter.Sustain = 0.0;
        p.EnvFilter.Release = 0.15;
        p.FilterEnvAmount = 0.6;
        p.ModRoutes.Add(Route("Velocity", "Filter1Cutoff", 0.4));
        return p;
    }

    private static PresetData BuildKeys_Rhodes()
    {
        var p = new PresetData { Name = "Rhodes", Category = "Keys" };
        p.Osc1.Wavetable = "Triangle";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.8;
        p.Osc2.Wavetable = "Sine";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.5;
        p.Osc2.CoarseTune = 7;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 4000;
        p.Filter1.Resonance = 0.15;
        p.EnvAmp.Attack = 0.003;
        p.EnvAmp.Decay = 1.8;
        p.EnvAmp.Sustain = 0.35;
        p.EnvAmp.Release = 0.9;
        p.ModRoutes.Add(Route("Velocity", "Filter1Cutoff", 0.25));
        p.Effects.Add(Chorus(0.35, rate: 0.4, depth: 0.2));
        p.Effects.Add(Reverb(0.25, size: 0.45, decay: 0.5));
        return p;
    }

    private static PresetData BuildKeys_Wurli()
    {
        var p = new PresetData { Name = "Wurli", Category = "Keys" };
        p.Osc1.Wavetable = "Triangle";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.8;
        p.Osc2.Wavetable = "Square";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.35;
        p.Osc2.CoarseTune = 12;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 5500;
        p.Filter1.Resonance = 0.2;
        p.EnvAmp.Attack = 0.002;
        p.EnvAmp.Decay = 0.8;
        p.EnvAmp.Sustain = 0.4;
        p.EnvAmp.Release = 0.6;
        p.ModRoutes.Add(Route("Velocity", "Osc1Level", 0.2));
        p.Effects.Add(Chorus(0.3, rate: 0.45, depth: 0.18));
        p.Effects.Add(Delay(0.15, timeLeft: 0.375, timeRight: 0.375, feedback: 0.3));
        return p;
    }

    // ── FX ──────────────────────────────────────────────────────

    private static PresetData BuildFx_Riser()
    {
        var p = new PresetData { Name = "Riser", Category = "FX" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.7;
        p.Osc1.UnisonVoices = 4;
        p.Osc1.UnisonDetune = 0.3;
        p.Noise.Enabled = true;
        p.Noise.Level = 0.3;
        p.Filter1.Mode = "HighPass";
        p.Filter1.Cutoff = 100;
        p.Filter1.Resonance = 0.4;
        p.EnvAmp.Attack = 3.0;
        p.EnvAmp.Decay = 0.0;
        p.EnvAmp.Sustain = 1.0;
        p.EnvAmp.Release = 0.5;
        p.EnvFilter.Attack = 3.0;
        p.EnvFilter.Decay = 0.0;
        p.EnvFilter.Sustain = 1.0;
        p.EnvFilter.Release = 0.5;
        p.FilterEnvAmount = 0.7;
        p.ModRoutes.Add(Route("Envelope1", "Osc1Pitch", 0.5));
        p.Effects.Add(Reverb(0.4, size: 0.7, decay: 0.7));
        return p;
    }

    private static PresetData BuildFx_Sweep()
    {
        var p = new PresetData { Name = "Sweep", Category = "FX" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.75;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 8000;
        p.Filter1.Resonance = 0.6;
        p.EnvAmp.Attack = 0.01;
        p.EnvAmp.Decay = 3.0;
        p.EnvAmp.Sustain = 0.0;
        p.EnvAmp.Release = 1.0;
        p.EnvFilter.Attack = 0.01;
        p.EnvFilter.Decay = 3.0;
        p.EnvFilter.Sustain = 0.0;
        p.EnvFilter.Release = 1.0;
        p.FilterEnvAmount = -0.7;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 0.3;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo1", "Filter1Resonance", 0.2));
        p.Effects.Add(Reverb(0.35, size: 0.6, decay: 0.7));
        return p;
    }

    private static PresetData BuildFx_NoiseTexture()
    {
        var p = new PresetData { Name = "Noise Texture", Category = "FX" };
        p.Osc1.Wavetable = "Sine";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.4;
        p.Noise.Enabled = true;
        p.Noise.Level = 0.7;
        p.Noise.Type = "Pink";
        p.Filter1.Mode = "BandPass";
        p.Filter1.Cutoff = 2000;
        p.Filter1.Resonance = 0.4;
        p.EnvAmp.Attack = 0.5;
        p.EnvAmp.Decay = 1.0;
        p.EnvAmp.Sustain = 0.6;
        p.EnvAmp.Release = 2.0;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 0.05;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo1", "Filter1Cutoff", 0.4));
        p.Effects.Add(Reverb(0.6, size: 0.85, decay: 0.9, damping: 0.6));
        return p;
    }

    private static PresetData BuildFx_Impact()
    {
        var p = new PresetData { Name = "Impact", Category = "FX" };
        p.Osc1.Wavetable = "Square";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.9;
        p.Noise.Enabled = true;
        p.Noise.Level = 0.6;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 4000;
        p.Filter1.Resonance = 0.3;
        p.EnvAmp.Attack = 0.001;
        p.EnvAmp.Decay = 1.5;
        p.EnvAmp.Sustain = 0.0;
        p.EnvAmp.Release = 1.0;
        p.EnvFilter.Attack = 0.001;
        p.EnvFilter.Decay = 0.8;
        p.EnvFilter.Sustain = 0.0;
        p.EnvFilter.Release = 0.5;
        p.FilterEnvAmount = -0.6;
        p.ModRoutes.Add(Route("Envelope1", "Osc1Pitch", -0.4));
        p.Effects.Add(Reverb(0.35, size: 0.55, decay: 0.6));
        return p;
    }

    private static PresetData BuildFx_Drone()
    {
        var p = new PresetData { Name = "Drone", Category = "FX" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.7;
        p.Osc1.UnisonVoices = 5;
        p.Osc1.UnisonDetune = 0.4;
        p.Osc2.Wavetable = "Triangle";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.5;
        p.Osc2.CoarseTune = -12;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 1500;
        p.Filter1.Resonance = 0.3;
        p.EnvAmp.Attack = 2.0;
        p.EnvAmp.Decay = 0.5;
        p.EnvAmp.Sustain = 0.85;
        p.EnvAmp.Release = 5.0;
        p.Lfo1.Shape = "SawDown";
        p.Lfo1.Rate = 0.05;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo1", "Filter1Cutoff", 0.3));
        p.Effects.Add(Reverb(0.6, size: 0.9, decay: 0.95, damping: 0.5, width: 1.0));
        p.Effects.Add(Chorus(0.3, rate: 0.2, depth: 0.3));
        return p;
    }

    // ── STRINGS ─────────────────────────────────────────────────

    private static PresetData BuildStrings_Analog()
    {
        var p = new PresetData { Name = "Analog Strings", Category = "Strings" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.7;
        p.Osc1.UnisonVoices = 4;
        p.Osc1.UnisonDetune = 0.3;
        p.Osc2.Wavetable = "Saw";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.6;
        p.Osc2.FineTune = 5.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 4000;
        p.Filter1.Resonance = 0.1;
        p.EnvAmp.Attack = 2.0;
        p.EnvAmp.Decay = 0.5;
        p.EnvAmp.Sustain = 0.8;
        p.EnvAmp.Release = 1.5;
        p.Effects.Add(Chorus(0.4, rate: 0.5, depth: 0.25));
        p.Effects.Add(Reverb(0.35, size: 0.65, decay: 0.7));
        return p;
    }

    private static PresetData BuildStrings_Cinematic()
    {
        var p = new PresetData { Name = "Cinematic Strings", Category = "Strings" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.7;
        p.Osc1.UnisonVoices = 5;
        p.Osc1.UnisonDetune = 0.35;
        p.Osc1.UnisonSpread = 0.7;
        p.Osc2.Wavetable = "Saw";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.55;
        p.Osc2.FineTune = 4.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 3500;
        p.Filter1.Resonance = 0.0;
        p.EnvAmp.Attack = 3.5;
        p.EnvAmp.Decay = 0.5;
        p.EnvAmp.Sustain = 0.8;
        p.EnvAmp.Release = 2.5;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 4.5;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("ModWheel", "Lfo1Depth", 0.6));
        p.ModRoutes.Add(Route("Lfo1", "Osc1Pitch", 0.04));
        p.Effects.Add(Reverb(0.5, size: 0.8, decay: 0.85, width: 1.0));
        p.Effects.Add(Chorus(0.35, rate: 0.3, depth: 0.2));
        return p;
    }

    private static PresetData BuildStrings_SlowAttack()
    {
        var p = new PresetData { Name = "Slow Attack Strings", Category = "Strings" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.7;
        p.Osc1.UnisonVoices = 3;
        p.Osc1.UnisonDetune = 0.25;
        p.Osc2.Wavetable = "Saw";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.6;
        p.Osc2.FineTune = 6.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 3000;
        p.Filter1.Resonance = 0.1;
        p.EnvAmp.Attack = 4.0;
        p.EnvAmp.Decay = 0.6;
        p.EnvAmp.Sustain = 0.8;
        p.EnvAmp.Release = 3.0;
        p.Effects.Add(Reverb(0.45, size: 0.75, decay: 0.8));
        p.Effects.Add(Chorus(0.35, rate: 0.4, depth: 0.2));
        return p;
    }

    private static PresetData BuildStrings_Solo()
    {
        var p = new PresetData { Name = "Solo Strings", Category = "Strings" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.75;
        p.Osc2.Wavetable = "Saw";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.55;
        p.Osc2.FineTune = 4.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 5000;
        p.Filter1.Resonance = 0.15;
        p.EnvAmp.Attack = 1.5;
        p.EnvAmp.Decay = 0.4;
        p.EnvAmp.Sustain = 0.82;
        p.EnvAmp.Release = 1.2;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 5.0;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("ModWheel", "Lfo1Depth", 0.7));
        p.ModRoutes.Add(Route("Lfo1", "Osc1Pitch", 0.05));
        p.GlideTime = 0.06;
        p.GlideMode = "Legato";
        p.Effects.Add(Reverb(0.3, size: 0.55, decay: 0.6));
        return p;
    }

    private static PresetData BuildStrings_Ensemble()
    {
        var p = new PresetData { Name = "Ensemble Strings", Category = "Strings" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.7;
        p.Osc1.UnisonVoices = 6;
        p.Osc1.UnisonDetune = 0.35;
        p.Osc1.UnisonSpread = 0.9;
        p.Osc2.Wavetable = "Saw";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.55;
        p.Osc2.UnisonVoices = 4;
        p.Osc2.UnisonDetune = 0.3;
        p.Osc2.FineTune = 5.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 4500;
        p.Filter1.Resonance = 0.05;
        p.EnvAmp.Attack = 2.5;
        p.EnvAmp.Decay = 0.5;
        p.EnvAmp.Sustain = 0.82;
        p.EnvAmp.Release = 2.5;
        p.Effects.Add(Chorus(0.4, rate: 0.45, depth: 0.3));
        p.Effects.Add(Reverb(0.4, size: 0.75, decay: 0.8, width: 1.0));
        return p;
    }

    // ── AMBIENT ─────────────────────────────────────────────────

    private static PresetData BuildAmbient_Atmosphere()
    {
        var p = new PresetData { Name = "Atmosphere", Category = "Ambient" };
        p.Osc1.Wavetable = "Sine";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.7;
        p.Osc1.UnisonVoices = 4;
        p.Osc1.UnisonDetune = 0.2;
        p.Osc2.Wavetable = "Triangle";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.5;
        p.Osc2.FineTune = 7.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 3000;
        p.Filter1.Resonance = 0.1;
        p.EnvAmp.Attack = 3.0;
        p.EnvAmp.Decay = 1.0;
        p.EnvAmp.Sustain = 0.75;
        p.EnvAmp.Release = 5.0;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 0.08;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo1", "Osc1WavetablePos", 0.35));
        p.ModRoutes.Add(Route("Lfo1", "Filter1Cutoff", 0.2));
        p.Effects.Add(Reverb(0.6, size: 0.9, decay: 0.95, damping: 0.3, width: 1.0));
        p.Effects.Add(Chorus(0.3, rate: 0.2, depth: 0.25));
        return p;
    }

    private static PresetData BuildAmbient_Ethereal()
    {
        var p = new PresetData { Name = "Ethereal", Category = "Ambient" };
        p.Osc1.Wavetable = "Sine";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.65;
        p.Osc1.UnisonVoices = 5;
        p.Osc1.UnisonDetune = 0.3;
        p.Osc1.UnisonSpread = 0.9;
        p.Osc2.Wavetable = "Triangle";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.5;
        p.Osc2.CoarseTune = 12;
        p.Osc2.FineTune = 5.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 4000;
        p.Filter1.Resonance = 0.15;
        p.EnvAmp.Attack = 4.0;
        p.EnvAmp.Decay = 1.5;
        p.EnvAmp.Sustain = 0.7;
        p.EnvAmp.Release = 6.0;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 0.05;
        p.Lfo1.Depth = 1.0;
        p.Lfo2.Shape = "Triangle";
        p.Lfo2.Rate = 0.12;
        p.Lfo2.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo1", "Filter1Cutoff", 0.3));
        p.ModRoutes.Add(Route("Lfo2", "Osc1Pan", 0.4));
        p.Effects.Add(Reverb(0.65, size: 0.95, decay: 0.97, damping: 0.2, width: 1.0));
        p.Effects.Add(Delay(0.25, timeLeft: 0.75, timeRight: 1.0, feedback: 0.3, pingPong: true));
        return p;
    }

    private static PresetData BuildAmbient_Texture()
    {
        var p = new PresetData { Name = "Texture", Category = "Ambient" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.6;
        p.Osc1.UnisonVoices = 6;
        p.Osc1.UnisonDetune = 0.45;
        p.Osc1.UnisonSpread = 1.0;
        p.Noise.Enabled = true;
        p.Noise.Level = 0.2;
        p.Noise.Type = "Pink";
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 2000;
        p.Filter1.Resonance = 0.2;
        p.EnvAmp.Attack = 4.0;
        p.EnvAmp.Decay = 1.0;
        p.EnvAmp.Sustain = 0.65;
        p.EnvAmp.Release = 4.0;
        p.Lfo1.Shape = "SampleHold";
        p.Lfo1.Rate = 0.1;
        p.Lfo1.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo1", "Filter1Cutoff", 0.25));
        p.ModRoutes.Add(Route("Lfo1", "Osc1Pan", 0.3));
        p.Effects.Add(Reverb(0.55, size: 0.88, decay: 0.92, damping: 0.4, width: 1.0));
        p.Effects.Add(Chorus(0.25, rate: 0.15, depth: 0.3));
        return p;
    }

    private static PresetData BuildAmbient_DronePad()
    {
        var p = new PresetData { Name = "Drone Pad", Category = "Ambient" };
        p.Osc1.Wavetable = "Saw";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.65;
        p.Osc1.UnisonVoices = 5;
        p.Osc1.UnisonDetune = 0.4;
        p.Osc2.Wavetable = "Triangle";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.5;
        p.Osc2.CoarseTune = -12;
        p.Osc2.FineTune = 8.0;
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 1200;
        p.Filter1.Resonance = 0.25;
        p.EnvAmp.Attack = 5.0;
        p.EnvAmp.Decay = 2.0;
        p.EnvAmp.Sustain = 0.75;
        p.EnvAmp.Release = 7.0;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 0.04;
        p.Lfo1.Depth = 1.0;
        p.Lfo2.Shape = "SawDown";
        p.Lfo2.Rate = 0.07;
        p.Lfo2.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo1", "Filter1Cutoff", 0.3));
        p.ModRoutes.Add(Route("Lfo2", "Osc1WavetablePos", 0.4));
        p.Effects.Add(Reverb(0.65, size: 0.95, decay: 0.97, damping: 0.5, width: 1.0));
        p.Effects.Add(Chorus(0.3, rate: 0.1, depth: 0.35));
        return p;
    }

    private static PresetData BuildAmbient_DeepSpace()
    {
        var p = new PresetData { Name = "Deep Space", Category = "Ambient" };
        p.Osc1.Wavetable = "Sine";
        p.Osc1.Enabled = true;
        p.Osc1.Level = 0.6;
        p.Osc1.UnisonVoices = 6;
        p.Osc1.UnisonDetune = 0.5;
        p.Osc1.UnisonSpread = 1.0;
        p.Osc2.Wavetable = "Triangle";
        p.Osc2.Enabled = true;
        p.Osc2.Level = 0.45;
        p.Osc2.CoarseTune = -12;
        p.Osc2.FineTune = -7.0;
        p.Noise.Enabled = true;
        p.Noise.Level = 0.15;
        p.Noise.Type = "Pink";
        p.Filter1.Mode = "LowPass";
        p.Filter1.Cutoff = 2500;
        p.Filter1.Resonance = 0.2;
        p.EnvAmp.Attack = 6.0;
        p.EnvAmp.Decay = 2.0;
        p.EnvAmp.Sustain = 0.7;
        p.EnvAmp.Release = 8.0;
        p.Lfo1.Shape = "Sine";
        p.Lfo1.Rate = 0.03;
        p.Lfo1.Depth = 1.0;
        p.Lfo2.Shape = "Triangle";
        p.Lfo2.Rate = 0.05;
        p.Lfo2.Depth = 1.0;
        p.Lfo3.Shape = "SampleHold";
        p.Lfo3.Rate = 0.08;
        p.Lfo3.Depth = 1.0;
        p.ModRoutes.Add(Route("Lfo1", "Filter1Cutoff", 0.3));
        p.ModRoutes.Add(Route("Lfo2", "Osc1WavetablePos", 0.4));
        p.ModRoutes.Add(Route("Lfo3", "Osc1Pan", 0.5));
        p.Effects.Add(Reverb(0.7, size: 0.98, decay: 0.98, damping: 0.3, width: 1.0));
        p.Effects.Add(Delay(0.3, timeLeft: 1.0, timeRight: 1.5, feedback: 0.4, pingPong: true, damping: 0.4));
        return p;
    }
}

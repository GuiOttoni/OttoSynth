using AudioPlugSharp;
using OttoSynth.Core;
using OttoSynth.Core.DSP.Filters;
using OttoSynth.Core.DSP.Modulation;
using OttoSynth.Core.DSP.Oscillators;
using OttoSynth.Core.Plugin;
using OttoSynth.Core.Voice;
using P = OttoSynth.Core.Plugin.ParameterIds;

namespace OttoSynth.Plugin;

/// <summary>
/// VST3-specific parameter registration. The actual dispatch logic (Apply / CaptureAll)
/// lives in <see cref="ParameterDispatcher"/> in OttoSynth.Core so that the CLAP plugin
/// can reuse it; only <see cref="RegisterAll"/> depends on AudioPlugSharp's
/// <see cref="AudioPluginBase"/> type.
/// </summary>
internal static class ParameterMap
{
    private static readonly string[] WarpTypeNames     = Enum.GetNames<WavetableOscillator.WaveWarp>();
    private static readonly string[] FilterModeNames   = Enum.GetNames<StateVariableFilter.FilterMode>();
    private static readonly string[] LfoShapeNames     = Enum.GetNames<LfoGenerator.LfoShape>();
    private static readonly string[] GlideModeNames    = Enum.GetNames<SynthVoice.GlideMode>();
    private static readonly string[] OscRoutingNames   = Enum.GetNames<SynthVoice.OscRouting>();
    private static readonly string[] FilterRoutingNames= Enum.GetNames<SynthVoice.FilterRouting>();

    // ─── Registration ────────────────────────────────────────────

    public static void RegisterAll(AudioPluginBase plugin)
    {
        // Master
        Add(plugin, P.MasterVolume,   "Master Volume",    0, 1, 0.8);
        Add(plugin, P.PitchBendRange, "Pitch Bend Range", 0, 24, 2, "F0");
        Add(plugin, P.GlideTime,      "Glide Time",       0, 2, 0, rangePower: 2.0);
        AddStepped(plugin, P.GlideMode, "Glide Mode", GlideModeNames.Length - 1, 0);
        Add(plugin, P.Macro1, "Macro 1", 0, 1, 0);
        Add(plugin, P.Macro2, "Macro 2", 0, 1, 0);
        Add(plugin, P.Macro3, "Macro 3", 0, 1, 0);
        Add(plugin, P.Macro4, "Macro 4", 0, 1, 0);

        // OSC 1
        Add(plugin, P.Osc1Level,        "OSC1 Level",         0, 1, 0.8);
        AddToggle(plugin, P.Osc1Enabled, "OSC1 Enabled", 1);
        Add(plugin, P.Osc1Position,     "OSC1 Position",      0, 1, 0);
        Add(plugin, P.Osc1WarpAmount,   "OSC1 Warp Amount",   0, 1, 0);
        AddStepped(plugin, P.Osc1WarpType, "OSC1 Warp Type", WarpTypeNames.Length - 1, 0);
        Add(plugin, P.Osc1CoarseTune,   "OSC1 Coarse Tune", -24, 24, 0, "F0");
        Add(plugin, P.Osc1FineTune,     "OSC1 Fine Tune",  -100, 100, 0, "F1");
        Add(plugin, P.Osc1Pan,          "OSC1 Pan",          -1, 1, 0);
        Add(plugin, P.Osc1UnisonVoices, "OSC1 Unison Voices", 1, 16, 1, "F0");
        Add(plugin, P.Osc1UnisonDetune, "OSC1 Unison Detune", 0, 100, 20, "F1");
        Add(plugin, P.Osc1UnisonSpread, "OSC1 Unison Spread", 0, 1, 0.8);

        // OSC 2
        Add(plugin, P.Osc2Level,        "OSC2 Level",         0, 1, 0.6);
        AddToggle(plugin, P.Osc2Enabled, "OSC2 Enabled", 0);
        Add(plugin, P.Osc2Position,     "OSC2 Position",      0, 1, 0);
        Add(plugin, P.Osc2WarpAmount,   "OSC2 Warp Amount",   0, 1, 0);
        AddStepped(plugin, P.Osc2WarpType, "OSC2 Warp Type", WarpTypeNames.Length - 1, 0);
        Add(plugin, P.Osc2CoarseTune,   "OSC2 Coarse Tune", -24, 24, 0, "F0");
        Add(plugin, P.Osc2FineTune,     "OSC2 Fine Tune",  -100, 100, 0, "F1");
        Add(plugin, P.Osc2Pan,          "OSC2 Pan",          -1, 1, 0);
        Add(plugin, P.Osc2UnisonVoices, "OSC2 Unison Voices", 1, 16, 1, "F0");
        Add(plugin, P.Osc2UnisonDetune, "OSC2 Unison Detune", 0, 100, 20, "F1");
        Add(plugin, P.Osc2UnisonSpread, "OSC2 Unison Spread", 0, 1, 0.8);

        // OSC 3
        Add(plugin, P.Osc3Level,        "OSC3 Level",         0, 1, 0.5);
        AddToggle(plugin, P.Osc3Enabled, "OSC3 Enabled", 0);
        Add(plugin, P.Osc3Position,     "OSC3 Position",      0, 1, 0);
        Add(plugin, P.Osc3WarpAmount,   "OSC3 Warp Amount",   0, 1, 0);
        AddStepped(plugin, P.Osc3WarpType, "OSC3 Warp Type", WarpTypeNames.Length - 1, 0);
        Add(plugin, P.Osc3CoarseTune,   "OSC3 Coarse Tune", -24, 24, 0, "F0");
        Add(plugin, P.Osc3FineTune,     "OSC3 Fine Tune",  -100, 100, 0, "F1");
        Add(plugin, P.Osc3Pan,          "OSC3 Pan",          -1, 1, 0);
        Add(plugin, P.Osc3UnisonVoices, "OSC3 Unison Voices", 1, 16, 1, "F0");
        Add(plugin, P.Osc3UnisonDetune, "OSC3 Unison Detune", 0, 100, 20, "F1");
        Add(plugin, P.Osc3UnisonSpread, "OSC3 Unison Spread", 0, 1, 0.8);

        // Noise
        Add(plugin, P.NoiseLevel,   "Noise Level",   0, 1, 0);
        AddToggle(plugin, P.NoiseEnabled, "Noise Enabled", 0);

        // Filter 1
        AddStepped(plugin, P.Filter1Mode,        "Filter1 Mode",         FilterModeNames.Length - 1, 0);
        Add(plugin,        P.Filter1Cutoff,      "Filter1 Cutoff",       20, 20000, 20000, rangePower: 4.0);
        Add(plugin,        P.Filter1Resonance,   "Filter1 Resonance",    0, 1, 0);
        Add(plugin,        P.Filter1Drive,        "Filter1 Drive",        0, 1, 0);
        AddToggle(plugin,  P.Filter1Is24dB,       "Filter1 24dB",          0);
        Add(plugin,        P.Filter1KeyTracking,  "Filter1 Key Track",     0, 1, 0);
        Add(plugin,        P.Filter1FormantVowel, "Filter1 Formant Vowel", 0, 1, 0);
        Add(plugin,        P.Filter1FormantShift, "Filter1 Formant Shift", 0.5, 2.0, 1.0);

        // Filter 2
        AddStepped(plugin, P.Filter2Mode,         "Filter2 Mode",          FilterModeNames.Length - 1, 0);
        Add(plugin,        P.Filter2Cutoff,        "Filter2 Cutoff",        20, 20000, 20000, rangePower: 4.0);
        Add(plugin,        P.Filter2Resonance,     "Filter2 Resonance",     0, 1, 0);
        Add(plugin,        P.Filter2Drive,         "Filter2 Drive",         0, 1, 0);
        AddToggle(plugin,  P.Filter2Is24dB,        "Filter2 24dB",          0);
        Add(plugin,        P.Filter2KeyTracking,   "Filter2 Key Track",     0, 1, 0);
        Add(plugin,        P.Filter2FormantVowel,  "Filter2 Formant Vowel", 0, 1, 0);
        Add(plugin,        P.Filter2FormantShift,  "Filter2 Formant Shift", 0.5, 2.0, 1.0);

        // Filter Routing
        AddStepped(plugin, P.FilterRouting,   "Filter Routing",    FilterRoutingNames.Length - 1, 0);
        Add(plugin,        P.FilterEnvAmount, "Filter Env Amount", -1, 1, 0);

        // Envelopes
        AddEnv(plugin, P.EnvAmpAttack,   "Amp Env Attack",  0.001, 10, 0.01);
        AddEnv(plugin, P.EnvAmpDecay,    "Amp Env Decay",   0.001, 10, 0.3);
        Add(plugin,    P.EnvAmpSustain,  "Amp Env Sustain", 0, 1, 0.7);
        AddEnv(plugin, P.EnvAmpRelease,  "Amp Env Release", 0.001, 10, 0.5);

        AddEnv(plugin, P.EnvFilterAttack,   "Flt Env Attack",  0.001, 10, 0.001);
        AddEnv(plugin, P.EnvFilterDecay,    "Flt Env Decay",   0.001, 10, 0.5);
        Add(plugin,    P.EnvFilterSustain,  "Flt Env Sustain", 0, 1, 0.3);
        AddEnv(plugin, P.EnvFilterRelease,  "Flt Env Release", 0.001, 10, 0.3);

        AddEnv(plugin, P.EnvFreeAttack,   "Free Env Attack",  0.001, 10, 0.01);
        AddEnv(plugin, P.EnvFreeDecay,    "Free Env Decay",   0.001, 10, 0.3);
        Add(plugin,    P.EnvFreeSustain,  "Free Env Sustain", 0, 1, 0.7);
        AddEnv(plugin, P.EnvFreeRelease,  "Free Env Release", 0.001, 10, 0.5);

        // LFOs
        AddStepped(plugin, P.Lfo1Shape,    "LFO1 Shape",   LfoShapeNames.Length - 1, 0);
        Add(plugin,        P.Lfo1Rate,     "LFO1 Rate",    0.01, 30, 1, rangePower: 2.0);
        Add(plugin,        P.Lfo1Depth,    "LFO1 Depth",   0, 1, 1);
        AddToggle(plugin,  P.Lfo1Retrigger,"LFO1 Retrig",  1);

        AddStepped(plugin, P.Lfo2Shape,    "LFO2 Shape",   LfoShapeNames.Length - 1, 0);
        Add(plugin,        P.Lfo2Rate,     "LFO2 Rate",    0.01, 30, 1, rangePower: 2.0);
        Add(plugin,        P.Lfo2Depth,    "LFO2 Depth",   0, 1, 1);
        AddToggle(plugin,  P.Lfo2Retrigger,"LFO2 Retrig",  1);

        AddStepped(plugin, P.Lfo3Shape,    "LFO3 Shape",   LfoShapeNames.Length - 1, 0);
        Add(plugin,        P.Lfo3Rate,     "LFO3 Rate",    0.01, 30, 1, rangePower: 2.0);
        Add(plugin,        P.Lfo3Depth,    "LFO3 Depth",   0, 1, 1);
        AddToggle(plugin,  P.Lfo3Retrigger,"LFO3 Retrig",  1);

        // OSC Routing
        Add(plugin, P.OscRouteDepth_1_2, "Route 1→2 Depth", 0, 1, 0.5);
        Add(plugin, P.OscRouteDepth_1_3, "Route 1→3 Depth", 0, 1, 0.5);
        Add(plugin, P.OscRouteDepth_2_1, "Route 2→1 Depth", 0, 1, 0.5);
        Add(plugin, P.OscRouteDepth_2_3, "Route 2→3 Depth", 0, 1, 0.5);
        Add(plugin, P.OscRouteDepth_3_1, "Route 3→1 Depth", 0, 1, 0.5);
        Add(plugin, P.OscRouteDepth_3_2, "Route 3→2 Depth", 0, 1, 0.5);
        AddStepped(plugin, P.OscRouteMode_1_2, "Route 1→2 Mode", OscRoutingNames.Length - 1, 0);
        AddStepped(plugin, P.OscRouteMode_1_3, "Route 1→3 Mode", OscRoutingNames.Length - 1, 0);
        AddStepped(plugin, P.OscRouteMode_2_1, "Route 2→1 Mode", OscRoutingNames.Length - 1, 0);
        AddStepped(plugin, P.OscRouteMode_2_3, "Route 2→3 Mode", OscRoutingNames.Length - 1, 0);
        AddStepped(plugin, P.OscRouteMode_3_1, "Route 3→1 Mode", OscRoutingNames.Length - 1, 0);
        AddStepped(plugin, P.OscRouteMode_3_2, "Route 3→2 Mode", OscRoutingNames.Length - 1, 0);

        // Mod matrix
        for (int i = 0; i < 32; i++)
            Add(plugin, P.ModRouteAmount(i), $"Mod Route {i + 1}", -1, 1, 0);

        // Effects
        for (int s = 0; s < P.EffectsSlotCount; s++)
        {
            AddToggle(plugin, P.EffectBypass(s), $"FX{s + 1} Bypass", 0);
            Add(plugin,       P.EffectMix(s),    $"FX{s + 1} Mix",    0, 1, 1);
        }
    }

    // ─── Private helpers ─────────────────────────────────────────

    private static void Add(AudioPluginBase plugin, int id, string name,
        double min, double max, double def,
        string fmt = "F2", double rangePower = 1.0)
    {
        plugin.AddParameter(new OttoParameter
        {
            ID           = id.ToString(),
            Name         = name,
            MinValue     = min,
            MaxValue     = max,
            DefaultValue = def,
            RangePower   = rangePower,
            ValueFormat  = fmt,
            EditValue    = def,
            ProcessValue = def
        });
    }

    private static void AddEnv(AudioPluginBase plugin, int id, string name,
        double min, double max, double def)
        => Add(plugin, id, name, min, max, def, "F3", rangePower: 3.0);

    private static void AddToggle(AudioPluginBase plugin, int id, string name, double def)
        => Add(plugin, id, name, 0, 1, def, "F0");

    private static void AddStepped(AudioPluginBase plugin, int id, string name,
        int maxValue, int def)
        => Add(plugin, id, name, 0, maxValue, def, "F0");
}

/// <summary>
/// Adapter exposing the VST3 parameter store as an <see cref="IPluginHost"/>
/// for the host-agnostic <see cref="ParameterDispatcher"/>.
/// </summary>
internal sealed class Vst3PluginHost : IPluginHost
{
    private readonly Dictionary<int, AudioPluginParameter> _paramsById;

    public Vst3PluginHost(Dictionary<int, AudioPluginParameter> paramsById)
    {
        _paramsById = paramsById;
    }

    public double GetParameterValue(int id)
        => _paramsById.TryGetValue(id, out var p) ? p.ProcessValue : 0.0;

    public void SetParameterValue(int id, double value)
    {
        if (_paramsById.TryGetValue(id, out var p))
            p.ProcessValue = value;
    }
}

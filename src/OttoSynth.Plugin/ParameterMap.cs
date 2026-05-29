using AudioPlugSharp;
using OttoSynth.Core;
using OttoSynth.Core.DSP.Filters;
using OttoSynth.Core.DSP.Modulation;
using OttoSynth.Core.DSP.Oscillators;
using OttoSynth.Core.Voice;

namespace OttoSynth.Plugin;

/// <summary>
/// Stable integer IDs for every automatable VST3 parameter.
/// IDs must never be reordered or reused — DAWs serialize them by ID.
/// Numbering scheme:
///   1xxx = Master / Global
///   2xxx = OSC 1, 21xx = OSC 2, 22xx = OSC 3, 23xx = Noise
///   3xxx = Filter 1, 31xx = Filter 2, 32xx = Filter Routing
///   4xxx = Envelopes (4001-4007 Amp, 4011-4017 Filter, 4021-4027 Free)
///   5xxx = LFOs (5001-5004 LFO1, 5011-5014 LFO2, 5021-5024 LFO3)
///   6xxx = OSC Routing matrix depths/modes
///   7xxx = Mod Matrix route amounts (7001-7032)
///   8xxx = Effects (by slot index, 8_s_pp where s=slot 0-10, pp=param)
/// </summary>
internal static class P
{
    // ── Master / Global ────────────────────────────────────────────
    public const int MasterVolume   = 1001;
    public const int PitchBendRange = 1002;
    public const int GlideTime      = 1003;
    public const int GlideMode      = 1004;   // stepped: 0=Off 1=Always 2=Legato
    public const int Macro1         = 1005;
    public const int Macro2         = 1006;
    public const int Macro3         = 1007;
    public const int Macro4         = 1008;

    // ── OSC 1 ────────────────────────────────────────────────────
    public const int Osc1Level        = 2001;
    public const int Osc1Enabled      = 2002;  // 0=off 1=on
    public const int Osc1Position     = 2003;
    public const int Osc1WarpAmount   = 2004;
    public const int Osc1WarpType     = 2005;  // stepped enum WaveWarp
    public const int Osc1CoarseTune   = 2006;  // -24..24
    public const int Osc1FineTune     = 2007;  // -100..100 cents
    public const int Osc1Pan          = 2008;  // -1..1
    public const int Osc1UnisonVoices = 2009;  // 1..16
    public const int Osc1UnisonDetune = 2010;  // 0..100 cents
    public const int Osc1UnisonSpread = 2011;

    // ── OSC 2 ────────────────────────────────────────────────────
    public const int Osc2Level        = 2101;
    public const int Osc2Enabled      = 2102;
    public const int Osc2Position     = 2103;
    public const int Osc2WarpAmount   = 2104;
    public const int Osc2WarpType     = 2105;
    public const int Osc2CoarseTune   = 2106;
    public const int Osc2FineTune     = 2107;
    public const int Osc2Pan          = 2108;
    public const int Osc2UnisonVoices = 2109;
    public const int Osc2UnisonDetune = 2110;
    public const int Osc2UnisonSpread = 2111;

    // ── OSC 3 ────────────────────────────────────────────────────
    public const int Osc3Level        = 2201;
    public const int Osc3Enabled      = 2202;
    public const int Osc3Position     = 2203;
    public const int Osc3WarpAmount   = 2204;
    public const int Osc3WarpType     = 2205;
    public const int Osc3CoarseTune   = 2206;
    public const int Osc3FineTune     = 2207;
    public const int Osc3Pan          = 2208;
    public const int Osc3UnisonVoices = 2209;
    public const int Osc3UnisonDetune = 2210;
    public const int Osc3UnisonSpread = 2211;

    // ── Noise ────────────────────────────────────────────────────
    public const int NoiseLevel   = 2301;
    public const int NoiseEnabled = 2302;

    // ── Filter 1 ─────────────────────────────────────────────────
    public const int Filter1Mode           = 3001;  // stepped enum FilterMode
    public const int Filter1Cutoff         = 3002;  // 20..20000 Hz (log)
    public const int Filter1Resonance      = 3003;
    public const int Filter1Drive          = 3004;
    public const int Filter1Is24dB         = 3005;  // 0/1
    public const int Filter1KeyTracking    = 3006;  // 0..1
    public const int Filter1FormantVowel   = 3007;  // 0..1 (A→E→I→O→U)
    public const int Filter1FormantShift   = 3008;  // 0.5..2.0

    // ── Filter 2 ─────────────────────────────────────────────────
    public const int Filter2Mode           = 3101;
    public const int Filter2Cutoff         = 3102;
    public const int Filter2Resonance      = 3103;
    public const int Filter2Drive          = 3104;
    public const int Filter2Is24dB         = 3105;
    public const int Filter2KeyTracking    = 3106;
    public const int Filter2FormantVowel   = 3107;  // 0..1
    public const int Filter2FormantShift   = 3108;  // 0.5..2.0

    // ── Filter Routing ───────────────────────────────────────────
    public const int FilterRouting    = 3201;  // stepped: 0=Serial 1=Parallel 2=Split
    public const int FilterEnvAmount  = 3202;  // -1..1

    // ── Envelope 1 (Amp) ─────────────────────────────────────────
    public const int EnvAmpAttack   = 4001;
    public const int EnvAmpDecay    = 4002;
    public const int EnvAmpSustain  = 4003;
    public const int EnvAmpRelease  = 4004;
    // 4005-4007 reserved for curves when implemented

    // ── Envelope 2 (Filter) ──────────────────────────────────────
    public const int EnvFilterAttack   = 4011;
    public const int EnvFilterDecay    = 4012;
    public const int EnvFilterSustain  = 4013;
    public const int EnvFilterRelease  = 4014;
    // 4015-4017 reserved

    // ── Envelope 3 (Free) ────────────────────────────────────────
    public const int EnvFreeAttack   = 4021;
    public const int EnvFreeDecay    = 4022;
    public const int EnvFreeSustain  = 4023;
    public const int EnvFreeRelease  = 4024;
    // 4025-4027 reserved

    // ── LFO 1 ────────────────────────────────────────────────────
    public const int Lfo1Shape    = 5001;  // stepped enum LfoShape
    public const int Lfo1Rate     = 5002;  // 0.01..30 Hz
    public const int Lfo1Depth    = 5003;
    public const int Lfo1Retrigger = 5004; // 0/1

    // ── LFO 2 ────────────────────────────────────────────────────
    public const int Lfo2Shape    = 5011;
    public const int Lfo2Rate     = 5012;
    public const int Lfo2Depth    = 5013;
    public const int Lfo2Retrigger = 5014;

    // ── LFO 3 ────────────────────────────────────────────────────
    public const int Lfo3Shape    = 5021;
    public const int Lfo3Rate     = 5022;
    public const int Lfo3Depth    = 5023;
    public const int Lfo3Retrigger = 5024;

    // ── OSC Routing Matrix (6 off-diagonal cells, row-major) ─────
    // Depths: (1→2)=6001 (1→3)=6002 (2→1)=6003 (2→3)=6004 (3→1)=6005 (3→2)=6006
    // Modes:  (1→2)=6011 (1→3)=6012 (2→1)=6013 (2→3)=6014 (3→1)=6015 (3→2)=6016
    public const int OscRouteDepth_1_2 = 6001;
    public const int OscRouteDepth_1_3 = 6002;
    public const int OscRouteDepth_2_1 = 6003;
    public const int OscRouteDepth_2_3 = 6004;
    public const int OscRouteDepth_3_1 = 6005;
    public const int OscRouteDepth_3_2 = 6006;
    public const int OscRouteMode_1_2  = 6011;  // stepped: 0=Mix 1=FM 2=RingMod
    public const int OscRouteMode_1_3  = 6012;
    public const int OscRouteMode_2_1  = 6013;
    public const int OscRouteMode_2_3  = 6014;
    public const int OscRouteMode_3_1  = 6015;
    public const int OscRouteMode_3_2  = 6016;

    // ── Mod Matrix route amounts (routes 0..31) ──────────────────
    // IDs 7001..7032  — amounts only; source/dest stay preset-only
    public static int ModRouteAmount(int routeIndex) => 7001 + routeIndex;

    // ── Effects (bypass and mix per slot 0..10) ──────────────────
    public static int EffectBypass(int slot) => 8001 + slot * 2;
    public static int EffectMix(int slot)    => 8002 + slot * 2;
}

/// <summary>
/// Builds and applies all VST3 parameters. Separates parameter definition
/// from OttoSynthPlugin to keep that file focused on audio processing.
/// </summary>
internal static class ParameterMap
{
    private static readonly string[] WarpTypeNames =
        Enum.GetNames<WavetableOscillator.WaveWarp>();
    private static readonly string[] FilterModeNames =
        Enum.GetNames<StateVariableFilter.FilterMode>();
    private static readonly string[] LfoShapeNames =
        Enum.GetNames<LfoGenerator.LfoShape>();
    private static readonly string[] GlideModeNames =
        Enum.GetNames<SynthVoice.GlideMode>();
    private static readonly string[] OscRoutingNames =
        Enum.GetNames<SynthVoice.OscRouting>();
    private static readonly string[] FilterRoutingNames =
        Enum.GetNames<SynthVoice.FilterRouting>();

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

        // OSC Routing depths and modes
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

        // Mod matrix route amounts (32 slots)
        for (int i = 0; i < 32; i++)
            Add(plugin, P.ModRouteAmount(i), $"Mod Route {i + 1}", -1, 1, 0);

        // Effects: bypass + mix per slot (11 possible slots)
        for (int s = 0; s < 11; s++)
        {
            AddToggle(plugin, P.EffectBypass(s), $"FX{s + 1} Bypass", 0);
            Add(plugin,       P.EffectMix(s),    $"FX{s + 1} Mix",    0, 1, 1);
        }
    }

    // ─── Apply (DAW → engine) ────────────────────────────────────

    /// <summary>
    /// Routes a VST3 parameter value change to the engine.
    /// <paramref name="getParam"/> reads the current ProcessValue of a parameter by ID —
    /// used when a parameter must read sibling values (e.g. Filter1Cutoff needs Resonance).
    /// </summary>
    public static void Apply(int id, double value, SynthEngine engine,
        Func<int, double> getParam)
    {
        switch (id)
        {
            // Master
            case P.MasterVolume:   engine.MasterVolume   = value; break;
            case P.PitchBendRange: engine.PitchBendRange = value; break;
            case P.GlideTime:
                engine.SetPortamento(value, engine.GlideMode);
                break;
            case P.GlideMode:
                engine.SetPortamento(engine.GlideTime,
                    (SynthVoice.GlideMode)(int)Math.Round(value));
                break;
            case P.Macro1: engine.SetMacro(0, value); break;
            case P.Macro2: engine.SetMacro(1, value); break;
            case P.Macro3: engine.SetMacro(2, value); break;
            case P.Macro4: engine.SetMacro(3, value); break;

            // OSC 1
            case P.Osc1Level:
            case P.Osc1Enabled:
                engine.SetOscillatorMix(1, getParam(P.Osc1Level),
                    getParam(P.Osc1Enabled) >= 0.5);
                break;
            case P.Osc1Position:
            case P.Osc1WarpAmount:
            case P.Osc1CoarseTune:
            case P.Osc1FineTune:
            case P.Osc1Pan:
                engine.SetOscillatorParams(1,
                    (int)Math.Round(getParam(P.Osc1CoarseTune)),
                    getParam(P.Osc1FineTune),
                    getParam(P.Osc1Position),
                    getParam(P.Osc1WarpAmount),
                    getParam(P.Osc1Pan));
                break;
            case P.Osc1WarpType:
                engine.SetOscillatorWarp(1,
                    (WavetableOscillator.WaveWarp)(int)Math.Round(value));
                break;
            case P.Osc1UnisonVoices:
            case P.Osc1UnisonDetune:
            case P.Osc1UnisonSpread:
                engine.SetOscillatorUnison(1,
                    (int)Math.Round(getParam(P.Osc1UnisonVoices)),
                    getParam(P.Osc1UnisonDetune),
                    getParam(P.Osc1UnisonSpread));
                break;

            // OSC 2
            case P.Osc2Level:
            case P.Osc2Enabled:
                engine.SetOscillatorMix(2, getParam(P.Osc2Level),
                    getParam(P.Osc2Enabled) >= 0.5);
                break;
            case P.Osc2Position:
            case P.Osc2WarpAmount:
            case P.Osc2CoarseTune:
            case P.Osc2FineTune:
            case P.Osc2Pan:
                engine.SetOscillatorParams(2,
                    (int)Math.Round(getParam(P.Osc2CoarseTune)),
                    getParam(P.Osc2FineTune),
                    getParam(P.Osc2Position),
                    getParam(P.Osc2WarpAmount),
                    getParam(P.Osc2Pan));
                break;
            case P.Osc2WarpType:
                engine.SetOscillatorWarp(2,
                    (WavetableOscillator.WaveWarp)(int)Math.Round(value));
                break;
            case P.Osc2UnisonVoices:
            case P.Osc2UnisonDetune:
            case P.Osc2UnisonSpread:
                engine.SetOscillatorUnison(2,
                    (int)Math.Round(getParam(P.Osc2UnisonVoices)),
                    getParam(P.Osc2UnisonDetune),
                    getParam(P.Osc2UnisonSpread));
                break;

            // OSC 3
            case P.Osc3Level:
            case P.Osc3Enabled:
                engine.SetOscillatorMix(3, getParam(P.Osc3Level),
                    getParam(P.Osc3Enabled) >= 0.5);
                break;
            case P.Osc3Position:
            case P.Osc3WarpAmount:
            case P.Osc3CoarseTune:
            case P.Osc3FineTune:
            case P.Osc3Pan:
                engine.SetOscillatorParams(3,
                    (int)Math.Round(getParam(P.Osc3CoarseTune)),
                    getParam(P.Osc3FineTune),
                    getParam(P.Osc3Position),
                    getParam(P.Osc3WarpAmount),
                    getParam(P.Osc3Pan));
                break;
            case P.Osc3WarpType:
                engine.SetOscillatorWarp(3,
                    (WavetableOscillator.WaveWarp)(int)Math.Round(value));
                break;
            case P.Osc3UnisonVoices:
            case P.Osc3UnisonDetune:
            case P.Osc3UnisonSpread:
                engine.SetOscillatorUnison(3,
                    (int)Math.Round(getParam(P.Osc3UnisonVoices)),
                    getParam(P.Osc3UnisonDetune),
                    getParam(P.Osc3UnisonSpread));
                break;

            // Noise
            case P.NoiseLevel:
            case P.NoiseEnabled:
                double nl = getParam(P.NoiseLevel);
                bool ne = getParam(P.NoiseEnabled) >= 0.5;
                foreach (var voice in engine.VoiceManager.Voices)
                { voice.NoiseLevel = nl; voice.NoiseEnabled = ne; }
                break;

            // Filter 1
            case P.Filter1Mode:
            case P.Filter1Cutoff:
            case P.Filter1Resonance:
            case P.Filter1Drive:
            case P.Filter1Is24dB:
                engine.SetFilter(1,
                    (StateVariableFilter.FilterMode)(int)Math.Round(
                        getParam(P.Filter1Mode)),
                    getParam(P.Filter1Cutoff),
                    getParam(P.Filter1Resonance),
                    getParam(P.Filter1Drive),
                    getParam(P.Filter1Is24dB) >= 0.5);
                break;
            case P.Filter1KeyTracking:
                foreach (var voice in engine.VoiceManager.Voices)
                    voice.Filter1.KeyTracking = value;
                break;
            case P.Filter1FormantVowel:
                engine.SetFormantParams(1, value, getParam(P.Filter1FormantShift));
                break;
            case P.Filter1FormantShift:
                engine.SetFormantParams(1, getParam(P.Filter1FormantVowel), value);
                break;

            // Filter 2
            case P.Filter2Mode:
            case P.Filter2Cutoff:
            case P.Filter2Resonance:
            case P.Filter2Drive:
            case P.Filter2Is24dB:
                engine.SetFilter(2,
                    (StateVariableFilter.FilterMode)(int)Math.Round(
                        getParam(P.Filter2Mode)),
                    getParam(P.Filter2Cutoff),
                    getParam(P.Filter2Resonance),
                    getParam(P.Filter2Drive),
                    getParam(P.Filter2Is24dB) >= 0.5);
                break;
            case P.Filter2KeyTracking:
                foreach (var voice in engine.VoiceManager.Voices)
                    voice.Filter2.KeyTracking = value;
                break;
            case P.Filter2FormantVowel:
                engine.SetFormantParams(2, value, getParam(P.Filter2FormantShift));
                break;
            case P.Filter2FormantShift:
                engine.SetFormantParams(2, getParam(P.Filter2FormantVowel), value);
                break;

            case P.FilterRouting:
                engine.SetFilterRouting(
                    (SynthVoice.FilterRouting)(int)Math.Round(value));
                break;
            case P.FilterEnvAmount:
                engine.SetFilterEnvAmount(value);
                break;

            // Envelopes
            case P.EnvAmpAttack:
            case P.EnvAmpDecay:
            case P.EnvAmpSustain:
            case P.EnvAmpRelease:
                engine.SetEnvelope(
                    getParam(P.EnvAmpAttack),
                    getParam(P.EnvAmpDecay),
                    getParam(P.EnvAmpSustain),
                    getParam(P.EnvAmpRelease));
                break;

            case P.EnvFilterAttack:
            case P.EnvFilterDecay:
            case P.EnvFilterSustain:
            case P.EnvFilterRelease:
                engine.SetFilterEnvelope(
                    getParam(P.EnvFilterAttack),
                    getParam(P.EnvFilterDecay),
                    getParam(P.EnvFilterSustain),
                    getParam(P.EnvFilterRelease));
                break;

            case P.EnvFreeAttack:
            case P.EnvFreeDecay:
            case P.EnvFreeSustain:
            case P.EnvFreeRelease:
                engine.SetFreeEnvelope(
                    getParam(P.EnvFreeAttack),
                    getParam(P.EnvFreeDecay),
                    getParam(P.EnvFreeSustain),
                    getParam(P.EnvFreeRelease));
                break;

            // LFOs
            case P.Lfo1Shape:
                engine.SetLfo(1, (LfoGenerator.LfoShape)(int)Math.Round(value),
                    getParam(P.Lfo1Rate), getParam(P.Lfo1Depth),
                    getParam(P.Lfo1Retrigger) >= 0.5);
                break;
            case P.Lfo1Rate:
            case P.Lfo1Depth:
            case P.Lfo1Retrigger:
                engine.SetLfo(1,
                    (LfoGenerator.LfoShape)(int)Math.Round(getParam(P.Lfo1Shape)),
                    getParam(P.Lfo1Rate), getParam(P.Lfo1Depth),
                    getParam(P.Lfo1Retrigger) >= 0.5);
                break;

            case P.Lfo2Shape:
            case P.Lfo2Rate:
            case P.Lfo2Depth:
            case P.Lfo2Retrigger:
                engine.SetLfo(2,
                    (LfoGenerator.LfoShape)(int)Math.Round(getParam(P.Lfo2Shape)),
                    getParam(P.Lfo2Rate), getParam(P.Lfo2Depth),
                    getParam(P.Lfo2Retrigger) >= 0.5);
                break;

            case P.Lfo3Shape:
            case P.Lfo3Rate:
            case P.Lfo3Depth:
            case P.Lfo3Retrigger:
                engine.SetLfo(3,
                    (LfoGenerator.LfoShape)(int)Math.Round(getParam(P.Lfo3Shape)),
                    getParam(P.Lfo3Rate), getParam(P.Lfo3Depth),
                    getParam(P.Lfo3Retrigger) >= 0.5);
                break;

            // OSC Routing
            case P.OscRouteDepth_1_2: ApplyOscRoute(engine, getParam,1, 2); break;
            case P.OscRouteDepth_1_3: ApplyOscRoute(engine, getParam,1, 3); break;
            case P.OscRouteDepth_2_1: ApplyOscRoute(engine, getParam,2, 1); break;
            case P.OscRouteDepth_2_3: ApplyOscRoute(engine, getParam,2, 3); break;
            case P.OscRouteDepth_3_1: ApplyOscRoute(engine, getParam,3, 1); break;
            case P.OscRouteDepth_3_2: ApplyOscRoute(engine, getParam,3, 2); break;
            case P.OscRouteMode_1_2: ApplyOscRoute(engine, getParam,1, 2); break;
            case P.OscRouteMode_1_3: ApplyOscRoute(engine, getParam,1, 3); break;
            case P.OscRouteMode_2_1: ApplyOscRoute(engine, getParam,2, 1); break;
            case P.OscRouteMode_2_3: ApplyOscRoute(engine, getParam,2, 3); break;
            case P.OscRouteMode_3_1: ApplyOscRoute(engine, getParam,3, 1); break;
            case P.OscRouteMode_3_2: ApplyOscRoute(engine, getParam,3, 2); break;

            // Mod matrix amounts
            default:
                if (id >= 7001 && id <= 7032)
                {
                    int routeIdx = id - 7001;
                    engine.SetModRouteAmount(routeIdx, value);
                }
                // Effects bypass/mix — applied at render time from param store
                break;
        }
    }

    // ─── Capture (engine → param store) ─────────────────────────

    /// <summary>
    /// Reads the current engine state into all parameters' ProcessValue.
    /// Call this when loading a preset to keep param store consistent.
    /// </summary>
    public static void CaptureAll(SynthEngine engine,
        Dictionary<int, AudioPluginParameter> paramsById)
    {
        void Set(int id, double val)
        {
            if (paramsById.TryGetValue(id, out var p))
                p.ProcessValue = val;
        }

        var v0 = engine.VoiceManager.Voices[0];

        Set(P.MasterVolume,   engine.MasterVolume);
        Set(P.PitchBendRange, engine.PitchBendRange);
        Set(P.GlideTime,      engine.GlideTime);
        Set(P.GlideMode,      (double)engine.GlideMode);
        Set(P.Macro1,         engine.Macros[0]);
        Set(P.Macro2,         engine.Macros[1]);
        Set(P.Macro3,         engine.Macros[2]);
        Set(P.Macro4,         engine.Macros[3]);

        // OSC 1
        Set(P.Osc1Level,        v0.Osc1Level);
        Set(P.Osc1Enabled,      v0.Osc1Enabled ? 1 : 0);
        Set(P.Osc1Position,     v0.Osc1.WavetablePosition);
        Set(P.Osc1WarpAmount,   v0.Osc1.WarpAmount);
        Set(P.Osc1WarpType,     (double)v0.Osc1.Warp);
        Set(P.Osc1CoarseTune,   v0.Osc1.CoarseTune);
        Set(P.Osc1FineTune,     v0.Osc1.FineTune);
        Set(P.Osc1Pan,          v0.Osc1.Pan);
        Set(P.Osc1UnisonVoices, v0.Unison1.VoiceCount);
        Set(P.Osc1UnisonDetune, v0.Unison1.DetuneSpread);
        Set(P.Osc1UnisonSpread, v0.Unison1.StereoSpread);

        // OSC 2
        Set(P.Osc2Level,        v0.Osc2Level);
        Set(P.Osc2Enabled,      v0.Osc2Enabled ? 1 : 0);
        Set(P.Osc2Position,     v0.Osc2.WavetablePosition);
        Set(P.Osc2WarpAmount,   v0.Osc2.WarpAmount);
        Set(P.Osc2WarpType,     (double)v0.Osc2.Warp);
        Set(P.Osc2CoarseTune,   v0.Osc2.CoarseTune);
        Set(P.Osc2FineTune,     v0.Osc2.FineTune);
        Set(P.Osc2Pan,          v0.Osc2.Pan);
        Set(P.Osc2UnisonVoices, v0.Unison2.VoiceCount);
        Set(P.Osc2UnisonDetune, v0.Unison2.DetuneSpread);
        Set(P.Osc2UnisonSpread, v0.Unison2.StereoSpread);

        // OSC 3
        Set(P.Osc3Level,        v0.Osc3Level);
        Set(P.Osc3Enabled,      v0.Osc3Enabled ? 1 : 0);
        Set(P.Osc3Position,     v0.Osc3.WavetablePosition);
        Set(P.Osc3WarpAmount,   v0.Osc3.WarpAmount);
        Set(P.Osc3WarpType,     (double)v0.Osc3.Warp);
        Set(P.Osc3CoarseTune,   v0.Osc3.CoarseTune);
        Set(P.Osc3FineTune,     v0.Osc3.FineTune);
        Set(P.Osc3Pan,          v0.Osc3.Pan);
        Set(P.Osc3UnisonVoices, v0.Unison3.VoiceCount);
        Set(P.Osc3UnisonDetune, v0.Unison3.DetuneSpread);
        Set(P.Osc3UnisonSpread, v0.Unison3.StereoSpread);

        Set(P.NoiseLevel,   v0.NoiseLevel);
        Set(P.NoiseEnabled, v0.NoiseEnabled ? 1 : 0);

        // Filters
        Set(P.Filter1Mode,        (double)v0.Filter1.Mode);
        Set(P.Filter1Cutoff,      v0.Filter1.Cutoff);
        Set(P.Filter1Resonance,   v0.Filter1.Resonance);
        Set(P.Filter1Drive,       v0.Filter1.Drive);
        Set(P.Filter1Is24dB,       v0.Filter1.Is24dB ? 1 : 0);
        Set(P.Filter1KeyTracking,  v0.Filter1.KeyTracking);
        Set(P.Filter1FormantVowel, v0.Filter1.FormantVowel);
        Set(P.Filter1FormantShift, v0.Filter1.FormantShift);

        Set(P.Filter2Mode,         (double)v0.Filter2.Mode);
        Set(P.Filter2Cutoff,       v0.Filter2.Cutoff);
        Set(P.Filter2Resonance,    v0.Filter2.Resonance);
        Set(P.Filter2Drive,        v0.Filter2.Drive);
        Set(P.Filter2Is24dB,       v0.Filter2.Is24dB ? 1 : 0);
        Set(P.Filter2KeyTracking,  v0.Filter2.KeyTracking);
        Set(P.Filter2FormantVowel, v0.Filter2.FormantVowel);
        Set(P.Filter2FormantShift, v0.Filter2.FormantShift);

        Set(P.FilterRouting,   (double)v0.Routing);
        Set(P.FilterEnvAmount, v0.FilterEnvAmount);

        // Envelopes
        Set(P.EnvAmpAttack,  v0.EnvAmp.AttackTime);
        Set(P.EnvAmpDecay,   v0.EnvAmp.DecayTime);
        Set(P.EnvAmpSustain, v0.EnvAmp.SustainLevel);
        Set(P.EnvAmpRelease, v0.EnvAmp.ReleaseTime);

        Set(P.EnvFilterAttack,  v0.EnvFilter.AttackTime);
        Set(P.EnvFilterDecay,   v0.EnvFilter.DecayTime);
        Set(P.EnvFilterSustain, v0.EnvFilter.SustainLevel);
        Set(P.EnvFilterRelease, v0.EnvFilter.ReleaseTime);

        Set(P.EnvFreeAttack,  v0.EnvFree.AttackTime);
        Set(P.EnvFreeDecay,   v0.EnvFree.DecayTime);
        Set(P.EnvFreeSustain, v0.EnvFree.SustainLevel);
        Set(P.EnvFreeRelease, v0.EnvFree.ReleaseTime);

        // LFOs
        Set(P.Lfo1Shape,    (double)v0.Lfo1.Shape);
        Set(P.Lfo1Rate,     v0.Lfo1.Rate);
        Set(P.Lfo1Depth,    v0.Lfo1.Depth);
        Set(P.Lfo1Retrigger, v0.Lfo1.Retrigger ? 1 : 0);

        Set(P.Lfo2Shape,    (double)v0.Lfo2.Shape);
        Set(P.Lfo2Rate,     v0.Lfo2.Rate);
        Set(P.Lfo2Depth,    v0.Lfo2.Depth);
        Set(P.Lfo2Retrigger, v0.Lfo2.Retrigger ? 1 : 0);

        Set(P.Lfo3Shape,    (double)v0.Lfo3.Shape);
        Set(P.Lfo3Rate,     v0.Lfo3.Rate);
        Set(P.Lfo3Depth,    v0.Lfo3.Depth);
        Set(P.Lfo3Retrigger, v0.Lfo3.Retrigger ? 1 : 0);

        // Routing
        void SetRoute(int mod, int car, int depthId, int modeId)
        {
            Set(depthId, engine.GetOscillatorFmDepth(mod, car));
            Set(modeId,  (double)engine.GetOscillatorRouting(mod, car));
        }
        SetRoute(1, 2, P.OscRouteDepth_1_2, P.OscRouteMode_1_2);
        SetRoute(1, 3, P.OscRouteDepth_1_3, P.OscRouteMode_1_3);
        SetRoute(2, 1, P.OscRouteDepth_2_1, P.OscRouteMode_2_1);
        SetRoute(2, 3, P.OscRouteDepth_2_3, P.OscRouteMode_2_3);
        SetRoute(3, 1, P.OscRouteDepth_3_1, P.OscRouteMode_3_1);
        SetRoute(3, 2, P.OscRouteDepth_3_2, P.OscRouteMode_3_2);

        // Mod matrix
        var routes = v0.ModMatrix.Routes;
        for (int i = 0; i < Math.Min(32, routes.Length); i++)
            Set(P.ModRouteAmount(i), routes[i].Amount);

        // Effects
        var fxList = engine.Effects.Effects;
        for (int s = 0; s < Math.Min(11, fxList.Count); s++)
        {
            Set(P.EffectBypass(s), fxList[s].Bypass ? 1 : 0);
            Set(P.EffectMix(s),    fxList[s].Mix);
        }
    }

    // ─── Private helpers ─────────────────────────────────────────

    private static void Add(AudioPluginBase plugin, int id, string name,
        double min, double max, double def,
        string fmt = "F2", double rangePower = 1.0)
    {
        var p = new OttoParameter
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
        };
        plugin.AddParameter(p);
    }

    private static void AddEnv(AudioPluginBase plugin, int id, string name,
        double min, double max, double def)
        => Add(plugin, id, name, min, max, def, "F3", rangePower: 3.0);

    private static void AddToggle(AudioPluginBase plugin, int id, string name, double def)
        => Add(plugin, id, name, 0, 1, def, "F0");

    private static void AddStepped(AudioPluginBase plugin, int id, string name,
        int maxValue, int def)
        => Add(plugin, id, name, 0, maxValue, def, "F0");

    private static void ApplyOscRoute(SynthEngine engine, Func<int, double> getParam, int mod, int car)
    {
        int depthId = (mod, car) switch
        {
            (1,2) => P.OscRouteDepth_1_2, (1,3) => P.OscRouteDepth_1_3,
            (2,1) => P.OscRouteDepth_2_1, (2,3) => P.OscRouteDepth_2_3,
            (3,1) => P.OscRouteDepth_3_1, (3,2) => P.OscRouteDepth_3_2,
            _ => 0
        };
        int modeId = (mod, car) switch
        {
            (1,2) => P.OscRouteMode_1_2, (1,3) => P.OscRouteMode_1_3,
            (2,1) => P.OscRouteMode_2_1, (2,3) => P.OscRouteMode_2_3,
            (3,1) => P.OscRouteMode_3_1, (3,2) => P.OscRouteMode_3_2,
            _ => 0
        };
        if (depthId == 0) return;

        var routingMode = (SynthVoice.OscRouting)(int)Math.Round(getParam(modeId));
        double depth = getParam(depthId);
        engine.SetOscillatorRouting(mod, car, routingMode, depth);
    }
}

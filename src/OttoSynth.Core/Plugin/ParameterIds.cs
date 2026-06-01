namespace OttoSynth.Core.Plugin;

/// <summary>
/// Stable integer IDs for every automatable parameter in OttoSynth.
/// IDs MUST never be reordered or reused — hosts and preset files serialize them by ID.
///
/// Format-agnostic: VST3 and CLAP both use these IDs so presets are interchangeable.
///
/// Numbering scheme:
///   1xxx = Master / Global
///   2xxx = OSC 1, 21xx = OSC 2, 22xx = OSC 3, 23xx = Noise
///   3xxx = Filter 1, 31xx = Filter 2, 32xx = Filter Routing
///   4xxx = Envelopes (4001-4007 Amp, 4011-4017 Filter, 4021-4027 Free)
///   5xxx = LFOs (5001-5004 LFO1, 5011-5014 LFO2, 5021-5024 LFO3)
///   6xxx = OSC Routing matrix depths/modes
///   7xxx = Mod Matrix route amounts (7001-7032)
///   8xxx = Effects (by slot index, 8001 + slot*2 = bypass, +1 = mix)
/// </summary>
public static class ParameterIds
{
    // ── Master / Global ────────────────────────────────────────────
    public const int MasterVolume   = 1001;
    public const int PitchBendRange = 1002;
    public const int GlideTime      = 1003;
    public const int GlideMode      = 1004;
    public const int Macro1         = 1005;
    public const int Macro2         = 1006;
    public const int Macro3         = 1007;
    public const int Macro4         = 1008;

    // ── OSC 1 ────────────────────────────────────────────────────
    public const int Osc1Level        = 2001;
    public const int Osc1Enabled      = 2002;
    public const int Osc1Position     = 2003;
    public const int Osc1WarpAmount   = 2004;
    public const int Osc1WarpType     = 2005;
    public const int Osc1CoarseTune   = 2006;
    public const int Osc1FineTune     = 2007;
    public const int Osc1Pan          = 2008;
    public const int Osc1UnisonVoices = 2009;
    public const int Osc1UnisonDetune = 2010;
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
    public const int Filter1Mode           = 3001;
    public const int Filter1Cutoff         = 3002;
    public const int Filter1Resonance      = 3003;
    public const int Filter1Drive          = 3004;
    public const int Filter1Is24dB         = 3005;
    public const int Filter1KeyTracking    = 3006;
    public const int Filter1FormantVowel   = 3007;
    public const int Filter1FormantShift   = 3008;

    // ── Filter 2 ─────────────────────────────────────────────────
    public const int Filter2Mode           = 3101;
    public const int Filter2Cutoff         = 3102;
    public const int Filter2Resonance      = 3103;
    public const int Filter2Drive          = 3104;
    public const int Filter2Is24dB         = 3105;
    public const int Filter2KeyTracking    = 3106;
    public const int Filter2FormantVowel   = 3107;
    public const int Filter2FormantShift   = 3108;

    // ── Filter Routing ───────────────────────────────────────────
    public const int FilterRouting    = 3201;
    public const int FilterEnvAmount  = 3202;

    // ── Envelope 1 (Amp) ─────────────────────────────────────────
    public const int EnvAmpAttack   = 4001;
    public const int EnvAmpDecay    = 4002;
    public const int EnvAmpSustain  = 4003;
    public const int EnvAmpRelease  = 4004;

    // ── Envelope 2 (Filter) ──────────────────────────────────────
    public const int EnvFilterAttack   = 4011;
    public const int EnvFilterDecay    = 4012;
    public const int EnvFilterSustain  = 4013;
    public const int EnvFilterRelease  = 4014;

    // ── Envelope 3 (Free) ────────────────────────────────────────
    public const int EnvFreeAttack   = 4021;
    public const int EnvFreeDecay    = 4022;
    public const int EnvFreeSustain  = 4023;
    public const int EnvFreeRelease  = 4024;

    // ── LFO 1 ────────────────────────────────────────────────────
    public const int Lfo1Shape    = 5001;
    public const int Lfo1Rate     = 5002;
    public const int Lfo1Depth    = 5003;
    public const int Lfo1Retrigger = 5004;

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

    // ── OSC Routing Matrix ───────────────────────────────────────
    public const int OscRouteDepth_1_2 = 6001;
    public const int OscRouteDepth_1_3 = 6002;
    public const int OscRouteDepth_2_1 = 6003;
    public const int OscRouteDepth_2_3 = 6004;
    public const int OscRouteDepth_3_1 = 6005;
    public const int OscRouteDepth_3_2 = 6006;
    public const int OscRouteMode_1_2  = 6011;
    public const int OscRouteMode_1_3  = 6012;
    public const int OscRouteMode_2_1  = 6013;
    public const int OscRouteMode_2_3  = 6014;
    public const int OscRouteMode_3_1  = 6015;
    public const int OscRouteMode_3_2  = 6016;

    // ── Mod Matrix route amounts (routes 0..31) ──────────────────
    public const int ModRouteAmount_Base = 7001;
    public static int ModRouteAmount(int routeIndex) => ModRouteAmount_Base + routeIndex;

    // ── Effects (bypass and mix per slot 0..10) ──────────────────
    public static int EffectBypass(int slot) => 8001 + slot * 2;
    public static int EffectMix(int slot)    => 8002 + slot * 2;

    /// <summary>Total number of slots used by effects (11 slots × 2 params).</summary>
    public const int EffectsSlotCount = 11;
}

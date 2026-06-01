using System;
using OttoSynth.Core.DSP.Filters;
using OttoSynth.Core.DSP.Modulation;
using OttoSynth.Core.DSP.Oscillators;
using OttoSynth.Core.Voice;
using P = OttoSynth.Core.Plugin.ParameterIds;

namespace OttoSynth.Core.Plugin;

/// <summary>
/// Host-agnostic dispatcher that maps parameter ID changes to <see cref="SynthEngine"/> calls
/// and reads the engine state back to the host's parameter store.
///
/// Used by both the VST3 plugin (AudioPlugSharp) and the CLAP plugin. The host SDK adapter
/// implements <see cref="IPluginHost"/> with its own parameter storage; this class contains
/// no host-specific code.
/// </summary>
public static class ParameterDispatcher
{
    // ─── Apply (host → engine) ────────────────────────────────────

    /// <summary>
    /// Routes a parameter value change to the engine. Some setters need sibling
    /// values (e.g. cutoff + resonance) which are read via <paramref name="host"/>.
    /// </summary>
    public static void Apply(int id, double value, SynthEngine engine, IPluginHost host)
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
                engine.SetOscillatorMix(1, host.GetParameterValue(P.Osc1Level),
                    host.GetParameterValue(P.Osc1Enabled) >= 0.5);
                break;
            case P.Osc1Position:
            case P.Osc1WarpAmount:
            case P.Osc1CoarseTune:
            case P.Osc1FineTune:
            case P.Osc1Pan:
                engine.SetOscillatorParams(1,
                    (int)Math.Round(host.GetParameterValue(P.Osc1CoarseTune)),
                    host.GetParameterValue(P.Osc1FineTune),
                    host.GetParameterValue(P.Osc1Position),
                    host.GetParameterValue(P.Osc1WarpAmount),
                    host.GetParameterValue(P.Osc1Pan));
                break;
            case P.Osc1WarpType:
                engine.SetOscillatorWarp(1,
                    (WavetableOscillator.WaveWarp)(int)Math.Round(value));
                break;
            case P.Osc1UnisonVoices:
            case P.Osc1UnisonDetune:
            case P.Osc1UnisonSpread:
                engine.SetOscillatorUnison(1,
                    (int)Math.Round(host.GetParameterValue(P.Osc1UnisonVoices)),
                    host.GetParameterValue(P.Osc1UnisonDetune),
                    host.GetParameterValue(P.Osc1UnisonSpread));
                break;

            // OSC 2
            case P.Osc2Level:
            case P.Osc2Enabled:
                engine.SetOscillatorMix(2, host.GetParameterValue(P.Osc2Level),
                    host.GetParameterValue(P.Osc2Enabled) >= 0.5);
                break;
            case P.Osc2Position:
            case P.Osc2WarpAmount:
            case P.Osc2CoarseTune:
            case P.Osc2FineTune:
            case P.Osc2Pan:
                engine.SetOscillatorParams(2,
                    (int)Math.Round(host.GetParameterValue(P.Osc2CoarseTune)),
                    host.GetParameterValue(P.Osc2FineTune),
                    host.GetParameterValue(P.Osc2Position),
                    host.GetParameterValue(P.Osc2WarpAmount),
                    host.GetParameterValue(P.Osc2Pan));
                break;
            case P.Osc2WarpType:
                engine.SetOscillatorWarp(2,
                    (WavetableOscillator.WaveWarp)(int)Math.Round(value));
                break;
            case P.Osc2UnisonVoices:
            case P.Osc2UnisonDetune:
            case P.Osc2UnisonSpread:
                engine.SetOscillatorUnison(2,
                    (int)Math.Round(host.GetParameterValue(P.Osc2UnisonVoices)),
                    host.GetParameterValue(P.Osc2UnisonDetune),
                    host.GetParameterValue(P.Osc2UnisonSpread));
                break;

            // OSC 3
            case P.Osc3Level:
            case P.Osc3Enabled:
                engine.SetOscillatorMix(3, host.GetParameterValue(P.Osc3Level),
                    host.GetParameterValue(P.Osc3Enabled) >= 0.5);
                break;
            case P.Osc3Position:
            case P.Osc3WarpAmount:
            case P.Osc3CoarseTune:
            case P.Osc3FineTune:
            case P.Osc3Pan:
                engine.SetOscillatorParams(3,
                    (int)Math.Round(host.GetParameterValue(P.Osc3CoarseTune)),
                    host.GetParameterValue(P.Osc3FineTune),
                    host.GetParameterValue(P.Osc3Position),
                    host.GetParameterValue(P.Osc3WarpAmount),
                    host.GetParameterValue(P.Osc3Pan));
                break;
            case P.Osc3WarpType:
                engine.SetOscillatorWarp(3,
                    (WavetableOscillator.WaveWarp)(int)Math.Round(value));
                break;
            case P.Osc3UnisonVoices:
            case P.Osc3UnisonDetune:
            case P.Osc3UnisonSpread:
                engine.SetOscillatorUnison(3,
                    (int)Math.Round(host.GetParameterValue(P.Osc3UnisonVoices)),
                    host.GetParameterValue(P.Osc3UnisonDetune),
                    host.GetParameterValue(P.Osc3UnisonSpread));
                break;

            // Noise
            case P.NoiseLevel:
            case P.NoiseEnabled:
                double nl = host.GetParameterValue(P.NoiseLevel);
                bool ne = host.GetParameterValue(P.NoiseEnabled) >= 0.5;
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
                        host.GetParameterValue(P.Filter1Mode)),
                    host.GetParameterValue(P.Filter1Cutoff),
                    host.GetParameterValue(P.Filter1Resonance),
                    host.GetParameterValue(P.Filter1Drive),
                    host.GetParameterValue(P.Filter1Is24dB) >= 0.5);
                break;
            case P.Filter1KeyTracking:
                foreach (var voice in engine.VoiceManager.Voices)
                    voice.Filter1.KeyTracking = value;
                break;
            case P.Filter1FormantVowel:
                engine.SetFormantParams(1, value, host.GetParameterValue(P.Filter1FormantShift));
                break;
            case P.Filter1FormantShift:
                engine.SetFormantParams(1, host.GetParameterValue(P.Filter1FormantVowel), value);
                break;

            // Filter 2
            case P.Filter2Mode:
            case P.Filter2Cutoff:
            case P.Filter2Resonance:
            case P.Filter2Drive:
            case P.Filter2Is24dB:
                engine.SetFilter(2,
                    (StateVariableFilter.FilterMode)(int)Math.Round(
                        host.GetParameterValue(P.Filter2Mode)),
                    host.GetParameterValue(P.Filter2Cutoff),
                    host.GetParameterValue(P.Filter2Resonance),
                    host.GetParameterValue(P.Filter2Drive),
                    host.GetParameterValue(P.Filter2Is24dB) >= 0.5);
                break;
            case P.Filter2KeyTracking:
                foreach (var voice in engine.VoiceManager.Voices)
                    voice.Filter2.KeyTracking = value;
                break;
            case P.Filter2FormantVowel:
                engine.SetFormantParams(2, value, host.GetParameterValue(P.Filter2FormantShift));
                break;
            case P.Filter2FormantShift:
                engine.SetFormantParams(2, host.GetParameterValue(P.Filter2FormantVowel), value);
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
                    host.GetParameterValue(P.EnvAmpAttack),
                    host.GetParameterValue(P.EnvAmpDecay),
                    host.GetParameterValue(P.EnvAmpSustain),
                    host.GetParameterValue(P.EnvAmpRelease));
                break;

            case P.EnvFilterAttack:
            case P.EnvFilterDecay:
            case P.EnvFilterSustain:
            case P.EnvFilterRelease:
                engine.SetFilterEnvelope(
                    host.GetParameterValue(P.EnvFilterAttack),
                    host.GetParameterValue(P.EnvFilterDecay),
                    host.GetParameterValue(P.EnvFilterSustain),
                    host.GetParameterValue(P.EnvFilterRelease));
                break;

            case P.EnvFreeAttack:
            case P.EnvFreeDecay:
            case P.EnvFreeSustain:
            case P.EnvFreeRelease:
                engine.SetFreeEnvelope(
                    host.GetParameterValue(P.EnvFreeAttack),
                    host.GetParameterValue(P.EnvFreeDecay),
                    host.GetParameterValue(P.EnvFreeSustain),
                    host.GetParameterValue(P.EnvFreeRelease));
                break;

            // LFOs
            case P.Lfo1Shape:
            case P.Lfo1Rate:
            case P.Lfo1Depth:
            case P.Lfo1Retrigger:
                engine.SetLfo(1,
                    (LfoGenerator.LfoShape)(int)Math.Round(host.GetParameterValue(P.Lfo1Shape)),
                    host.GetParameterValue(P.Lfo1Rate),
                    host.GetParameterValue(P.Lfo1Depth),
                    host.GetParameterValue(P.Lfo1Retrigger) >= 0.5);
                break;

            case P.Lfo2Shape:
            case P.Lfo2Rate:
            case P.Lfo2Depth:
            case P.Lfo2Retrigger:
                engine.SetLfo(2,
                    (LfoGenerator.LfoShape)(int)Math.Round(host.GetParameterValue(P.Lfo2Shape)),
                    host.GetParameterValue(P.Lfo2Rate),
                    host.GetParameterValue(P.Lfo2Depth),
                    host.GetParameterValue(P.Lfo2Retrigger) >= 0.5);
                break;

            case P.Lfo3Shape:
            case P.Lfo3Rate:
            case P.Lfo3Depth:
            case P.Lfo3Retrigger:
                engine.SetLfo(3,
                    (LfoGenerator.LfoShape)(int)Math.Round(host.GetParameterValue(P.Lfo3Shape)),
                    host.GetParameterValue(P.Lfo3Rate),
                    host.GetParameterValue(P.Lfo3Depth),
                    host.GetParameterValue(P.Lfo3Retrigger) >= 0.5);
                break;

            // OSC Routing
            case P.OscRouteDepth_1_2:
            case P.OscRouteMode_1_2: ApplyOscRoute(engine, host, 1, 2); break;
            case P.OscRouteDepth_1_3:
            case P.OscRouteMode_1_3: ApplyOscRoute(engine, host, 1, 3); break;
            case P.OscRouteDepth_2_1:
            case P.OscRouteMode_2_1: ApplyOscRoute(engine, host, 2, 1); break;
            case P.OscRouteDepth_2_3:
            case P.OscRouteMode_2_3: ApplyOscRoute(engine, host, 2, 3); break;
            case P.OscRouteDepth_3_1:
            case P.OscRouteMode_3_1: ApplyOscRoute(engine, host, 3, 1); break;
            case P.OscRouteDepth_3_2:
            case P.OscRouteMode_3_2: ApplyOscRoute(engine, host, 3, 2); break;

            // Mod matrix amounts
            default:
                if (id >= P.ModRouteAmount(0) && id <= P.ModRouteAmount(31))
                {
                    int routeIdx = id - P.ModRouteAmount(0);
                    engine.SetModRouteAmount(routeIdx, value);
                }
                // Effects bypass/mix — applied at render time from the host's param store
                break;
        }
    }

    // ─── Capture (engine → host) ─────────────────────────────────

    /// <summary>
    /// Reads the engine state into the host's parameter store. Call after preset load
    /// to keep the host UI in sync with the engine.
    /// </summary>
    public static void CaptureAll(SynthEngine engine, IPluginHost host)
    {
        var v0 = engine.VoiceManager.Voices[0];

        host.SetParameterValue(P.MasterVolume,   engine.MasterVolume);
        host.SetParameterValue(P.PitchBendRange, engine.PitchBendRange);
        host.SetParameterValue(P.GlideTime,      engine.GlideTime);
        host.SetParameterValue(P.GlideMode,      (double)engine.GlideMode);
        host.SetParameterValue(P.Macro1,         engine.Macros[0]);
        host.SetParameterValue(P.Macro2,         engine.Macros[1]);
        host.SetParameterValue(P.Macro3,         engine.Macros[2]);
        host.SetParameterValue(P.Macro4,         engine.Macros[3]);

        // OSC 1
        host.SetParameterValue(P.Osc1Level,        v0.Osc1Level);
        host.SetParameterValue(P.Osc1Enabled,      v0.Osc1Enabled ? 1 : 0);
        host.SetParameterValue(P.Osc1Position,     v0.Osc1.WavetablePosition);
        host.SetParameterValue(P.Osc1WarpAmount,   v0.Osc1.WarpAmount);
        host.SetParameterValue(P.Osc1WarpType,     (double)v0.Osc1.Warp);
        host.SetParameterValue(P.Osc1CoarseTune,   v0.Osc1.CoarseTune);
        host.SetParameterValue(P.Osc1FineTune,     v0.Osc1.FineTune);
        host.SetParameterValue(P.Osc1Pan,          v0.Osc1.Pan);
        host.SetParameterValue(P.Osc1UnisonVoices, v0.Unison1.VoiceCount);
        host.SetParameterValue(P.Osc1UnisonDetune, v0.Unison1.DetuneSpread);
        host.SetParameterValue(P.Osc1UnisonSpread, v0.Unison1.StereoSpread);

        // OSC 2
        host.SetParameterValue(P.Osc2Level,        v0.Osc2Level);
        host.SetParameterValue(P.Osc2Enabled,      v0.Osc2Enabled ? 1 : 0);
        host.SetParameterValue(P.Osc2Position,     v0.Osc2.WavetablePosition);
        host.SetParameterValue(P.Osc2WarpAmount,   v0.Osc2.WarpAmount);
        host.SetParameterValue(P.Osc2WarpType,     (double)v0.Osc2.Warp);
        host.SetParameterValue(P.Osc2CoarseTune,   v0.Osc2.CoarseTune);
        host.SetParameterValue(P.Osc2FineTune,     v0.Osc2.FineTune);
        host.SetParameterValue(P.Osc2Pan,          v0.Osc2.Pan);
        host.SetParameterValue(P.Osc2UnisonVoices, v0.Unison2.VoiceCount);
        host.SetParameterValue(P.Osc2UnisonDetune, v0.Unison2.DetuneSpread);
        host.SetParameterValue(P.Osc2UnisonSpread, v0.Unison2.StereoSpread);

        // OSC 3
        host.SetParameterValue(P.Osc3Level,        v0.Osc3Level);
        host.SetParameterValue(P.Osc3Enabled,      v0.Osc3Enabled ? 1 : 0);
        host.SetParameterValue(P.Osc3Position,     v0.Osc3.WavetablePosition);
        host.SetParameterValue(P.Osc3WarpAmount,   v0.Osc3.WarpAmount);
        host.SetParameterValue(P.Osc3WarpType,     (double)v0.Osc3.Warp);
        host.SetParameterValue(P.Osc3CoarseTune,   v0.Osc3.CoarseTune);
        host.SetParameterValue(P.Osc3FineTune,     v0.Osc3.FineTune);
        host.SetParameterValue(P.Osc3Pan,          v0.Osc3.Pan);
        host.SetParameterValue(P.Osc3UnisonVoices, v0.Unison3.VoiceCount);
        host.SetParameterValue(P.Osc3UnisonDetune, v0.Unison3.DetuneSpread);
        host.SetParameterValue(P.Osc3UnisonSpread, v0.Unison3.StereoSpread);

        host.SetParameterValue(P.NoiseLevel,   v0.NoiseLevel);
        host.SetParameterValue(P.NoiseEnabled, v0.NoiseEnabled ? 1 : 0);

        // Filters
        host.SetParameterValue(P.Filter1Mode,         (double)v0.Filter1.Mode);
        host.SetParameterValue(P.Filter1Cutoff,       v0.Filter1.Cutoff);
        host.SetParameterValue(P.Filter1Resonance,    v0.Filter1.Resonance);
        host.SetParameterValue(P.Filter1Drive,        v0.Filter1.Drive);
        host.SetParameterValue(P.Filter1Is24dB,       v0.Filter1.Is24dB ? 1 : 0);
        host.SetParameterValue(P.Filter1KeyTracking,  v0.Filter1.KeyTracking);
        host.SetParameterValue(P.Filter1FormantVowel, v0.Filter1.FormantVowel);
        host.SetParameterValue(P.Filter1FormantShift, v0.Filter1.FormantShift);

        host.SetParameterValue(P.Filter2Mode,         (double)v0.Filter2.Mode);
        host.SetParameterValue(P.Filter2Cutoff,       v0.Filter2.Cutoff);
        host.SetParameterValue(P.Filter2Resonance,    v0.Filter2.Resonance);
        host.SetParameterValue(P.Filter2Drive,        v0.Filter2.Drive);
        host.SetParameterValue(P.Filter2Is24dB,       v0.Filter2.Is24dB ? 1 : 0);
        host.SetParameterValue(P.Filter2KeyTracking,  v0.Filter2.KeyTracking);
        host.SetParameterValue(P.Filter2FormantVowel, v0.Filter2.FormantVowel);
        host.SetParameterValue(P.Filter2FormantShift, v0.Filter2.FormantShift);

        host.SetParameterValue(P.FilterRouting,   (double)v0.Routing);
        host.SetParameterValue(P.FilterEnvAmount, v0.FilterEnvAmount);

        // Envelopes
        host.SetParameterValue(P.EnvAmpAttack,  v0.EnvAmp.AttackTime);
        host.SetParameterValue(P.EnvAmpDecay,   v0.EnvAmp.DecayTime);
        host.SetParameterValue(P.EnvAmpSustain, v0.EnvAmp.SustainLevel);
        host.SetParameterValue(P.EnvAmpRelease, v0.EnvAmp.ReleaseTime);

        host.SetParameterValue(P.EnvFilterAttack,  v0.EnvFilter.AttackTime);
        host.SetParameterValue(P.EnvFilterDecay,   v0.EnvFilter.DecayTime);
        host.SetParameterValue(P.EnvFilterSustain, v0.EnvFilter.SustainLevel);
        host.SetParameterValue(P.EnvFilterRelease, v0.EnvFilter.ReleaseTime);

        host.SetParameterValue(P.EnvFreeAttack,  v0.EnvFree.AttackTime);
        host.SetParameterValue(P.EnvFreeDecay,   v0.EnvFree.DecayTime);
        host.SetParameterValue(P.EnvFreeSustain, v0.EnvFree.SustainLevel);
        host.SetParameterValue(P.EnvFreeRelease, v0.EnvFree.ReleaseTime);

        // LFOs
        host.SetParameterValue(P.Lfo1Shape,     (double)v0.Lfo1.Shape);
        host.SetParameterValue(P.Lfo1Rate,      v0.Lfo1.Rate);
        host.SetParameterValue(P.Lfo1Depth,     v0.Lfo1.Depth);
        host.SetParameterValue(P.Lfo1Retrigger, v0.Lfo1.Retrigger ? 1 : 0);

        host.SetParameterValue(P.Lfo2Shape,     (double)v0.Lfo2.Shape);
        host.SetParameterValue(P.Lfo2Rate,      v0.Lfo2.Rate);
        host.SetParameterValue(P.Lfo2Depth,     v0.Lfo2.Depth);
        host.SetParameterValue(P.Lfo2Retrigger, v0.Lfo2.Retrigger ? 1 : 0);

        host.SetParameterValue(P.Lfo3Shape,     (double)v0.Lfo3.Shape);
        host.SetParameterValue(P.Lfo3Rate,      v0.Lfo3.Rate);
        host.SetParameterValue(P.Lfo3Depth,     v0.Lfo3.Depth);
        host.SetParameterValue(P.Lfo3Retrigger, v0.Lfo3.Retrigger ? 1 : 0);

        // OSC routing
        SetRoute(engine, host, 1, 2, P.OscRouteDepth_1_2, P.OscRouteMode_1_2);
        SetRoute(engine, host, 1, 3, P.OscRouteDepth_1_3, P.OscRouteMode_1_3);
        SetRoute(engine, host, 2, 1, P.OscRouteDepth_2_1, P.OscRouteMode_2_1);
        SetRoute(engine, host, 2, 3, P.OscRouteDepth_2_3, P.OscRouteMode_2_3);
        SetRoute(engine, host, 3, 1, P.OscRouteDepth_3_1, P.OscRouteMode_3_1);
        SetRoute(engine, host, 3, 2, P.OscRouteDepth_3_2, P.OscRouteMode_3_2);

        // Mod matrix
        var routes = v0.ModMatrix.Routes;
        for (int i = 0; i < Math.Min(32, routes.Length); i++)
            host.SetParameterValue(P.ModRouteAmount(i), routes[i].Amount);

        // Effects
        var fxList = engine.Effects.Effects;
        for (int s = 0; s < Math.Min(P.EffectsSlotCount, fxList.Count); s++)
        {
            host.SetParameterValue(P.EffectBypass(s), fxList[s].Bypass ? 1 : 0);
            host.SetParameterValue(P.EffectMix(s),    fxList[s].Mix);
        }
    }

    // ─── Private helpers ─────────────────────────────────────────

    private static void SetRoute(SynthEngine engine, IPluginHost host,
        int mod, int car, int depthId, int modeId)
    {
        host.SetParameterValue(depthId, engine.GetOscillatorFmDepth(mod, car));
        host.SetParameterValue(modeId,  (double)engine.GetOscillatorRouting(mod, car));
    }

    private static void ApplyOscRoute(SynthEngine engine, IPluginHost host, int mod, int car)
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

        var routingMode = (SynthVoice.OscRouting)(int)Math.Round(host.GetParameterValue(modeId));
        double depth = host.GetParameterValue(depthId);
        engine.SetOscillatorRouting(mod, car, routingMode, depth);
    }
}

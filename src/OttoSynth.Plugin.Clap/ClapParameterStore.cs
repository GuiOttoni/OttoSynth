// CLAP-side parameter store. Mirrors the VST3 plugin's Parameters collection:
// keeps every automatable parameter's current value, definition, and metadata
// for the CLAP host to query via `clap_plugin_params`.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using OttoSynth.Core.DSP.Filters;
using OttoSynth.Core.DSP.Modulation;
using OttoSynth.Core.DSP.Oscillators;
using OttoSynth.Core.Plugin;
using OttoSynth.Core.Voice;
using P = OttoSynth.Core.Plugin.ParameterIds;

namespace OttoSynth.Plugin.Clap;

internal sealed unsafe class ClapParameterStore : IPluginHost
{
    private readonly Dictionary<int, ParamDef> _byId = new();
    private readonly List<int> _orderedIds = new();
    private readonly Dictionary<int, double> _values = new();

    public ClapParameterStore()
    {
        RegisterAll();
    }

    public int Count => _orderedIds.Count;

    // ─── IPluginHost ───────────────────────────────────────────────

    public double GetParameterValue(int id)
        => _values.TryGetValue(id, out var v) ? v : 0.0;

    public void SetParameterValue(int id, double value)
        => _values[id] = value;

    public void Set(int id, double value) => _values[id] = value;

    // ─── CLAP params extension dispatch ────────────────────────────

    public bool GetInfo(uint index, ClapParamInfo* info)
    {
        if (index >= _orderedIds.Count) return false;
        int id = _orderedIds[(int)index];
        var def = _byId[id];

        info->Id           = (uint)id;
        info->Flags        = def.Flags;
        info->Cookie       = null;
        info->MinValue     = def.Min;
        info->MaxValue     = def.Max;
        info->DefaultValue = def.Default;
        WriteUtf8(info->Name, 256, def.Name);
        WriteUtf8(info->Module, 1024, def.Module);
        return true;
    }

    private static void WriteUtf8(byte* dst, int max, string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        int n = Math.Min(bytes.Length, max - 1);
        for (int i = 0; i < n; i++) dst[i] = bytes[i];
        dst[n] = 0;
    }

    // ─── Registration (mirror of VST3's ParameterMap.RegisterAll) ─

    private void RegisterAll()
    {
        // Master
        Reg(P.MasterVolume,   "Master Volume",    "Master", 0, 1, 0.8);
        RegSteppedInt(P.PitchBendRange, "Pitch Bend Range", "Master", 0, 24, 2);
        Reg(P.GlideTime,      "Glide Time",       "Master", 0, 2, 0);
        RegStepped(P.GlideMode, "Glide Mode", "Master", Enum.GetValues<SynthVoice.GlideMode>().Length - 1, 0);
        Reg(P.Macro1, "Macro 1", "Master", 0, 1, 0);
        Reg(P.Macro2, "Macro 2", "Master", 0, 1, 0);
        Reg(P.Macro3, "Macro 3", "Master", 0, 1, 0);
        Reg(P.Macro4, "Macro 4", "Master", 0, 1, 0);

        // OSC 1
        RegOsc(1, "Osc 1", 0.8, true);
        RegOsc(2, "Osc 2", 0.6, false);
        RegOsc(3, "Osc 3", 0.5, false);

        // Noise
        Reg(P.NoiseLevel, "Noise Level", "Noise", 0, 1, 0);
        RegToggle(P.NoiseEnabled, "Noise Enabled", "Noise", false);

        // Filters
        RegFilter(1);
        RegFilter(2);
        RegStepped(P.FilterRouting, "Filter Routing", "Filter", Enum.GetValues<SynthVoice.FilterRouting>().Length - 1, 0);
        Reg(P.FilterEnvAmount, "Filter Env Amount", "Filter", -1, 1, 0);

        // Envelopes
        RegEnv("Amp",    P.EnvAmpAttack, P.EnvAmpDecay, P.EnvAmpSustain, P.EnvAmpRelease, 0.01, 0.3, 0.7, 0.5);
        RegEnv("Filter", P.EnvFilterAttack, P.EnvFilterDecay, P.EnvFilterSustain, P.EnvFilterRelease, 0.001, 0.5, 0.3, 0.3);
        RegEnv("Free",   P.EnvFreeAttack, P.EnvFreeDecay, P.EnvFreeSustain, P.EnvFreeRelease, 0.01, 0.3, 0.7, 0.5);

        // LFOs
        RegLfo(1, P.Lfo1Shape, P.Lfo1Rate, P.Lfo1Depth, P.Lfo1Retrigger);
        RegLfo(2, P.Lfo2Shape, P.Lfo2Rate, P.Lfo2Depth, P.Lfo2Retrigger);
        RegLfo(3, P.Lfo3Shape, P.Lfo3Rate, P.Lfo3Depth, P.Lfo3Retrigger);

        // OSC routing
        Reg(P.OscRouteDepth_1_2, "Route 1→2 Depth", "OscRouting", 0, 1, 0.5);
        Reg(P.OscRouteDepth_1_3, "Route 1→3 Depth", "OscRouting", 0, 1, 0.5);
        Reg(P.OscRouteDepth_2_1, "Route 2→1 Depth", "OscRouting", 0, 1, 0.5);
        Reg(P.OscRouteDepth_2_3, "Route 2→3 Depth", "OscRouting", 0, 1, 0.5);
        Reg(P.OscRouteDepth_3_1, "Route 3→1 Depth", "OscRouting", 0, 1, 0.5);
        Reg(P.OscRouteDepth_3_2, "Route 3→2 Depth", "OscRouting", 0, 1, 0.5);
        int oscRoutingCount = Enum.GetValues<SynthVoice.OscRouting>().Length - 1;
        RegStepped(P.OscRouteMode_1_2, "Route 1→2 Mode", "OscRouting", oscRoutingCount, 0);
        RegStepped(P.OscRouteMode_1_3, "Route 1→3 Mode", "OscRouting", oscRoutingCount, 0);
        RegStepped(P.OscRouteMode_2_1, "Route 2→1 Mode", "OscRouting", oscRoutingCount, 0);
        RegStepped(P.OscRouteMode_2_3, "Route 2→3 Mode", "OscRouting", oscRoutingCount, 0);
        RegStepped(P.OscRouteMode_3_1, "Route 3→1 Mode", "OscRouting", oscRoutingCount, 0);
        RegStepped(P.OscRouteMode_3_2, "Route 3→2 Mode", "OscRouting", oscRoutingCount, 0);

        // Mod matrix
        for (int i = 0; i < 32; i++)
            Reg(P.ModRouteAmount(i), $"Mod Route {i + 1}", "ModMatrix", -1, 1, 0);

        // Effects
        for (int s = 0; s < P.EffectsSlotCount; s++)
        {
            RegToggle(P.EffectBypass(s), $"FX{s + 1} Bypass", "Effects", false);
            Reg(P.EffectMix(s),    $"FX{s + 1} Mix",    "Effects", 0, 1, 1);
        }
    }

    private void RegOsc(int idx, string module, double defLevel, bool enabledDefault)
    {
        int baseId = idx switch { 1 => 2000, 2 => 2100, _ => 2200 };
        Reg(baseId + 1, $"OSC{idx} Level", module, 0, 1, defLevel);
        RegToggle(baseId + 2, $"OSC{idx} Enabled", module, enabledDefault);
        Reg(baseId + 3, $"OSC{idx} Position", module, 0, 1, 0);
        Reg(baseId + 4, $"OSC{idx} Warp Amount", module, 0, 1, 0);
        RegStepped(baseId + 5, $"OSC{idx} Warp Type", module, Enum.GetValues<WavetableOscillator.WaveWarp>().Length - 1, 0);
        RegSteppedInt(baseId + 6, $"OSC{idx} Coarse Tune", module, -24, 24, 0);
        Reg(baseId + 7, $"OSC{idx} Fine Tune", module, -100, 100, 0);
        Reg(baseId + 8, $"OSC{idx} Pan", module, -1, 1, 0);
        RegSteppedInt(baseId + 9, $"OSC{idx} Unison Voices", module, 1, 16, 1);
        Reg(baseId + 10, $"OSC{idx} Unison Detune", module, 0, 100, 20);
        Reg(baseId + 11, $"OSC{idx} Unison Spread", module, 0, 1, 0.8);
    }

    private void RegFilter(int idx)
    {
        int baseId = idx == 1 ? 3000 : 3100;
        string module = $"Filter {idx}";
        RegStepped(baseId + 1, $"Filter{idx} Mode", module, Enum.GetValues<StateVariableFilter.FilterMode>().Length - 1, 0);
        Reg(baseId + 2, $"Filter{idx} Cutoff", module, 20, 20000, 20000);
        Reg(baseId + 3, $"Filter{idx} Resonance", module, 0, 1, 0);
        Reg(baseId + 4, $"Filter{idx} Drive", module, 0, 1, 0);
        RegToggle(baseId + 5, $"Filter{idx} 24dB", module, false);
        Reg(baseId + 6, $"Filter{idx} Key Track", module, 0, 1, 0);
        Reg(baseId + 7, $"Filter{idx} Formant Vowel", module, 0, 1, 0);
        Reg(baseId + 8, $"Filter{idx} Formant Shift", module, 0.5, 2.0, 1.0);
    }

    private void RegEnv(string label, int attackId, int decayId, int sustainId, int releaseId,
        double defA, double defD, double defS, double defR)
    {
        string m = $"{label} Env";
        Reg(attackId,  $"{label} Env Attack",  m, 0.001, 10, defA);
        Reg(decayId,   $"{label} Env Decay",   m, 0.001, 10, defD);
        Reg(sustainId, $"{label} Env Sustain", m, 0, 1, defS);
        Reg(releaseId, $"{label} Env Release", m, 0.001, 10, defR);
    }

    private void RegLfo(int idx, int shapeId, int rateId, int depthId, int retrigId)
    {
        string m = $"LFO {idx}";
        RegStepped(shapeId, $"LFO{idx} Shape", m, Enum.GetValues<LfoGenerator.LfoShape>().Length - 1, 0);
        Reg(rateId,  $"LFO{idx} Rate",  m, 0.01, 30, 1);
        Reg(depthId, $"LFO{idx} Depth", m, 0, 1, 1);
        RegToggle(retrigId, $"LFO{idx} Retrig", m, true);
    }

    // ─── Param def storage ──────────────────────────────────────────

    private void Reg(int id, string name, string module, double min, double max, double def, uint flags = ClapParamFlag.IsAutomatable)
    {
        _byId[id] = new ParamDef(name, module, min, max, def, flags);
        _orderedIds.Add(id);
        _values[id] = def;
    }

    private void RegStepped(int id, string name, string module, int maxStep, int def)
        => Reg(id, name, module, 0, maxStep, def, ClapParamFlag.IsAutomatable | ClapParamFlag.IsStepped);

    private void RegSteppedInt(int id, string name, string module, int min, int max, int def)
        => Reg(id, name, module, min, max, def, ClapParamFlag.IsAutomatable | ClapParamFlag.IsStepped);

    private void RegToggle(int id, string name, string module, bool def)
        => Reg(id, name, module, 0, 1, def ? 1.0 : 0.0, ClapParamFlag.IsAutomatable | ClapParamFlag.IsStepped);

    private readonly record struct ParamDef(string Name, string Module, double Min, double Max, double Default, uint Flags);
}

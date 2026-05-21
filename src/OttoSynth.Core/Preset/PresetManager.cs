using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OttoSynth.Core.Diagnostics;
using OttoSynth.Core.DSP.Effects;
using OttoSynth.Core.DSP.Filters;
using OttoSynth.Core.DSP.Modulation;
using OttoSynth.Core.Voice;

namespace OttoSynth.Core.Preset;

/// <summary>
/// Save/load presets, scan a directory, and apply a preset to the SynthEngine.
/// File format: JSON with .ottopreset extension.
/// </summary>
public sealed class PresetManager
{
    /// <summary>Primary file extension for preset files.</summary>
    public const string FileExtension = ".otto";

    /// <summary>Legacy extension accepted when loading (read-only compat).</summary>
    public const string LegacyExtension = ".ottopreset";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Default user preset directory. Created lazily.</summary>
    public string DefaultDirectory { get; }

    public PresetManager(string? defaultDir = null)
    {
        DefaultDirectory = defaultDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OttoSynth", "Presets");
    }

    /// <summary>Ensures the preset directory exists.</summary>
    public void EnsureDirectory(string? path = null)
    {
        var dir = path ?? DefaultDirectory;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    /// <summary>Saves a preset to a file.</summary>
    public void Save(PresetData preset, string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string json = JsonSerializer.Serialize(preset, JsonOpts);
        File.WriteAllText(filePath, json);
    }

    /// <summary>Saves a preset to the default directory using its Name.</summary>
    public string SaveToDefault(PresetData preset)
    {
        EnsureDirectory();
        var safeName = MakeSafeFileName(preset.Name);
        var path = Path.Combine(DefaultDirectory, preset.Category ?? "Init",
            safeName + FileExtension);
        Save(preset, path);
        return path;
    }

    /// <summary>Loads a preset from a file.</summary>
    public PresetData Load(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var preset = JsonSerializer.Deserialize<PresetData>(json, JsonOpts);
        if (preset == null)
            throw new InvalidDataException($"Failed to parse preset: {filePath}");
        return preset;
    }

    /// <summary>Loads a preset from a JSON string.</summary>
    public PresetData LoadFromJson(string json)
    {
        var preset = JsonSerializer.Deserialize<PresetData>(json, JsonOpts);
        if (preset == null)
            throw new InvalidDataException("Failed to parse preset JSON.");
        return preset;
    }

    /// <summary>Serializes a preset to a JSON string.</summary>
    public string ToJson(PresetData preset) => JsonSerializer.Serialize(preset, JsonOpts);

    /// <summary>Scans a directory for preset files (.otto and legacy .ottopreset). Returns full paths.</summary>
    public List<string> Scan(string? dir = null)
    {
        var d = dir ?? DefaultDirectory;
        var results = new List<string>();
        if (!Directory.Exists(d)) return results;

        foreach (var file in Directory.EnumerateFiles(d, "*.otto", SearchOption.AllDirectories))
            results.Add(file);
        foreach (var file in Directory.EnumerateFiles(d, "*.ottopreset", SearchOption.AllDirectories))
            results.Add(file);

        return results;
    }

    /// <summary>Scans the default directory and returns loaded PresetData objects with their file paths.</summary>
    public List<(string Path, PresetData Preset)> ScanUserPresets()
    {
        var result = new List<(string, PresetData)>();
        foreach (var path in Scan())
        {
            try { result.Add((path, Load(path))); }
            catch { /* skip corrupt files */ }
        }
        return result;
    }

    /// <summary>Exports the current engine state to a specific file path with .otto extension.</summary>
    public string ExportToFile(SynthEngine engine, string filePath, string name)
    {
        if (!filePath.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase))
            filePath = Path.ChangeExtension(filePath, FileExtension);
        var preset = Capture(engine, name);
        Save(preset, filePath);
        return filePath;
    }

    /// <summary>Imports a preset from a .otto or .ottopreset file and applies it to the engine.</summary>
    public PresetData ImportFromFile(string filePath, SynthEngine engine)
    {
        var preset = Load(filePath);
        Apply(preset, engine);
        return preset;
    }

    /// <summary>
    /// Captures the current state of a SynthEngine into a PresetData.
    /// </summary>
    public PresetData Capture(SynthEngine engine, string name = "Untitled")
    {
        var preset = new PresetData
        {
            Name = name,
            MasterVolume = engine.MasterVolume,
            PitchBendRange = engine.PitchBendRange,
        };

        // Take a "template" voice for current parameter state (voice 0).
        // All voices share the same parameter set, only state differs.
        var voice = engine.VoiceManager.Voices[0];

        preset.GlideTime = engine.GlideTime;
        preset.GlideMode = engine.GlideMode.ToString();

        preset.Osc1 = CaptureOsc(voice, 1, engine.CurrentWavetableName);
        preset.Osc2 = CaptureOsc(voice, 2, "Sine");
        preset.Osc3 = CaptureOsc(voice, 3, "Sine");

        preset.Noise.Level = voice.NoiseLevel;
        preset.Noise.Enabled = voice.NoiseEnabled;

        preset.Filter1 = CaptureFilter(voice.Filter1);
        preset.Filter2 = CaptureFilter(voice.Filter2);
        preset.FilterRouting = voice.Routing.ToString();
        preset.FilterEnvAmount = voice.FilterEnvAmount;

        preset.EnvAmp = CaptureEnv(voice.EnvAmp);
        preset.EnvFilter = CaptureEnv(voice.EnvFilter);
        preset.EnvFree = CaptureEnv(voice.EnvFree);

        preset.Lfo1 = CaptureLfo(voice.Lfo1);
        preset.Lfo2 = CaptureLfo(voice.Lfo2);
        preset.Lfo3 = CaptureLfo(voice.Lfo3);

        for (int i = 0; i < 4; i++) preset.Macros[i] = engine.Macros[i];

        // Mod routes (from voice 0's matrix — all voices share the same)
        foreach (var route in voice.ModMatrix.Routes)
        {
            preset.ModRoutes.Add(new ModRouteData
            {
                Source = route.Source.ToString(),
                Destination = route.Destination.ToString(),
                Amount = route.Amount,
                Active = route.Active
            });
        }

        // Effects
        foreach (var fx in engine.Effects.Effects)
        {
            preset.Effects.Add(CaptureEffect(fx));
        }

        return preset;
    }

    /// <summary>
    /// Applies a preset to a SynthEngine, replacing all current state.
    /// </summary>
    public void Apply(PresetData preset, SynthEngine engine)
    {
        try
        {
            Logger.Info("PresetManager", $"Applying preset '{preset.Name}' [{preset.Category}]");
            ApplyInternal(preset, engine);
        }
        catch (Exception ex)
        {
            Logger.Error("PresetManager.Apply", ex, $"preset='{preset.Name}'");
            throw;
        }
    }

    private void ApplyInternal(PresetData preset, SynthEngine engine)
    {
        engine.MasterVolume = preset.MasterVolume;
        engine.PitchBendRange = preset.PitchBendRange;

        if (Enum.TryParse<SynthVoice.GlideMode>(preset.GlideMode, out var glideMode))
            engine.SetPortamento(preset.GlideTime, glideMode);
        else
            engine.SetPortamento(0.0, SynthVoice.GlideMode.Off);

        // Oscillators (apply to all voices)
        ApplyOscToVoices(engine, 1, preset.Osc1);
        ApplyOscToVoices(engine, 2, preset.Osc2);
        ApplyOscToVoices(engine, 3, preset.Osc3);

        // Noise
        foreach (var voice in engine.VoiceManager.Voices)
        {
            voice.NoiseLevel = preset.Noise.Level;
            voice.NoiseEnabled = preset.Noise.Enabled;
        }

        // Filters
        engine.SetFilter(1,
            Enum.TryParse<StateVariableFilter.FilterMode>(preset.Filter1.Mode, out var f1m) ? f1m : StateVariableFilter.FilterMode.LowPass,
            preset.Filter1.Cutoff, preset.Filter1.Resonance, preset.Filter1.Drive, preset.Filter1.Is24dB);
        engine.SetFilter(2,
            Enum.TryParse<StateVariableFilter.FilterMode>(preset.Filter2.Mode, out var f2m) ? f2m : StateVariableFilter.FilterMode.LowPass,
            preset.Filter2.Cutoff, preset.Filter2.Resonance, preset.Filter2.Drive, preset.Filter2.Is24dB);
        if (Enum.TryParse<SynthVoice.FilterRouting>(preset.FilterRouting, out var routing))
            engine.SetFilterRouting(routing);
        engine.SetFilterEnvAmount(preset.FilterEnvAmount);

        // Envelopes
        engine.SetEnvelope(preset.EnvAmp.Attack, preset.EnvAmp.Decay, preset.EnvAmp.Sustain, preset.EnvAmp.Release);
        engine.SetFilterEnvelope(preset.EnvFilter.Attack, preset.EnvFilter.Decay, preset.EnvFilter.Sustain, preset.EnvFilter.Release);
        foreach (var voice in engine.VoiceManager.Voices)
        {
            ApplyEnvCurves(voice.EnvAmp, preset.EnvAmp);
            ApplyEnvCurves(voice.EnvFilter, preset.EnvFilter);
            ApplyEnvCurves(voice.EnvFree, preset.EnvFree);
            voice.EnvFree.AttackTime = preset.EnvFree.Attack;
            voice.EnvFree.DecayTime = preset.EnvFree.Decay;
            voice.EnvFree.SustainLevel = preset.EnvFree.Sustain;
            voice.EnvFree.ReleaseTime = preset.EnvFree.Release;
        }

        // LFOs
        ApplyLfo(engine, 1, preset.Lfo1);
        ApplyLfo(engine, 2, preset.Lfo2);
        ApplyLfo(engine, 3, preset.Lfo3);

        // Macros
        for (int i = 0; i < 4; i++) engine.SetMacro(i, preset.Macros[i]);

        // Mod routes
        engine.ClearModRoutes();
        foreach (var r in preset.ModRoutes)
        {
            if (Enum.TryParse<ModSource>(r.Source, out var src) &&
                Enum.TryParse<ModDestination>(r.Destination, out var dst))
            {
                int idx = engine.AddModRoute(src, dst, r.Amount);
                if (!r.Active && idx >= 0)
                {
                    foreach (var v in engine.VoiceManager.Voices)
                        v.ModMatrix.SetRouteActive(idx, false);
                }
            }
        }

        // Effects: clear and rebuild
        engine.Effects.Clear();
        foreach (var fxData in preset.Effects)
        {
            var fx = CreateEffect(fxData);
            if (fx != null) engine.Effects.Add(fx);
        }
    }

    // ─── Capture helpers ────────────────────────────────────────

    private static OscillatorData CaptureOsc(SynthVoice voice, int idx, string wavetable)
    {
        var osc = idx == 1 ? voice.Osc1 : idx == 2 ? voice.Osc2 : voice.Osc3;
        return new OscillatorData
        {
            Wavetable = wavetable,
            Level = idx == 1 ? voice.Osc1Level : idx == 2 ? voice.Osc2Level : voice.Osc3Level,
            Enabled = idx == 1 ? voice.Osc1Enabled : idx == 2 ? voice.Osc2Enabled : voice.Osc3Enabled,
            Pan = osc.Pan,
            CoarseTune = osc.CoarseTune,
            FineTune = osc.FineTune,
            WavetablePosition = osc.WavetablePosition,
            UnisonVoices = 1,
            UnisonDetune = 0.3,
            UnisonSpread = 0.5
        };
    }

    private static FilterData CaptureFilter(StateVariableFilter f) => new()
    {
        Mode = f.Mode.ToString(),
        Cutoff = f.Cutoff,
        Resonance = f.Resonance,
        Drive = f.Drive,
        Is24dB = f.Is24dB,
        KeyTracking = f.KeyTracking
    };

    private static EnvelopeData CaptureEnv(DSP.Envelopes.AdsrEnvelope env) => new()
    {
        Attack = env.AttackTime,
        Decay = env.DecayTime,
        Sustain = env.SustainLevel,
        Release = env.ReleaseTime
    };

    private static LfoData CaptureLfo(LfoGenerator lfo) => new()
    {
        Shape = lfo.Shape.ToString(),
        Rate = lfo.Rate,
        Depth = lfo.Depth,
        Retrigger = lfo.Retrigger
    };

    private static EffectData CaptureEffect(IEffect fx)
    {
        var data = new EffectData
        {
            Type = fx.GetType().Name,
            Bypass = fx.Bypass,
            Mix = fx.Mix
        };
        switch (fx)
        {
            case Distortion d:
                data.Parameters["Drive"] = d.Drive;
                data.Parameters["OutputGain"] = d.OutputGain;
                data.Parameters["BitDepth"] = d.BitDepth;
                data.StringParameters["Type"] = d.Type.ToString();
                break;
            case Delay dl:
                data.Parameters["TimeLeft"] = dl.TimeLeft;
                data.Parameters["TimeRight"] = dl.TimeRight;
                data.Parameters["Feedback"] = dl.Feedback;
                data.Parameters["PingPong"] = dl.PingPong ? 1 : 0;
                data.Parameters["Damping"] = dl.Damping;
                break;
            case Reverb rv:
                data.Parameters["Size"] = rv.Size;
                data.Parameters["Decay"] = rv.Decay;
                data.Parameters["Damping"] = rv.Damping;
                data.Parameters["PreDelay"] = rv.PreDelay;
                data.Parameters["Width"] = rv.Width;
                break;
            case Chorus c:
                data.Parameters["Rate"] = c.Rate;
                data.Parameters["Depth"] = c.Depth;
                data.Parameters["Feedback"] = c.Feedback;
                break;
            case Phaser p:
                data.Parameters["Rate"] = p.Rate;
                data.Parameters["Depth"] = p.Depth;
                data.Parameters["Feedback"] = p.Feedback;
                data.Parameters["Stages"] = p.Stages;
                break;
            case Flanger fl:
                data.Parameters["Rate"] = fl.Rate;
                data.Parameters["Depth"] = fl.Depth;
                data.Parameters["Feedback"] = fl.Feedback;
                break;
            case Eq3Band eq:
                data.Parameters["LowFreq"] = eq.LowFreq;
                data.Parameters["LowGainDb"] = eq.LowGainDb;
                data.Parameters["MidFreq"] = eq.MidFreq;
                data.Parameters["MidGainDb"] = eq.MidGainDb;
                data.Parameters["MidQ"] = eq.MidQ;
                data.Parameters["HighFreq"] = eq.HighFreq;
                data.Parameters["HighGainDb"] = eq.HighGainDb;
                break;
            case Compressor comp:
                data.Parameters["ThresholdDb"] = comp.ThresholdDb;
                data.Parameters["Ratio"] = comp.Ratio;
                data.Parameters["Attack"] = comp.Attack;
                data.Parameters["Release"] = comp.Release;
                data.Parameters["KneeDb"] = comp.KneeDb;
                data.Parameters["MakeupGainDb"] = comp.MakeupGainDb;
                break;
        }
        return data;
    }

    private static void ApplyOscToVoices(SynthEngine engine, int oscIdx, OscillatorData data)
    {
        try
        {
            engine.SelectWavetable(oscIdx, data.Wavetable);
        }
        catch { /* unknown wavetable name — ignore */ }

        foreach (var voice in engine.VoiceManager.Voices)
        {
            var osc = oscIdx == 1 ? voice.Osc1 : oscIdx == 2 ? voice.Osc2 : voice.Osc3;
            switch (oscIdx)
            {
                case 1: voice.Osc1Level = data.Level; voice.Osc1Enabled = data.Enabled; break;
                case 2: voice.Osc2Level = data.Level; voice.Osc2Enabled = data.Enabled; break;
                case 3: voice.Osc3Level = data.Level; voice.Osc3Enabled = data.Enabled; break;
            }
            osc.Pan = data.Pan;
            osc.CoarseTune = data.CoarseTune;
            osc.FineTune = data.FineTune;
            osc.WavetablePosition = data.WavetablePosition;
        }
    }

    private static void ApplyEnvCurves(DSP.Envelopes.AdsrEnvelope env, EnvelopeData data)
    {
        env.AttackTime = data.Attack;
        env.DecayTime = data.Decay;
        env.SustainLevel = data.Sustain;
        env.ReleaseTime = data.Release;
    }

    private static void ApplyLfo(SynthEngine engine, int idx, LfoData data)
    {
        if (Enum.TryParse<LfoGenerator.LfoShape>(data.Shape, out var shape))
        {
            engine.SetLfo(idx, shape, data.Rate, data.Depth, data.Retrigger);
        }
    }

    private static IEffect? CreateEffect(EffectData data)
    {
        IEffect? fx = data.Type switch
        {
            "Distortion" => CreateDistortion(data),
            "Delay" => CreateDelay(data),
            "Reverb" => CreateReverb(data),
            "Chorus" => CreateChorus(data),
            "Phaser" => CreatePhaser(data),
            "Flanger" => CreateFlanger(data),
            "Eq3Band" => CreateEq(data),
            "Compressor" => CreateCompressor(data),
            _ => null
        };
        if (fx != null)
        {
            fx.Bypass = data.Bypass;
            fx.Mix = data.Mix;
        }
        return fx;
    }

    private static IEffect CreateDistortion(EffectData d)
    {
        var fx = new Distortion();
        if (d.Parameters.TryGetValue("Drive", out var v)) fx.Drive = v;
        if (d.Parameters.TryGetValue("OutputGain", out v)) fx.OutputGain = v;
        if (d.Parameters.TryGetValue("BitDepth", out v)) fx.BitDepth = v;
        if (d.StringParameters.TryGetValue("Type", out var t) &&
            Enum.TryParse<Distortion.DistortionType>(t, out var dt)) fx.Type = dt;
        return fx;
    }

    private static IEffect CreateDelay(EffectData d)
    {
        var fx = new Delay();
        if (d.Parameters.TryGetValue("TimeLeft", out var v)) fx.TimeLeft = v;
        if (d.Parameters.TryGetValue("TimeRight", out v)) fx.TimeRight = v;
        if (d.Parameters.TryGetValue("Feedback", out v)) fx.Feedback = v;
        if (d.Parameters.TryGetValue("PingPong", out v)) fx.PingPong = v > 0.5;
        if (d.Parameters.TryGetValue("Damping", out v)) fx.Damping = v;
        return fx;
    }

    private static IEffect CreateReverb(EffectData d)
    {
        var fx = new Reverb();
        if (d.Parameters.TryGetValue("Size", out var v)) fx.Size = v;
        if (d.Parameters.TryGetValue("Decay", out v)) fx.Decay = v;
        if (d.Parameters.TryGetValue("Damping", out v)) fx.Damping = v;
        if (d.Parameters.TryGetValue("PreDelay", out v)) fx.PreDelay = v;
        if (d.Parameters.TryGetValue("Width", out v)) fx.Width = v;
        return fx;
    }

    private static IEffect CreateChorus(EffectData d)
    {
        var fx = new Chorus();
        if (d.Parameters.TryGetValue("Rate", out var v)) fx.Rate = v;
        if (d.Parameters.TryGetValue("Depth", out v)) fx.Depth = v;
        if (d.Parameters.TryGetValue("Feedback", out v)) fx.Feedback = v;
        return fx;
    }

    private static IEffect CreatePhaser(EffectData d)
    {
        var fx = new Phaser();
        if (d.Parameters.TryGetValue("Rate", out var v)) fx.Rate = v;
        if (d.Parameters.TryGetValue("Depth", out v)) fx.Depth = v;
        if (d.Parameters.TryGetValue("Feedback", out v)) fx.Feedback = v;
        if (d.Parameters.TryGetValue("Stages", out v)) fx.Stages = (int)v;
        return fx;
    }

    private static IEffect CreateFlanger(EffectData d)
    {
        var fx = new Flanger();
        if (d.Parameters.TryGetValue("Rate", out var v)) fx.Rate = v;
        if (d.Parameters.TryGetValue("Depth", out v)) fx.Depth = v;
        if (d.Parameters.TryGetValue("Feedback", out v)) fx.Feedback = v;
        return fx;
    }

    private static IEffect CreateEq(EffectData d)
    {
        var fx = new Eq3Band();
        if (d.Parameters.TryGetValue("LowFreq", out var v)) fx.LowFreq = v;
        if (d.Parameters.TryGetValue("LowGainDb", out v)) fx.LowGainDb = v;
        if (d.Parameters.TryGetValue("MidFreq", out v)) fx.MidFreq = v;
        if (d.Parameters.TryGetValue("MidGainDb", out v)) fx.MidGainDb = v;
        if (d.Parameters.TryGetValue("MidQ", out v)) fx.MidQ = v;
        if (d.Parameters.TryGetValue("HighFreq", out v)) fx.HighFreq = v;
        if (d.Parameters.TryGetValue("HighGainDb", out v)) fx.HighGainDb = v;
        return fx;
    }

    private static IEffect CreateCompressor(EffectData d)
    {
        var fx = new Compressor();
        if (d.Parameters.TryGetValue("ThresholdDb", out var v)) fx.ThresholdDb = v;
        if (d.Parameters.TryGetValue("Ratio", out v)) fx.Ratio = v;
        if (d.Parameters.TryGetValue("Attack", out v)) fx.Attack = v;
        if (d.Parameters.TryGetValue("Release", out v)) fx.Release = v;
        if (d.Parameters.TryGetValue("KneeDb", out v)) fx.KneeDb = v;
        if (d.Parameters.TryGetValue("MakeupGainDb", out v)) fx.MakeupGainDb = v;
        return fx;
    }

    private static string MakeSafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}

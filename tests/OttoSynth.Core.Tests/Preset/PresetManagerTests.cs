using System.IO;
using OttoSynth.Core;
using OttoSynth.Core.DSP.Effects;
using OttoSynth.Core.DSP.Modulation;
using OttoSynth.Core.Preset;

namespace OttoSynth.Core.Tests.Preset;

public class PresetManagerTests
{
    [Fact]
    public void RoundTrip_JsonString_PreservesAllFields()
    {
        var mgr = new PresetManager();
        var preset = FactoryPresets.Init();
        preset.Name = "MyPreset";
        preset.Osc1.Level = 0.42;
        preset.Filter1.Cutoff = 1234.5;

        string json = mgr.ToJson(preset);
        var roundTrip = mgr.LoadFromJson(json);

        Assert.Equal("MyPreset", roundTrip.Name);
        Assert.Equal(0.42, roundTrip.Osc1.Level);
        Assert.Equal(1234.5, roundTrip.Filter1.Cutoff);
    }

    [Fact]
    public void CaptureAndApply_EngineState_RoundTrips()
    {
        var engine = new SynthEngine(8, 512);
        engine.Initialize(44100, 256);
        engine.MasterVolume = 0.55;
        engine.SetFilter(1, OttoSynth.Core.DSP.Filters.StateVariableFilter.FilterMode.HighPass, 2500.0, 0.3, 0.1);
        engine.AddModRoute(ModSource.Lfo1, ModDestination.Filter1Cutoff, 0.7);
        engine.SetMacro(0, 0.7);

        var mgr = new PresetManager();
        var captured = mgr.Capture(engine, "Test");

        // Apply to a fresh engine
        var engine2 = new SynthEngine(8, 512);
        engine2.Initialize(44100, 256);
        mgr.Apply(captured, engine2);

        Assert.Equal(0.55, engine2.MasterVolume, precision: 4);
        Assert.Equal(0.7, engine2.Macros[0], precision: 4);
        // Mod route should be transferred
        Assert.Equal(1, engine2.VoiceManager.Voices[0].ModMatrix.RouteCount);
    }

    [Fact]
    public void Apply_PresetWithEffects_RebuildsChain()
    {
        var engine = new SynthEngine(4, 256);
        engine.Initialize(44100, 256);

        var preset = FactoryPresets.Init();
        preset.Effects.Add(new EffectData
        {
            Type = "Reverb",
            Mix = 0.4,
            Parameters = { ["Size"] = 0.5, ["Decay"] = 0.6 }
        });
        preset.Effects.Add(new EffectData
        {
            Type = "Delay",
            Mix = 0.3,
            Parameters = { ["TimeLeft"] = 0.25, ["TimeRight"] = 0.25, ["Feedback"] = 0.5 }
        });

        new PresetManager().Apply(preset, engine);

        Assert.Equal(2, engine.Effects.Count);
        Assert.Equal("Reverb", engine.Effects[0].Name);
        Assert.Equal("Delay", engine.Effects[1].Name);
        Assert.Equal(0.4, engine.Effects[0].Mix, precision: 4);
    }

    [Fact]
    public void FactoryPresets_HasAtLeast50()
    {
        var presets = FactoryPresets.All();
        Assert.True(presets.Count >= 50, $"Expected ≥50 factory presets, got {presets.Count}");
    }

    [Fact]
    public void SaveAndLoad_File_RoundTrips()
    {
        var mgr = new PresetManager();
        var preset = FactoryPresets.Init();
        preset.Name = "FileTest";

        var tmp = Path.Combine(Path.GetTempPath(), "ottosynth_test.ottopreset");
        try
        {
            mgr.Save(preset, tmp);
            Assert.True(File.Exists(tmp));
            var loaded = mgr.Load(tmp);
            Assert.Equal("FileTest", loaded.Name);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}

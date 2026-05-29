using System.Collections.ObjectModel;
using OttoSynth.Core;
using OttoSynth.Core.DSP.Oscillators;
using OttoSynth.Core.Preset;
using OttoSynth.UI.Commands;
using ReactiveUI;

namespace OttoSynth.UI.ViewModels;

public class OscillatorsViewModel : ReactiveObject
{
    private readonly SynthEngine _engine;

    public OscillatorViewModel Osc1 { get; }
    public OscillatorViewModel Osc2 { get; }
    public OscillatorViewModel Osc3 { get; }

    public OscRoutingMatrixViewModel RoutingMatrix { get; }

    public ObservableCollection<string> WavetableNames { get; }
    public IReadOnlyList<string> WarpTypes { get; } =
        Enum.GetNames<WavetableOscillator.WaveWarp>().ToList();

    public OscillatorsViewModel(SynthEngine engine, CommandHistory? history = null)
    {
        _engine        = engine;
        WavetableNames = new ObservableCollection<string>(engine.WavetableNames);
        Osc1           = new OscillatorViewModel(engine, 1, history) { SelectedWavetable = "Saw" };
        Osc2           = new OscillatorViewModel(engine, 2, history) { SelectedWavetable = "Sine" };
        Osc3           = new OscillatorViewModel(engine, 3, history) { SelectedWavetable = "Triangle" };
        RoutingMatrix  = new OscRoutingMatrixViewModel(engine);
    }

    public string LoadWavetableFromFile(int oscIndex, string path)
    {
        string name = _engine.LoadWavetableFromFile(path, oscIndex);
        if (!WavetableNames.Contains(name))
            WavetableNames.Add(name);
        var osc = oscIndex switch { 1 => Osc1, 2 => Osc2, _ => Osc3 };
        osc.SelectedWavetable = name;
        return name;
    }

    public void ApplyPreset(PresetData p)
    {
        Osc1.ApplyPreset(p.Osc1);
        Osc2.ApplyPreset(p.Osc2);
        Osc3.ApplyPreset(p.Osc3);
        RoutingMatrix.ApplyPreset(p);
    }
}

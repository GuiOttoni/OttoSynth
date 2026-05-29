using OttoSynth.Core;
using OttoSynth.Core.Preset;
using OttoSynth.UI.Commands;
using ReactiveUI;

namespace OttoSynth.UI.ViewModels;

public class LfosViewModel : ReactiveObject
{
    public LfoViewModel Lfo1 { get; }
    public LfoViewModel Lfo2 { get; }
    public LfoViewModel Lfo3 { get; }

    public LfosViewModel(SynthEngine engine, CommandHistory? history = null)
    {
        Lfo1 = new LfoViewModel(engine, 1, history);
        Lfo2 = new LfoViewModel(engine, 2, history);
        Lfo3 = new LfoViewModel(engine, 3, history);
    }

    public void ApplyPreset(PresetData p)
    {
        Lfo1.ApplyPreset(p.Lfo1);
        Lfo2.ApplyPreset(p.Lfo2);
        Lfo3.ApplyPreset(p.Lfo3);
    }
}

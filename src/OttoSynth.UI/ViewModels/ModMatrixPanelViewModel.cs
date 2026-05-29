using OttoSynth.Core;
using OttoSynth.Core.Preset;

namespace OttoSynth.UI.ViewModels;

public sealed class ModMatrixPanelViewModel
{
    public ModMatrixViewModel Matrix { get; }
    public MasterViewModel Master { get; }

    public ModMatrixPanelViewModel(SynthEngine engine)
    {
        Matrix = new ModMatrixViewModel(engine);
        Master = new MasterViewModel(engine);
    }

    public void ApplyPreset(PresetData p)
    {
        Matrix.Refresh();
        Master.ApplyPreset(p);
    }
}

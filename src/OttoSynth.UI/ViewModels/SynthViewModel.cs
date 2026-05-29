using OttoSynth.Core;
using OttoSynth.Core.Preset;
using OttoSynth.UI.Commands;

namespace OttoSynth.UI.ViewModels;

public sealed class SynthViewModel
{
    public CommandHistory History { get; } = new();

    public OscillatorsViewModel Oscillators { get; }
    public FilterViewModel Filter { get; }
    public EnvelopeViewModel Envelopes { get; }
    public LfosViewModel Lfos { get; }
    public ModMatrixPanelViewModel ModMatrix { get; }
    public MasterViewModel Master { get; }

    public SynthViewModel(SynthEngine engine)
    {
        Oscillators = new OscillatorsViewModel(engine, History);
        Filter      = new FilterViewModel(engine, History);
        Envelopes   = new EnvelopeViewModel(engine, History);
        Lfos        = new LfosViewModel(engine, History);
        ModMatrix   = new ModMatrixPanelViewModel(engine);
        Master      = new MasterViewModel(engine);
    }

    public void ApplyPreset(PresetData preset, SynthEngine engine, PresetManager manager)
    {
        manager.Apply(preset, engine);
        Oscillators.ApplyPreset(preset);
        Filter.ApplyPreset(preset);
        Envelopes.ApplyPreset(preset);
        Lfos.ApplyPreset(preset);
        ModMatrix.ApplyPreset(preset);
    }
}

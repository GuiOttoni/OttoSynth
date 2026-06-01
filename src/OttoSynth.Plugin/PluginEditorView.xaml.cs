using System.Windows.Controls;
using OttoSynth.Core;
using OttoSynth.Core.Preset;
using OttoSynth.UI.ViewModels;

namespace OttoSynth.Plugin;

public partial class PluginEditorView : UserControl
{
    private readonly SynthEngine _engine;
    private readonly PresetManager _presetManager;
    private SynthViewModel _vm = null!;

    public PluginEditorView(SynthEngine engine)
    {
        _engine = engine;
        _presetManager = new PresetManager();
        InitializeComponent();
        Loaded += (_, _) => Initialize();
    }

    private void Initialize()
    {
        _vm = new SynthViewModel(_engine);

        OscillatorsPanelView.DataContext = _vm.Oscillators;
        FilterPanelView.DataContext      = _vm.Filter;
        EnvelopePanelView.DataContext    = _vm.Envelopes;
        LfosPanelView.DataContext        = _vm.Lfos;
        ModMatrixPanelView.DataContext   = _vm.ModMatrix;
        MasterPanelView.DataContext      = _vm.Master;

        PopulatePresets();
        StatusText.Text = "> PLUGIN EDITOR ONLINE";
    }

    private void PopulatePresets()
    {
        PresetSelector.Items.Clear();
        foreach (var p in FactoryPresets.All())
            PresetSelector.Items.Add(p.Name);
        if (PresetSelector.Items.Count > 0)
            PresetSelector.SelectedIndex = 0;
    }

    private void LoadPresetBtn_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (PresetSelector.SelectedItem is not string name) return;
        foreach (var p in FactoryPresets.All())
        {
            if (p.Name != name) continue;
            _vm.ApplyPreset(p, _engine, _presetManager);
            StatusText.Text = $"> LOADED: {p.Name}";
            break;
        }
    }
}

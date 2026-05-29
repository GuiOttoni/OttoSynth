using System.Windows.Controls;
using OttoSynth.UI.Controls;
using OttoSynth.UI.ViewModels;

namespace OttoSynth.UI.Views;

public partial class LfosPanel : UserControl
{
    public LfosPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => WireShapeDisplays();
    }

    private void WireShapeDisplays()
    {
        if (DataContext is not LfosViewModel vm) return;

        WireDisplay(vm.Lfo1, Lfo1View);
        WireDisplay(vm.Lfo2, Lfo2View);
        WireDisplay(vm.Lfo3, Lfo3View);
    }

    private static void WireDisplay(LfoViewModel lfo, LfoDisplay display)
    {
        lfo.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LfoViewModel.SelectedShape))
            {
                if (Enum.TryParse<LfoDisplay.LfoShape>(lfo.SelectedShape, out var s))
                    display.Shape = s;
            }
        };
        if (Enum.TryParse<LfoDisplay.LfoShape>(lfo.SelectedShape, out var initial))
            display.Shape = initial;
    }
}

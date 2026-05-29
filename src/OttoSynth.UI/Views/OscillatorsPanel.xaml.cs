using System.Windows;
using System.Windows.Controls;
using OttoSynth.UI.ViewModels;

namespace OttoSynth.UI.Views;

public partial class OscillatorsPanel : UserControl
{
    public OscillatorsPanel() => InitializeComponent();

    private OscillatorsViewModel? VM => DataContext as OscillatorsViewModel;

    private void OnLoadWavetable1Click(object sender, RoutedEventArgs e) => LoadWavetable(1);
    private void OnLoadWavetable2Click(object sender, RoutedEventArgs e) => LoadWavetable(2);
    private void OnLoadWavetable3Click(object sender, RoutedEventArgs e) => LoadWavetable(3);

    private void LoadWavetable(int oscIndex)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "WAV files|*.wav",
            Title  = $"Load Wavetable for OSC {oscIndex}"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            VM?.LoadWavetableFromFile(oscIndex, dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Load Wavetable Failed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

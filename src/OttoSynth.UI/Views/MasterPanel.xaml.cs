using System.Windows;
using System.Windows.Controls;
using OttoSynth.UI.ViewModels;

namespace OttoSynth.UI.Views;

public partial class MasterPanel : UserControl
{
    public MasterPanel() => InitializeComponent();

    private MasterViewModel? VM => DataContext as MasterViewModel;

    private void OnLearn1Click(object sender, RoutedEventArgs e) => VM?.ToggleLearn(0);
    private void OnLearn2Click(object sender, RoutedEventArgs e) => VM?.ToggleLearn(1);
    private void OnLearn3Click(object sender, RoutedEventArgs e) => VM?.ToggleLearn(2);
    private void OnLearn4Click(object sender, RoutedEventArgs e) => VM?.ToggleLearn(3);

    private void OnUnmap1Click(object sender, RoutedEventArgs e) => VM?.UnmapMacroCc(0);
    private void OnUnmap2Click(object sender, RoutedEventArgs e) => VM?.UnmapMacroCc(1);
    private void OnUnmap3Click(object sender, RoutedEventArgs e) => VM?.UnmapMacroCc(2);
    private void OnUnmap4Click(object sender, RoutedEventArgs e) => VM?.UnmapMacroCc(3);
}

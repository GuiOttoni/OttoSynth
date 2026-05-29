using System.Windows;
using OttoSynth.Standalone.Models;
using OttoSynth.Standalone.Services;

namespace OttoSynth.Standalone.Views;

public partial class AudioSettingsDialog : Window
{
    public int SelectedSampleRate { get; private set; }
    public int SelectedBufferSize { get; private set; }

    public AudioSettingsDialog(int currentSampleRate, int currentBufferSize)
    {
        InitializeComponent();

        foreach (int sr in AudioService.SupportedSampleRates)
            SampleRateBox.Items.Add($"{sr / 1000.0:G} kHz");

        foreach (int bs in AudioService.SupportedBufferSizes)
            BufferSizeBox.Items.Add($"{bs} samples");

        SampleRateBox.SelectedIndex = Array.IndexOf(AudioService.SupportedSampleRates, currentSampleRate);
        BufferSizeBox.SelectedIndex = Array.IndexOf(AudioService.SupportedBufferSizes, currentBufferSize);

        if (SampleRateBox.SelectedIndex < 0) SampleRateBox.SelectedIndex = 0;
        if (BufferSizeBox.SelectedIndex  < 0) BufferSizeBox.SelectedIndex  = 1;

        UpdateLatency();
    }

    private void OnSettingChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => UpdateLatency();

    private void UpdateLatency()
    {
        if (SampleRateBox.SelectedIndex < 0 || BufferSizeBox.SelectedIndex < 0) return;

        int sr = AudioService.SupportedSampleRates[SampleRateBox.SelectedIndex];
        int bs = AudioService.SupportedBufferSizes[BufferSizeBox.SelectedIndex];
        double ms = Math.Round((double)bs / sr * 1000.0, 1);
        LatencyText.Text = $"~{ms} ms";
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        SelectedSampleRate = AudioService.SupportedSampleRates[SampleRateBox.SelectedIndex];
        SelectedBufferSize = AudioService.SupportedBufferSizes[BufferSizeBox.SelectedIndex];
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
        => DialogResult = false;
}

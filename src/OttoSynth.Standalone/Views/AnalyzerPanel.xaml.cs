using System.Windows.Controls;

namespace OttoSynth.Standalone.Views;

public partial class AnalyzerPanel : UserControl
{
    public AnalyzerPanel() => InitializeComponent();

    public double SampleRate
    {
        set => SpectrumViewInternal.SampleRate = value;
    }

    public double[] WaveformSamples
    {
        set => WaveformViewInternal.Samples = value;
    }

    public double[] SpectrumSamples
    {
        set => SpectrumViewInternal.Samples = value;
    }
}

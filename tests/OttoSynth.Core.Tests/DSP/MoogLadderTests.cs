using OttoSynth.Core.DSP.Filters;

namespace OttoSynth.Core.Tests.DSP;

public class MoogLadderTests
{
    [Fact]
    public void Process_SilentInput_ProducesSilence()
    {
        var f = new MoogLadderFilter { Cutoff = 1000, Resonance = 0.5 };
        f.SetSampleRate(44100);
        var input = new double[256];
        var output = new double[256];
        f.Process(input, output, 256);
        foreach (var s in output) Assert.Equal(0.0, s);
    }

    [Fact]
    public void Process_LowFrequencyPasses_HighFrequencyAttenuated()
    {
        // Generate 100 Hz and 5000 Hz inputs separately, measure output level after settling
        var f = new MoogLadderFilter { Cutoff = 500, Resonance = 0.0 };
        f.SetSampleRate(44100);

        var lowIn = new double[4096];
        var highIn = new double[4096];
        for (int i = 0; i < 4096; i++)
        {
            lowIn[i] = System.Math.Sin(2 * System.Math.PI * 100.0 * i / 44100.0);
            highIn[i] = System.Math.Sin(2 * System.Math.PI * 5000.0 * i / 44100.0);
        }

        var lowOut = new double[4096];
        var highOut = new double[4096];
        f.Process(lowIn, lowOut, 4096);

        // Reset for second pass
        f.ResetState();
        f.Process(highIn, highOut, 4096);

        double lowEnergy = 0, highEnergy = 0;
        for (int i = 2048; i < 4096; i++)
        {
            lowEnergy += lowOut[i] * lowOut[i];
            highEnergy += highOut[i] * highOut[i];
        }

        Assert.True(lowEnergy > highEnergy * 5.0,
            $"Expected low-passed signal to dominate. Low={lowEnergy}, High={highEnergy}");
    }

    [Fact]
    public void Process_DoesNotProduceNaN()
    {
        var f = new MoogLadderFilter { Cutoff = 1000, Resonance = 0.95, Drive = 1.0 };
        f.SetSampleRate(44100);
        var input = new double[1024];
        var output = new double[1024];
        for (int i = 0; i < 1024; i++)
            input[i] = System.Math.Sin(2 * System.Math.PI * 440.0 * i / 44100.0);
        f.Process(input, output, 1024);
        foreach (var s in output) Assert.False(double.IsNaN(s) || double.IsInfinity(s));
    }
}

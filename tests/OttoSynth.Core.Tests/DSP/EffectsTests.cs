using OttoSynth.Core.DSP.Effects;

namespace OttoSynth.Core.Tests.DSP;

public class EffectsTests
{
    [Fact]
    public void EffectsChain_Empty_PassesThrough()
    {
        var chain = new EffectsChain();
        chain.SetSampleRate(44100);
        var l = new double[] { 0.5, -0.3, 0.7 };
        var r = new double[] { 0.1, 0.2, -0.1 };

        chain.Process(l, r, 3);

        Assert.Equal(0.5, l[0]);
        Assert.Equal(0.1, r[0]);
    }

    [Fact]
    public void EffectsChain_AddRemoveMove_WorksCorrectly()
    {
        var chain = new EffectsChain();
        var d = new Distortion();
        var r = new Reverb();
        var dl = new Delay();
        chain.Add(d);
        chain.Add(r);
        chain.Add(dl);

        Assert.Equal(3, chain.Count);
        Assert.Same(d, chain[0]);
        Assert.Same(r, chain[1]);

        chain.Move(0, 2);
        Assert.Same(d, chain[2]);
        Assert.Same(r, chain[0]);

        chain.RemoveAt(2);
        Assert.Equal(2, chain.Count);
    }

    [Fact]
    public void Distortion_Bypassed_LeavesSignalUnchanged()
    {
        var d = new Distortion { Bypass = true, Drive = 1.0 };
        d.SetSampleRate(44100);
        var l = new double[] { 0.5 };
        var r = new double[] { 0.3 };

        d.Process(l, r, 1);

        Assert.Equal(0.5, l[0]);
        Assert.Equal(0.3, r[0]);
    }

    [Fact]
    public void Distortion_Overdrive_AmplifiesAndClips()
    {
        var d = new Distortion { Drive = 1.0, Type = Distortion.DistortionType.Overdrive };
        d.SetSampleRate(44100);
        var l = new double[] { 0.5 };
        var r = new double[] { 0.5 };

        d.Process(l, r, 1);

        // Should be clipped near 1.0 since drive amplifies by ~40x
        Assert.True(Math.Abs(l[0]) > 0.5);
        Assert.True(Math.Abs(l[0]) <= 1.0);
    }

    [Fact]
    public void Delay_ProducesEchoAfterDelayTime()
    {
        var d = new Delay { TimeLeft = 0.01, TimeRight = 0.01, Feedback = 0.0 };
        d.SetSampleRate(44100);
        // Mix=1 to get pure delayed signal
        d.Mix = 1.0;

        int delaySamples = 441; // 10ms @ 44100
        var l = new double[delaySamples + 100];
        var r = new double[delaySamples + 100];
        l[0] = 1.0;
        r[0] = 1.0;

        d.Process(l, r, l.Length);

        // First sample is the dry that becomes wet=0 (no prior delay)
        Assert.True(Math.Abs(l[delaySamples]) > 0.5, $"Expected echo at {delaySamples}, got {l[delaySamples]}");
    }

    [Fact]
    public void Reverb_SilentInput_RemainsSilent()
    {
        var rv = new Reverb { Decay = 0.5, Size = 0.5 };
        rv.SetSampleRate(44100);
        var l = new double[256];
        var r = new double[256];

        rv.Process(l, r, 256);

        for (int i = 0; i < 256; i++)
        {
            Assert.Equal(0.0, l[i]);
            Assert.Equal(0.0, r[i]);
        }
    }

    [Fact]
    public void Reverb_Impulse_ProducesNonZeroTail()
    {
        var rv = new Reverb { Decay = 0.7, Size = 0.5, PreDelay = 0 };
        rv.SetSampleRate(44100);
        const int n = 2048;
        var l = new double[n];
        var r = new double[n];
        l[0] = 1.0;
        r[0] = 1.0;

        rv.Process(l, r, n);

        // Reverb tail should produce non-zero output well after the impulse
        double tailEnergy = 0;
        for (int i = 500; i < n; i++)
        {
            tailEnergy += Math.Abs(l[i]) + Math.Abs(r[i]);
        }
        Assert.True(tailEnergy > 0.01, $"Expected non-zero reverb tail, got {tailEnergy}");
    }

    [Fact]
    public void Compressor_Reduces_LoudSignal()
    {
        var comp = new Compressor
        {
            ThresholdDb = -20.0,
            Ratio = 10.0,
            Attack = 0.001,
            Release = 0.01,
            KneeDb = 0.0,
            MakeupGainDb = 0.0
        };
        comp.SetSampleRate(44100);

        const int count = 2048;
        var l = new double[count];
        var r = new double[count];
        // Loud sustained signal
        for (int i = 0; i < count; i++)
        {
            l[i] = 0.9;
            r[i] = 0.9;
        }

        comp.Process(l, r, count);

        // After settling (~1ms = 44 samples), gain reduction should bring level well below original
        Assert.True(Math.Abs(l[count - 1]) < 0.5,
            $"Expected l[last] < 0.5 after settling, got {l[count - 1]}; l[100]={l[100]}, l[1000]={l[1000]}");
    }

    [Fact]
    public void Eq_FlatResponse_DoesNotChangeAmplitudeMuch()
    {
        var eq = new Eq3Band();
        eq.SetSampleRate(44100);

        // Generate a 1kHz sine wave
        var l = new double[1024];
        var r = new double[1024];
        for (int i = 0; i < 1024; i++)
        {
            double s = Math.Sin(2 * Math.PI * 1000.0 * i / 44100.0);
            l[i] = s;
            r[i] = s;
        }

        eq.Process(l, r, 1024);

        // With flat 0dB settings, amplitude should remain near 1.0
        double maxL = 0;
        for (int i = 256; i < 1024; i++) maxL = Math.Max(maxL, Math.Abs(l[i]));
        Assert.True(maxL > 0.9 && maxL < 1.1);
    }
}

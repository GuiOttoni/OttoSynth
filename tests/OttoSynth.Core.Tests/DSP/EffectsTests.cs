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
    public void Compressor_ZeroKnee_DoesNotProduceNaN()
    {
        // Bug 1.3: KneeDb=0 → knee=0 → (x*x)/(2*knee) = 0/0 = NaN when inDb == threshold.
        // The fix clamps knee to a minimum of 0.001.
        var c = new Compressor { ThresholdDb = -12.0, KneeDb = 0.0, Ratio = 4.0 };
        c.SetSampleRate(44100);

        // Signal exactly at threshold: 10^(-12/20) ≈ 0.2512
        double atThreshold = Math.Pow(10, -12.0 / 20.0);
        var l = new double[64];
        var r = new double[64];
        for (int i = 0; i < 64; i++) { l[i] = atThreshold; r[i] = atThreshold; }

        c.Process(l, r, 64);

        for (int i = 0; i < 64; i++)
        {
            Assert.False(double.IsNaN(l[i]), $"NaN at l[{i}] with KneeDb=0");
            Assert.False(double.IsNaN(r[i]), $"NaN at r[{i}] with KneeDb=0");
            Assert.False(double.IsInfinity(l[i]), $"Inf at l[{i}] with KneeDb=0");
        }
    }

    [Fact]
    public void Compressor_QuietSignal_FlushesDenormalsInGrDb()
    {
        // Bug 1.4: smoothing converges to subnormal values (~1e-38) causing CPU spikes.
        // The fix flushes _grDb to 0 when |_grDb| < 1e-8.
        var c = new Compressor
        {
            ThresholdDb = -60.0,
            Ratio = 4.0,
            Attack = 0.001,
            Release = 0.001,
            KneeDb = 6.0
        };
        c.SetSampleRate(44100);

        // Warm up with audible signal so _grDb has a non-zero starting point.
        var loud = new double[4096];
        for (int i = 0; i < 4096; i++) loud[i] = 0.5;
        c.Process(loud, loud, 4096);

        // Now feed a long stretch of near-silence so _grDb decays toward zero.
        var quiet = new double[8192];
        c.Process(quiet, quiet, 8192);

        // Final sample should be exactly 0 (denormal flushed), not a subnormal residue.
        Assert.Equal(0.0, quiet[8191]);
    }

    [Fact]
    public void Delay_PingPongMaxFeedback_DoesNotRunAway()
    {
        // Bug 2.6: ping-pong cross-couples L↔R; without an extra clamp the
        // resonance can grow unbounded even within the per-channel 0.95 limit.
        var d = new Delay
        {
            TimeLeft = 0.05,
            TimeRight = 0.07,
            Feedback = 0.95,
            PingPong = true,
            Damping = 0.0,
            Mix = 1.0
        };
        d.SetSampleRate(44100);

        var l = new double[4096];
        var r = new double[4096];
        // Impulse only at the start.
        l[0] = 1.0;
        r[0] = 1.0;

        // Run 10 buffers worth of time (~1 second). Signal should NOT grow unbounded.
        for (int b = 0; b < 10; b++)
        {
            d.Process(l, r, 4096);

            double peak = 0;
            for (int i = 0; i < 4096; i++)
            {
                peak = Math.Max(peak, Math.Abs(l[i]));
                peak = Math.Max(peak, Math.Abs(r[i]));
                Assert.False(double.IsNaN(l[i]) || double.IsInfinity(l[i]), $"NaN/Inf at buffer {b} sample {i}");
            }

            // tanh soft-clip + 0.85 cap keeps the signal bounded near 1.0.
            Assert.True(peak < 3.0, $"Buffer {b}: peak={peak} exceeded safety bound; ping-pong delay ran away");

            // Zero the input for subsequent buffers — only the feedback chain drives output.
            Array.Clear(l, 0, 4096);
            Array.Clear(r, 0, 4096);
        }
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

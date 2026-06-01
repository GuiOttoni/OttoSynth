using System;
using Xunit;
using OttoSynth.Core.DSP.Oscillators;

namespace OttoSynth.Core.Tests.DSP;

public class NoiseOscillatorTests
{
    [Fact]
    public void WhiteNoise_ProducesNonZeroOutput()
    {
        var noise = new NoiseOscillator();
        noise.Type = NoiseOscillator.NoiseType.White;
        noise.Level = 1.0;

        int bufferSize = 1024;
        var left = new double[bufferSize];
        var right = new double[bufferSize];

        noise.Process(left, right, bufferSize);

        double energy = 0;
        for (int i = 0; i < bufferSize; i++)
            energy += left[i] * left[i];

        Assert.True(energy > 0.01, "White noise should produce non-zero output");
    }

    [Fact]
    public void WhiteNoise_OutputWithinRange()
    {
        var noise = new NoiseOscillator();
        noise.Type = NoiseOscillator.NoiseType.White;
        noise.Level = 1.0;

        int bufferSize = 4096;
        var left = new double[bufferSize];
        var right = new double[bufferSize];

        noise.Process(left, right, bufferSize);

        for (int i = 0; i < bufferSize; i++)
        {
            Assert.InRange(left[i], -1.1, 1.1); // Allow slight overshoot due to level
        }
    }

    [Fact]
    public void PinkNoise_ProducesNonZeroOutput()
    {
        var noise = new NoiseOscillator();
        noise.Type = NoiseOscillator.NoiseType.Pink;
        noise.Level = 1.0;

        int bufferSize = 1024;
        var left = new double[bufferSize];
        var right = new double[bufferSize];

        noise.Process(left, right, bufferSize);

        double energy = 0;
        for (int i = 0; i < bufferSize; i++)
            energy += left[i] * left[i];

        Assert.True(energy > 0.001, "Pink noise should produce non-zero output");
    }

    [Fact]
    public void ZeroLevel_ProducesSilence()
    {
        var noise = new NoiseOscillator();
        noise.Level = 0.0;

        int bufferSize = 256;
        var left = new double[bufferSize];
        var right = new double[bufferSize];

        noise.Process(left, right, bufferSize);

        for (int i = 0; i < bufferSize; i++)
        {
            Assert.Equal(0.0, left[i]);
            Assert.Equal(0.0, right[i]);
        }
    }

    [Fact]
    public void DeterministicSeed_ProducesReproducibleOutput()
    {
        var noise1 = new NoiseOscillator();
        noise1.Reseed(12345);
        noise1.Type = NoiseOscillator.NoiseType.White;
        noise1.Level = 1.0;

        var noise2 = new NoiseOscillator();
        noise2.Reseed(12345);
        noise2.Type = NoiseOscillator.NoiseType.White;
        noise2.Level = 1.0;

        int bufferSize = 256;
        var left1 = new double[bufferSize];
        var right1 = new double[bufferSize];
        var left2 = new double[bufferSize];
        var right2 = new double[bufferSize];

        noise1.Process(left1, right1, bufferSize);
        noise2.Process(left2, right2, bufferSize);

        for (int i = 0; i < bufferSize; i++)
        {
            Assert.Equal(left1[i], left2[i], precision: 10);
        }
    }

    [Fact]
    public void LevelScaling_AffectsAmplitude()
    {
        var noiseLow = new NoiseOscillator();
        noiseLow.Reseed(42);
        noiseLow.Level = 0.1;

        var noiseHigh = new NoiseOscillator();
        noiseHigh.Reseed(42);
        noiseHigh.Level = 1.0;

        int bufferSize = 256;
        var lowLeft = new double[bufferSize];
        var lowRight = new double[bufferSize];
        var highLeft = new double[bufferSize];
        var highRight = new double[bufferSize];

        noiseLow.Process(lowLeft, lowRight, bufferSize);
        noiseHigh.Process(highLeft, highRight, bufferSize);

        // Low level should have less energy
        double energyLow = 0, energyHigh = 0;
        for (int i = 0; i < bufferSize; i++)
        {
            energyLow += lowLeft[i] * lowLeft[i];
            energyHigh += highLeft[i] * highLeft[i];
        }

        Assert.True(energyHigh > energyLow * 5, "Higher level should produce more energy");
    }

    [Fact]
    public void PinkNoise_LongRun_RmsStaysInRange()
    {
        // Bug 2.5: Voss-McCartney's incremental sum drifts over time due to FP error.
        // The fix recomputes the running sum from _pinkRows every 1024 samples.
        // We verify by running ~30 seconds of audio and checking RMS stays bounded.
        var noise = new NoiseOscillator { Type = NoiseOscillator.NoiseType.Pink, Level = 1.0 };
        noise.Reseed(0xCafe);

        const int BlockSize = 1024;
        const int Blocks = 1300; // ~30 seconds at 44.1kHz
        var left = new double[BlockSize];
        var right = new double[BlockSize];

        double maxBlockRms = 0;
        double minBlockRms = double.MaxValue;
        for (int b = 0; b < Blocks; b++)
        {
            Array.Clear(left, 0, BlockSize);
            Array.Clear(right, 0, BlockSize);
            noise.Process(left, right, BlockSize);

            double sumSq = 0;
            for (int i = 0; i < BlockSize; i++) sumSq += left[i] * left[i];
            double rms = Math.Sqrt(sumSq / BlockSize);

            maxBlockRms = Math.Max(maxBlockRms, rms);
            minBlockRms = Math.Min(minBlockRms, rms);

            // Hard fail: any block above 1.0 means drift has overflowed the [-1,1] clamp.
            Assert.False(double.IsNaN(rms), $"NaN RMS in block {b}");
            Assert.True(rms < 1.0, $"Block {b} RMS={rms} suggests drift > clamp");
        }

        // Soft check: RMS should be reasonably stable across the run.
        // Pink noise RMS ≈ 0.07-0.20 with this normalization. We just require
        // the spread to be < 5x to confirm no slow-drift trend.
        Assert.True(maxBlockRms / minBlockRms < 5.0,
            $"RMS varied wildly: min={minBlockRms} max={maxBlockRms} — possible drift");
    }
}

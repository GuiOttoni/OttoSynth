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
}

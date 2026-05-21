using System;
using Xunit;
using OttoSynth.Core.DSP.Modulation;

namespace OttoSynth.Core.Tests.DSP;

public class LfoGeneratorTests
{
    [Fact]
    public void Sine_ProducesBipolarOutput()
    {
        var lfo = new LfoGenerator();
        lfo.Shape = LfoGenerator.LfoShape.Sine;
        lfo.Rate = 1.0;
        lfo.Depth = 1.0;
        lfo.SetSampleRate(1000); // Low rate for easier testing

        int sampleCount = 1000; // Full cycle at 1Hz / 1000Hz SR
        var output = new double[sampleCount];

        lfo.Process(output, sampleCount);

        double min = double.MaxValue, max = double.MinValue;
        for (int i = 0; i < sampleCount; i++)
        {
            if (output[i] < min) min = output[i];
            if (output[i] > max) max = output[i];
        }

        Assert.True(max > 0.9, $"Sine LFO max should be near 1.0, got {max}");
        Assert.True(min < -0.9, $"Sine LFO min should be near -1.0, got {min}");
    }

    [Fact]
    public void Triangle_ProducesBipolarOutput()
    {
        var lfo = new LfoGenerator();
        lfo.Shape = LfoGenerator.LfoShape.Triangle;
        lfo.Rate = 1.0;
        lfo.Depth = 1.0;
        lfo.SetSampleRate(1000);

        int sampleCount = 1000;
        var output = new double[sampleCount];

        lfo.Process(output, sampleCount);

        double min = double.MaxValue, max = double.MinValue;
        for (int i = 0; i < sampleCount; i++)
        {
            if (output[i] < min) min = output[i];
            if (output[i] > max) max = output[i];
        }

        Assert.True(max > 0.9, $"Triangle max should be near 1.0, got {max}");
        Assert.True(min < -0.9, $"Triangle min should be near -1.0, got {min}");
    }

    [Fact]
    public void Square_ProducesOnlyOneAndMinusOne()
    {
        var lfo = new LfoGenerator();
        lfo.Shape = LfoGenerator.LfoShape.Square;
        lfo.Rate = 1.0;
        lfo.Depth = 1.0;
        lfo.SetSampleRate(1000);

        int sampleCount = 1000;
        var output = new double[sampleCount];

        lfo.Process(output, sampleCount);

        bool hasPositive = false, hasNegative = false;
        for (int i = 0; i < sampleCount; i++)
        {
            Assert.True(Math.Abs(output[i]) == 1.0 || output[i] == 0.0,
                $"Square LFO should only produce +1 or -1, got {output[i]}");
            if (output[i] > 0) hasPositive = true;
            if (output[i] < 0) hasNegative = true;
        }

        Assert.True(hasPositive && hasNegative, "Square LFO should have both positive and negative values");
    }

    [Fact]
    public void Depth_ScalesOutput()
    {
        var lfo = new LfoGenerator();
        lfo.Shape = LfoGenerator.LfoShape.Sine;
        lfo.Rate = 10.0;
        lfo.Depth = 0.5;
        lfo.SetSampleRate(44100);

        int sampleCount = 4410; // ~0.1 seconds = full cycle at 10Hz
        var output = new double[sampleCount];

        lfo.Process(output, sampleCount);

        double max = double.MinValue;
        for (int i = 0; i < sampleCount; i++)
            if (Math.Abs(output[i]) > max) max = Math.Abs(output[i]);

        Assert.True(max <= 0.51, $"With depth 0.5, max should be ≤ 0.5, got {max}");
        Assert.True(max > 0.4, $"With depth 0.5, max should be > 0.4, got {max}");
    }

    [Fact]
    public void Unipolar_OutputAlwaysPositive()
    {
        var lfo = new LfoGenerator();
        lfo.Shape = LfoGenerator.LfoShape.Sine;
        lfo.Rate = 5.0;
        lfo.Depth = 1.0;
        lfo.Unipolar = true;
        lfo.SetSampleRate(44100);

        int sampleCount = 44100;
        var output = new double[sampleCount];

        lfo.Process(output, sampleCount);

        for (int i = 0; i < sampleCount; i++)
        {
            Assert.True(output[i] >= -0.001, $"Unipolar LFO should be >= 0, got {output[i]} at sample {i}");
        }
    }

    [Fact]
    public void Retrigger_ResetsPhaseOnNoteOn()
    {
        var lfo = new LfoGenerator();
        lfo.Shape = LfoGenerator.LfoShape.SawUp;
        lfo.Rate = 1.0;
        lfo.Depth = 1.0;
        lfo.Retrigger = true;
        lfo.SetSampleRate(1000);

        // Process half a cycle
        var buf1 = new double[500];
        lfo.Process(buf1, 500);

        // NoteOn should reset
        lfo.NoteOn();

        // Process again — should start from beginning
        var buf2 = new double[100];
        lfo.Process(buf2, 100);

        // First sample after reset should be near the starting value
        Assert.True(buf2[0] < -0.5, "After retrigger, LFO should restart near -1");
    }
}

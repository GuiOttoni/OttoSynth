using System;
using Xunit;
using OttoSynth.Core.DSP.Oscillators;
using OttoSynth.Core.DSP.Utils;

namespace OttoSynth.Core.Tests.DSP;

public class WavetableOscillatorTests
{
    private const double SampleRate = 44100.0;
    private const int BufferSize = 1024;

    [Fact]
    public void Process_SineWave_OutputWithinRange()
    {
        // Arrange
        var osc = new WavetableOscillator();
        osc.SetSampleRate(SampleRate);
        osc.SetWavetable(BasicWavetables.GenerateSine(), hasMipmap: false);
        osc.SetFrequency(440.0); // A4
        osc.Level = 1.0;
        osc.Pan = 0.0;

        var left = new double[BufferSize];
        var right = new double[BufferSize];

        // Act
        osc.Process(left, right, BufferSize);

        // Assert — all samples should be within [-1, 1]
        for (int i = 0; i < BufferSize; i++)
        {
            Assert.InRange(left[i], -1.0, 1.0);
            Assert.InRange(right[i], -1.0, 1.0);
        }
    }

    [Fact]
    public void Process_SineWave_CorrectFrequency()
    {
        // Arrange
        var osc = new WavetableOscillator();
        osc.SetSampleRate(SampleRate);
        osc.SetWavetable(BasicWavetables.GenerateSine(), hasMipmap: false);
        osc.SetFrequency(440.0);
        osc.Level = 1.0;
        osc.Pan = 0.0;
        osc.ResetPhase();

        // Generate enough samples for several cycles
        int samplesToGenerate = 4096;
        var left = new double[samplesToGenerate];
        var right = new double[samplesToGenerate];

        // Act
        osc.Process(left, right, samplesToGenerate);

        // Assert — count zero crossings to estimate frequency
        int zeroCrossings = 0;
        for (int i = 1; i < samplesToGenerate; i++)
        {
            if ((left[i] >= 0 && left[i - 1] < 0) || (left[i] < 0 && left[i - 1] >= 0))
                zeroCrossings++;
        }

        // Each cycle has 2 zero crossings
        double estimatedFrequency = (zeroCrossings / 2.0) * SampleRate / samplesToGenerate;
        Assert.InRange(estimatedFrequency, 430.0, 450.0); // Allow ~2% tolerance
    }

    [Fact]
    public void Process_ZeroLevel_ProducesSilence()
    {
        // Arrange
        var osc = new WavetableOscillator();
        osc.SetSampleRate(SampleRate);
        osc.SetFrequency(440.0);
        osc.Level = 0.0;

        var left = new double[BufferSize];
        var right = new double[BufferSize];

        // Act
        osc.Process(left, right, BufferSize);

        // Assert
        for (int i = 0; i < BufferSize; i++)
        {
            Assert.Equal(0.0, left[i]);
            Assert.Equal(0.0, right[i]);
        }
    }

    [Fact]
    public void Process_PanLeft_LeftLouderThanRight()
    {
        // Arrange
        var osc = new WavetableOscillator();
        osc.SetSampleRate(SampleRate);
        osc.SetWavetable(BasicWavetables.GenerateSine(), hasMipmap: false);
        osc.SetFrequency(440.0);
        osc.Level = 1.0;
        osc.Pan = -1.0; // Full left

        var left = new double[BufferSize];
        var right = new double[BufferSize];

        // Act
        osc.Process(left, right, BufferSize);

        // Assert — left channel should have energy, right should be near-silent
        double leftEnergy = 0, rightEnergy = 0;
        for (int i = 0; i < BufferSize; i++)
        {
            leftEnergy += left[i] * left[i];
            rightEnergy += right[i] * right[i];
        }
        Assert.True(leftEnergy > rightEnergy * 10, "Full left pan should have much more energy on left");
    }

    [Fact]
    public void Process_SawWave_HasHarmonics()
    {
        // Arrange
        var osc = new WavetableOscillator();
        osc.SetSampleRate(SampleRate);
        osc.SetWavetable(BasicWavetables.GenerateSaw(), hasMipmap: true);
        osc.SetFrequency(440.0);
        osc.Level = 1.0;
        osc.Pan = 0.0;

        var left = new double[BufferSize];
        var right = new double[BufferSize];

        // Act
        osc.Process(left, right, BufferSize);

        // Assert — saw wave should have signal (non-zero RMS)
        double rms = 0;
        for (int i = 0; i < BufferSize; i++)
            rms += left[i] * left[i];
        rms = Math.Sqrt(rms / BufferSize);

        Assert.True(rms > 0.1, "Saw wave should produce significant signal");
    }

    [Fact]
    public void Process_CoarseTune_ChangesPitch()
    {
        // Arrange — two oscillators, one with +12 semitones (octave up)
        var oscBase = new WavetableOscillator();
        var oscTuned = new WavetableOscillator();

        var sine = BasicWavetables.GenerateSine();
        oscBase.SetSampleRate(SampleRate);
        oscBase.SetWavetable(sine, false);
        oscBase.SetFrequency(440.0);
        oscBase.Level = 1.0;
        oscBase.ResetPhase();

        oscTuned.SetSampleRate(SampleRate);
        oscTuned.SetWavetable(sine, false);
        oscTuned.SetFrequency(440.0);
        oscTuned.CoarseTune = 12; // Octave up
        oscTuned.Level = 1.0;
        oscTuned.ResetPhase();

        int samples = 4096;
        var leftBase = new double[samples];
        var rightBase = new double[samples];
        var leftTuned = new double[samples];
        var rightTuned = new double[samples];

        // Act
        oscBase.Process(leftBase, rightBase, samples);
        oscTuned.Process(leftTuned, rightTuned, samples);

        // Count zero crossings for both
        int crossingsBase = CountZeroCrossings(leftBase, samples);
        int crossingsTuned = CountZeroCrossings(leftTuned, samples);

        // Assert — tuned should have approximately double the zero crossings
        double ratio = (double)crossingsTuned / crossingsBase;
        Assert.InRange(ratio, 1.8, 2.2); // Octave = 2x frequency
    }

    private static int CountZeroCrossings(double[] data, int count)
    {
        int crossings = 0;
        for (int i = 1; i < count; i++)
        {
            if ((data[i] >= 0 && data[i - 1] < 0) || (data[i] < 0 && data[i - 1] >= 0))
                crossings++;
        }
        return crossings;
    }
}

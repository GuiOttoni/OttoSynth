using System;
using Xunit;
using OttoSynth.Core.DSP.Filters;

namespace OttoSynth.Core.Tests.DSP;

public class StateVariableFilterTests
{
    [Fact]
    public void LowPass_PassesLowFrequencies()
    {
        var filter = new StateVariableFilter();
        filter.SetSampleRate(44100);
        filter.Cutoff = 5000.0;
        filter.Resonance = 0.0;
        filter.Mode = StateVariableFilter.FilterMode.LowPass;

        // Generate a 200Hz sine wave (well below 5kHz cutoff)
        int sampleCount = 4410; // 0.1 seconds
        var input = new double[sampleCount];
        var output = new double[sampleCount];

        double freq = 200.0;
        for (int i = 0; i < sampleCount; i++)
            input[i] = Math.Sin(2 * Math.PI * freq * i / 44100.0);

        filter.Process(input, output, sampleCount);

        // Measure output energy (skip first 100 samples for filter warmup)
        double inputEnergy = 0, outputEnergy = 0;
        for (int i = 100; i < sampleCount; i++)
        {
            inputEnergy += input[i] * input[i];
            outputEnergy += output[i] * output[i];
        }

        // Low frequency should pass through with minimal attenuation
        double ratio = outputEnergy / inputEnergy;
        Assert.True(ratio > 0.7, $"200Hz should pass through LP@5kHz with >70% energy, got {ratio:F3}");
    }

    [Fact]
    public void LowPass_AttenuatesHighFrequencies()
    {
        var filter = new StateVariableFilter();
        filter.SetSampleRate(44100);
        filter.Cutoff = 1000.0;
        filter.Resonance = 0.0;
        filter.Mode = StateVariableFilter.FilterMode.LowPass;

        // Generate a 10kHz sine wave (well above 1kHz cutoff)
        int sampleCount = 4410;
        var input = new double[sampleCount];
        var output = new double[sampleCount];

        double freq = 10000.0;
        for (int i = 0; i < sampleCount; i++)
            input[i] = Math.Sin(2 * Math.PI * freq * i / 44100.0);

        filter.Process(input, output, sampleCount);

        double inputEnergy = 0, outputEnergy = 0;
        for (int i = 100; i < sampleCount; i++)
        {
            inputEnergy += input[i] * input[i];
            outputEnergy += output[i] * output[i];
        }

        double ratio = outputEnergy / inputEnergy;
        Assert.True(ratio < 0.1, $"10kHz should be attenuated by LP@1kHz, ratio = {ratio:F3}");
    }

    [Fact]
    public void HighPass_PassesHighFrequencies()
    {
        var filter = new StateVariableFilter();
        filter.SetSampleRate(44100);
        filter.Cutoff = 1000.0;
        filter.Resonance = 0.0;
        filter.Mode = StateVariableFilter.FilterMode.HighPass;

        // Generate a 10kHz sine wave
        int sampleCount = 4410;
        var input = new double[sampleCount];
        var output = new double[sampleCount];

        double freq = 10000.0;
        for (int i = 0; i < sampleCount; i++)
            input[i] = Math.Sin(2 * Math.PI * freq * i / 44100.0);

        filter.Process(input, output, sampleCount);

        double inputEnergy = 0, outputEnergy = 0;
        for (int i = 100; i < sampleCount; i++)
        {
            inputEnergy += input[i] * input[i];
            outputEnergy += output[i] * output[i];
        }

        double ratio = outputEnergy / inputEnergy;
        Assert.True(ratio > 0.5, $"10kHz should pass through HP@1kHz, ratio = {ratio:F3}");
    }

    [Fact]
    public void BypassWhenWideOpen()
    {
        // Bypass triggers when cutoff = 20000 Hz (the maximum allowed) with no resonance in LP mode.
        var filter = new StateVariableFilter();
        filter.SetSampleRate(44100);
        filter.Cutoff = 20000.0;
        filter.Resonance = 0.0;
        filter.Mode = StateVariableFilter.FilterMode.LowPass;

        int sampleCount = 256;
        var input = new double[sampleCount];
        var output = new double[sampleCount];

        for (int i = 0; i < sampleCount; i++)
            input[i] = Math.Sin(2 * Math.PI * 440 * i / 44100.0);

        filter.Process(input, output, sampleCount);

        // Bypass path copies input unchanged
        for (int i = 0; i < sampleCount; i++)
        {
            Assert.Equal(input[i], output[i], precision: 10);
        }
    }

    [Fact]
    public void ResetState_ClearsFilterMemory()
    {
        var filter = new StateVariableFilter();
        filter.SetSampleRate(44100);
        filter.Cutoff = 500.0;
        filter.Resonance = 0.5;

        // Process some audio
        int sampleCount = 256;
        var input = new double[sampleCount];
        var output = new double[sampleCount];
        for (int i = 0; i < sampleCount; i++)
            input[i] = Math.Sin(2 * Math.PI * 440 * i / 44100.0);

        filter.Process(input, output, sampleCount);
        filter.ResetState();

        // After reset, processing silence should produce silence
        var silence = new double[sampleCount];
        var silenceOut = new double[sampleCount];
        filter.Process(silence, silenceOut, sampleCount);

        for (int i = 0; i < sampleCount; i++)
        {
            Assert.True(Math.Abs(silenceOut[i]) < 0.0001,
                $"After reset, silence input should produce silence output, got {silenceOut[i]}");
        }
    }

    [Fact]
    public void Is24dB_SteeperSlope()
    {
        var filter12 = new StateVariableFilter();
        filter12.SetSampleRate(44100);
        filter12.Cutoff = 1000.0;
        filter12.Resonance = 0.0;
        filter12.Is24dB = false;

        var filter24 = new StateVariableFilter();
        filter24.SetSampleRate(44100);
        filter24.Cutoff = 1000.0;
        filter24.Resonance = 0.0;
        filter24.Is24dB = true;

        int sampleCount = 4410;
        var input = new double[sampleCount];
        var output12 = new double[sampleCount];
        var output24 = new double[sampleCount];

        double freq = 5000.0; // Above cutoff
        for (int i = 0; i < sampleCount; i++)
            input[i] = Math.Sin(2 * Math.PI * freq * i / 44100.0);

        filter12.Process(input, output12, sampleCount);
        filter24.Process(input, output24, sampleCount);

        double energy12 = 0, energy24 = 0;
        for (int i = 200; i < sampleCount; i++)
        {
            energy12 += output12[i] * output12[i];
            energy24 += output24[i] * output24[i];
        }

        Assert.True(energy24 < energy12,
            $"24dB should attenuate more than 12dB. 12dB energy={energy12:F4}, 24dB energy={energy24:F4}");
    }
}

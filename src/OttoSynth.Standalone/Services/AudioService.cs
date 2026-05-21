using System;
using NAudio.Wave;
using OttoSynth.Core;
using OttoSynth.Core.Diagnostics;

namespace OttoSynth.Standalone.Services;

/// <summary>
/// Manages the NAudio WaveOut output device and SynthWaveProvider lifecycle.
/// </summary>
public sealed class AudioService : IDisposable
{
    private WaveOutEvent? _waveOut;
    private SynthWaveProvider? _waveProvider;

    public const int DefaultSampleRate = 44100;

    public int SampleRate { get; private set; }
    public SynthWaveProvider? WaveProvider => _waveProvider;

    public void Initialize(SynthEngine engine, int sampleRate = DefaultSampleRate)
    {
        SampleRate = sampleRate;
        engine.Initialize(sampleRate, 256);
        engine.SelectWavetable("Saw");

        _waveProvider = new SynthWaveProvider(engine, sampleRate, channels: 2);
        _waveOut = new WaveOutEvent { DesiredLatency = 50, NumberOfBuffers = 3 };
        _waveOut.Init(_waveProvider);
        _waveOut.Play();

        Logger.Info("AudioService", $"WaveOut started — {sampleRate} Hz, latency=50ms");
    }

    public void GetLastBuffer(double[] output) => _waveProvider?.GetLastBuffer(output);

    public void Dispose()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;
    }
}

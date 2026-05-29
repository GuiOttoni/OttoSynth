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
    private SynthEngine? _engine;

    public const int DefaultSampleRate = 44100;
    public const int DefaultBufferSize = 256;

    public static readonly int[] SupportedSampleRates = { 44100, 48000, 96000, 192000 };
    public static readonly int[] SupportedBufferSizes = { 128, 256, 512, 1024 };

    public int SampleRate   { get; private set; }
    public int BufferSize   { get; private set; }
    public double LatencyMs => BufferSize > 0 && SampleRate > 0
        ? Math.Round((double)BufferSize / SampleRate * 1000.0, 1)
        : 0;

    public SynthWaveProvider? WaveProvider => _waveProvider;

    public void Initialize(SynthEngine engine, int sampleRate = DefaultSampleRate, int bufferSize = DefaultBufferSize)
    {
        _engine    = engine;
        SampleRate = sampleRate;
        BufferSize = bufferSize;

        engine.Initialize(sampleRate, bufferSize);
        engine.SelectWavetable("Saw");

        _waveProvider = new SynthWaveProvider(engine, sampleRate, channels: 2);
        _waveOut = new WaveOutEvent { DesiredLatency = 50, NumberOfBuffers = 3 };
        _waveOut.Init(_waveProvider);
        _waveOut.Play();

        Logger.Info("AudioService", $"WaveOut started — {sampleRate} Hz, buffer={bufferSize}, latency≈{LatencyMs}ms");
    }

    /// <summary>Stops the current output and restarts with new settings.</summary>
    public void Reinitialize(int sampleRate, int bufferSize)
    {
        if (_engine == null) return;
        Stop();
        Initialize(_engine, sampleRate, bufferSize);
    }

    public void GetLastBuffer(double[] output) => _waveProvider?.GetLastBuffer(output);

    private void Stop()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;
        _waveProvider = null;
    }

    public void Dispose() => Stop();
}

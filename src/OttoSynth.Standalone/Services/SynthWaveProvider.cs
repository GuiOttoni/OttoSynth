using System;
using NAudio.Wave;
using OttoSynth.Core;

namespace OttoSynth.Standalone.Services;

/// <summary>
/// NAudio IWaveProvider bridge between SynthEngine and WaveOut.
/// Captures the last rendered buffer for waveform/spectrum display.
/// </summary>
public sealed class SynthWaveProvider : IWaveProvider
{
    private readonly SynthEngine _engine;
    private readonly double[] _tempLeft = new double[4096];
    private readonly double[] _tempRight = new double[4096];
    private readonly object _lock = new();
    private readonly double[] _lastBuffer = new double[1024];

    public WaveFormat WaveFormat { get; }

    public SynthWaveProvider(SynthEngine engine, int sampleRate, int channels)
    {
        _engine = engine;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        int samples = count / (sizeof(float) * WaveFormat.Channels);
        if (samples > _tempLeft.Length) samples = _tempLeft.Length;
        _engine.ProcessAudio(_tempLeft, _tempRight, samples);

        int byteIdx = offset;
        for (int i = 0; i < samples; i++)
        {
            float l = (float)_tempLeft[i];
            float r = WaveFormat.Channels > 1 ? (float)_tempRight[i] : l;
            Buffer.BlockCopy(BitConverter.GetBytes(l), 0, buffer, byteIdx, 4); byteIdx += 4;
            if (WaveFormat.Channels > 1)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(r), 0, buffer, byteIdx, 4);
                byteIdx += 4;
            }
        }

        lock (_lock)
        {
            int copy = Math.Min(samples, _lastBuffer.Length);
            Array.Copy(_tempLeft, _lastBuffer, copy);
        }
        return count;
    }

    public void GetLastBuffer(double[] output)
    {
        lock (_lock)
        {
            int copy = Math.Min(output.Length, _lastBuffer.Length);
            Array.Copy(_lastBuffer, output, copy);
        }
    }
}

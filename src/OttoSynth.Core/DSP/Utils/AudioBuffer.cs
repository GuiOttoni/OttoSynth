using System;
using System.Runtime.CompilerServices;

namespace OttoSynth.Core.DSP.Utils;

/// <summary>
/// Pre-allocated stereo audio buffer for real-time processing.
/// Designed for zero-allocation usage in the audio thread.
/// </summary>
public sealed class AudioBuffer
{
    /// <summary>Left channel sample data.</summary>
    public double[] Left { get; }

    /// <summary>Right channel sample data.</summary>
    public double[] Right { get; }

    /// <summary>Number of samples per channel.</summary>
    public int Length { get; }

    public AudioBuffer(int maxBufferSize)
    {
        Left = new double[maxBufferSize];
        Right = new double[maxBufferSize];
        Length = maxBufferSize;
    }

    /// <summary>
    /// Clears all samples to zero.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(int sampleCount)
    {
        Array.Clear(Left, 0, sampleCount);
        Array.Clear(Right, 0, sampleCount);
    }

    /// <summary>
    /// Clears the entire buffer.
    /// </summary>
    public void Clear()
    {
        Array.Clear(Left, 0, Length);
        Array.Clear(Right, 0, Length);
    }

    /// <summary>
    /// Adds samples from another buffer into this buffer (mixing).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddFrom(AudioBuffer source, int sampleCount)
    {
        for (int i = 0; i < sampleCount; i++)
        {
            Left[i] += source.Left[i];
            Right[i] += source.Right[i];
        }
    }

    /// <summary>
    /// Adds samples from another buffer with gain applied.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddFrom(AudioBuffer source, int sampleCount, double gain)
    {
        for (int i = 0; i < sampleCount; i++)
        {
            Left[i] += source.Left[i] * gain;
            Right[i] += source.Right[i] * gain;
        }
    }

    /// <summary>
    /// Adds samples from another buffer with stereo gain.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddFrom(AudioBuffer source, int sampleCount, double gainLeft, double gainRight)
    {
        for (int i = 0; i < sampleCount; i++)
        {
            Left[i] += source.Left[i] * gainLeft;
            Right[i] += source.Right[i] * gainRight;
        }
    }

    /// <summary>
    /// Applies a gain value to all samples.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ApplyGain(int sampleCount, double gain)
    {
        for (int i = 0; i < sampleCount; i++)
        {
            Left[i] *= gain;
            Right[i] *= gain;
        }
    }

    /// <summary>
    /// Applies per-sample gain from an envelope or modulation array.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ApplyGain(int sampleCount, double[] gainPerSample)
    {
        for (int i = 0; i < sampleCount; i++)
        {
            Left[i] *= gainPerSample[i];
            Right[i] *= gainPerSample[i];
        }
    }

    /// <summary>
    /// Copies this buffer's content into float arrays (for VST3 output).
    /// </summary>
    public void CopyToFloat(float[] leftOut, float[] rightOut, int sampleCount)
    {
        for (int i = 0; i < sampleCount; i++)
        {
            leftOut[i] = (float)Left[i];
            rightOut[i] = (float)Right[i];
        }
    }

    /// <summary>
    /// Copies interleaved stereo samples to a flat array (for NAudio output).
    /// </summary>
    public void CopyToInterleaved(float[] output, int sampleCount)
    {
        for (int i = 0; i < sampleCount; i++)
        {
            output[i * 2] = (float)Left[i];
            output[i * 2 + 1] = (float)Right[i];
        }
    }
}

using System;
using System.Collections.Generic;

namespace OttoSynth.Core.DSP.Effects;

/// <summary>Describes one editable parameter of an effect.</summary>
public sealed record EffectParameter(
    string Name,
    string Label,
    double Min,
    double Max,
    double Value,
    string Unit = "",
    bool IsBipolar = false);

/// <summary>
/// Common base implementation for effects.
/// Provides default bypass/mix handling and dry-buffer storage for wet/dry blending.
/// Derived effects override ProcessInternal() to do the actual work.
/// </summary>
public abstract class EffectBase : IEffect
{
    private double _mix = 1.0;
    protected double _sampleRate = 44100.0;

    private readonly double[] _dryLeft;
    private readonly double[] _dryRight;

    /// <summary>The maximum buffer size that can be processed at once.</summary>
    public int MaxBufferSize { get; }

    public abstract string Name { get; }
    public bool Bypass { get; set; }

    /// <summary>Which stereo channel(s) this effect processes.</summary>
    public ChannelRoute Channel { get; set; } = ChannelRoute.Both;

    public double Mix
    {
        get => _mix;
        set => _mix = Math.Clamp(value, 0.0, 1.0);
    }

    protected EffectBase(int maxBufferSize = 4096)
    {
        MaxBufferSize = maxBufferSize;
        _dryLeft = new double[maxBufferSize];
        _dryRight = new double[maxBufferSize];
    }

    public virtual void SetSampleRate(double sampleRate)
    {
        _sampleRate = sampleRate;
    }

    public void Process(double[] left, double[] right, int sampleCount)
    {
        if (Bypass || sampleCount <= 0) return;
        if (sampleCount > MaxBufferSize) sampleCount = MaxBufferSize;

        // If mix == 1.0 we process directly in place (zero dry overhead)
        if (_mix >= 0.9999)
        {
            ProcessInternal(left, right, sampleCount);
            return;
        }

        if (_mix <= 0.0001)
        {
            // Fully dry: nothing to do
            return;
        }

        // Save dry signal, run wet processing, then blend
        Buffer.BlockCopy(left, 0, _dryLeft, 0, sampleCount * sizeof(double));
        Buffer.BlockCopy(right, 0, _dryRight, 0, sampleCount * sizeof(double));

        ProcessInternal(left, right, sampleCount);

        double wet = _mix;
        double dry = 1.0 - _mix;
        for (int i = 0; i < sampleCount; i++)
        {
            left[i] = left[i] * wet + _dryLeft[i] * dry;
            right[i] = right[i] * wet + _dryRight[i] * dry;
        }
    }

    /// <summary>
    /// Performs the wet-only processing. The derived effect should overwrite the buffers
    /// with the processed (wet) signal. Wet/dry blend is handled by Process().
    /// </summary>
    protected abstract void ProcessInternal(double[] left, double[] right, int sampleCount);

    public abstract void Reset();

    /// <summary>Returns descriptors for all editable parameters (current values).</summary>
    public virtual IReadOnlyList<EffectParameter> GetParameters() => [];

    /// <summary>Sets a parameter by name. Ignored if name is not recognized.</summary>
    public virtual void SetParameter(string name, double value) { }
}

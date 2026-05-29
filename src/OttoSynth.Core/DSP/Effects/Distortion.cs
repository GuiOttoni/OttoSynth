using System;
using OttoSynth.Core.DSP.Utils;

namespace OttoSynth.Core.DSP.Effects;

/// <summary>
/// Multi-mode distortion effect.
/// Supports overdrive (soft clip), waveshaping (tanh), bitcrush (sample reduction)
/// and foldback (wave folding).
/// </summary>
public sealed class Distortion : EffectBase
{
    public enum DistortionType
    {
        Overdrive,
        Waveshape,
        Bitcrush,
        Foldback
    }

    public override string Name => "Distortion";

    /// <summary>Distortion type.</summary>
    public DistortionType Type { get; set; } = DistortionType.Overdrive;

    /// <summary>Drive amount (0..1). 0 = no drive, 1 = maximum distortion.</summary>
    public double Drive { get; set; } = 0.5;

    /// <summary>Output gain compensation (0..1).</summary>
    public double OutputGain { get; set; } = 1.0;

    /// <summary>Bitcrush bit depth (1..16). Only used in Bitcrush mode.</summary>
    public double BitDepth { get; set; } = 8.0;

    public Distortion(int maxBufferSize = 4096) : base(maxBufferSize) { }

    protected override void ProcessInternal(double[] left, double[] right, int sampleCount)
    {
        // Map drive (0..1) to gain (1..40)
        double gain = 1.0 + Drive * 39.0;
        double outGain = OutputGain;

        switch (Type)
        {
            case DistortionType.Overdrive:
                for (int i = 0; i < sampleCount; i++)
                {
                    left[i] = MathUtils.SoftClip(left[i] * gain) * outGain;
                    right[i] = MathUtils.SoftClip(right[i] * gain) * outGain;
                }
                break;

            case DistortionType.Waveshape:
                for (int i = 0; i < sampleCount; i++)
                {
                    left[i] = Math.Tanh(left[i] * gain) * outGain;
                    right[i] = Math.Tanh(right[i] * gain) * outGain;
                }
                break;

            case DistortionType.Bitcrush:
            {
                double bits = Math.Clamp(BitDepth, 1.0, 16.0);
                double steps = Math.Pow(2.0, bits) - 1.0;
                double inv = 2.0 / steps;
                for (int i = 0; i < sampleCount; i++)
                {
                    double xl = left[i] * gain;
                    double xr = right[i] * gain;
                    // Quantize to steps
                    left[i] = (Math.Round((xl + 1.0) * 0.5 * steps) * inv - 1.0) * outGain;
                    right[i] = (Math.Round((xr + 1.0) * 0.5 * steps) * inv - 1.0) * outGain;
                }
                break;
            }

            case DistortionType.Foldback:
            {
                double threshold = 1.0 - Drive * 0.9; // From 1.0 down to 0.1
                if (threshold < 0.05) threshold = 0.05;
                for (int i = 0; i < sampleCount; i++)
                {
                    left[i] = Fold(left[i] * gain, threshold) * outGain;
                    right[i] = Fold(right[i] * gain, threshold) * outGain;
                }
                break;
            }
        }
    }

    private static double Fold(double x, double threshold)
    {
        while (x > threshold || x < -threshold)
        {
            if (x > threshold) x = 2.0 * threshold - x;
            else if (x < -threshold) x = -2.0 * threshold - x;
        }
        return x;
    }

    public override void Reset() { /* stateless */ }

    public override IReadOnlyList<EffectParameter> GetParameters() =>
    [
        new("Drive",      "DRIVE",    0, 1,  Drive,      ""),
        new("OutputGain", "OUT GAIN", 0, 1,  OutputGain, ""),
        new("BitDepth",   "BIT DPTH", 1, 16, BitDepth,   "bit"),
        new("Mix",        "MIX",      0, 1,  Mix,        ""),
    ];

    public override void SetParameter(string name, double value)
    {
        switch (name)
        {
            case "Drive":      Drive      = value; break;
            case "OutputGain": OutputGain = value; break;
            case "BitDepth":   BitDepth   = value; break;
            case "Mix":        Mix        = value; break;
        }
    }
}

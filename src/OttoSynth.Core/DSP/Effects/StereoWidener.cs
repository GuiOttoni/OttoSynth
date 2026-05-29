using System;
using System.Collections.Generic;

namespace OttoSynth.Core.DSP.Effects;

/// <summary>
/// Mid-Side stereo width control.
/// Width 0 = pure mono, 1 = unchanged, 2 = maximum width.
/// Always mono-compatible (no phase issues at width ≤ 1).
/// </summary>
public sealed class StereoWidener : EffectBase
{
    public override string Name => "Width";

    /// <summary>Stereo width multiplier (0=mono, 1=normal, 2=extra wide).</summary>
    public double Width { get; set; } = 1.5;

    public StereoWidener(int maxBufferSize = 4096) : base(maxBufferSize) { }

    protected override void ProcessInternal(double[] left, double[] right, int sampleCount)
    {
        double w = Math.Clamp(Width, 0.0, 2.0);
        for (int i = 0; i < sampleCount; i++)
        {
            double mid  = (left[i] + right[i]) * 0.5;
            double side = (left[i] - right[i]) * 0.5;
            left[i]  = mid + side * w;
            right[i] = mid - side * w;
        }
    }

    public override void Reset() { }

    public override IReadOnlyList<EffectParameter> GetParameters() =>
    [
        new("Width", "WIDTH", 0, 2, Width, ""),
        new("Mix",   "MIX",   0, 1, Mix,  ""),
    ];

    public override void SetParameter(string name, double value)
    {
        switch (name)
        {
            case "Width": Width = value; break;
            case "Mix":   Mix   = value; break;
        }
    }
}

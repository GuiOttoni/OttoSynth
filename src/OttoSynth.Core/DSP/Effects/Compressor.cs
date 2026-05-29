using System;
using OttoSynth.Core.DSP.Utils;

namespace OttoSynth.Core.DSP.Effects;

/// <summary>
/// Stereo feedforward compressor with soft-knee and program-dependent envelope.
/// Detection is done in the log domain (dB) for accurate behavior across
/// the full range. Attack/Release shape the gain reduction envelope.
/// </summary>
public sealed class Compressor : EffectBase
{
    public override string Name => "Compressor";

    /// <summary>Threshold in dB (-60..0). Signal above this is compressed.</summary>
    public double ThresholdDb { get; set; } = -12.0;

    /// <summary>Ratio (1..20). 1=no compression, 20=hard limiting.</summary>
    public double Ratio { get; set; } = 4.0;

    /// <summary>Attack time in seconds (0.001..0.5).</summary>
    public double Attack { get; set; } = 0.005;

    /// <summary>Release time in seconds (0.01..3).</summary>
    public double Release { get; set; } = 0.15;

    /// <summary>Soft knee width in dB (0..24).</summary>
    public double KneeDb { get; set; } = 6.0;

    /// <summary>Make-up gain in dB (-12..24).</summary>
    public double MakeupGainDb { get; set; } = 0.0;

    // Internal state: gain reduction in dB (always >= 0)
    private double _grDb;

    public Compressor(int maxBufferSize = 4096) : base(maxBufferSize) { }

    public override void SetSampleRate(double sampleRate)
    {
        base.SetSampleRate(sampleRate);
        _grDb = 0.0;
    }

    protected override void ProcessInternal(double[] left, double[] right, int sampleCount)
    {
        double attackCoeff = Math.Exp(-1.0 / (Math.Max(Attack, 0.0001) * _sampleRate));
        double releaseCoeff = Math.Exp(-1.0 / (Math.Max(Release, 0.0001) * _sampleRate));
        double threshold = ThresholdDb;
        double ratio = Math.Max(1.0, Ratio);
        double knee = Math.Max(0.0, KneeDb);
        double makeup = MathUtils.DbToLinear(MakeupGainDb);
        double halfKnee = knee * 0.5;
        double invRatio = 1.0 - 1.0 / ratio;

        for (int i = 0; i < sampleCount; i++)
        {
            // Stereo link via peak detection
            double inMax = Math.Max(Math.Abs(left[i]), Math.Abs(right[i]));
            if (inMax < 1e-10) inMax = 1e-10;

            // Input level in dB
            double inDb = 20.0 * Math.Log10(inMax);

            // Soft-knee gain reduction curve (target reduction in dB)
            double targetGrDb;
            if (inDb < threshold - halfKnee)
            {
                targetGrDb = 0.0;
            }
            else if (inDb > threshold + halfKnee)
            {
                targetGrDb = (inDb - threshold) * invRatio;
            }
            else
            {
                double x = inDb - threshold + halfKnee;
                targetGrDb = (x * x) / (2.0 * knee) * invRatio;
            }

            // Smooth gain reduction (attack rising, release falling)
            // Higher GR target = compress harder = "attack"
            double coeff = targetGrDb > _grDb ? attackCoeff : releaseCoeff;
            _grDb = targetGrDb + coeff * (_grDb - targetGrDb);

            // Apply gain reduction and makeup
            double g = MathUtils.DbToLinear(-_grDb) * makeup;
            left[i] *= g;
            right[i] *= g;
        }
    }

    public override void Reset()
    {
        _grDb = 0.0;
    }

    public override IReadOnlyList<EffectParameter> GetParameters() =>
    [
        new("ThresholdDb",  "THRESH",  -60,  0,   ThresholdDb,  "dB"),
        new("Ratio",        "RATIO",   1,    20,  Ratio,        ":1"),
        new("Attack",       "ATTACK",  0.001, 0.5, Attack,      "s"),
        new("Release",      "RELEASE", 0.01, 3,   Release,      "s"),
        new("KneeDb",       "KNEE",    0,    24,  KneeDb,       "dB"),
        new("MakeupGainDb", "MAKEUP",  -12,  24,  MakeupGainDb, "dB"),
        new("Mix",          "MIX",     0,    1,   Mix,          ""),
    ];

    public override void SetParameter(string name, double value)
    {
        switch (name)
        {
            case "ThresholdDb":  ThresholdDb  = value; break;
            case "Ratio":        Ratio        = value; break;
            case "Attack":       Attack       = value; break;
            case "Release":      Release      = value; break;
            case "KneeDb":       KneeDb       = value; break;
            case "MakeupGainDb": MakeupGainDb = value; break;
            case "Mix":          Mix          = value; break;
        }
    }
}

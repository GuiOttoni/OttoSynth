using System;
using OttoSynth.Core.DSP.Utils;

namespace OttoSynth.Core.DSP.Effects;

/// <summary>
/// Phaser effect: cascade of all-pass filters whose cutoffs are swept by an LFO.
/// Produces the characteristic phaser swooshing sound.
/// </summary>
public sealed class Phaser : EffectBase
{
    public override string Name => "Phaser";

    /// <summary>LFO rate in Hz (0.01..10).</summary>
    public double Rate { get; set; } = 0.5;

    /// <summary>Modulation depth (0..1). Controls cutoff sweep range.</summary>
    public double Depth { get; set; } = 0.7;

    /// <summary>Feedback (0..0.95). Stronger phaser when increased.</summary>
    public double Feedback { get; set; } = 0.4;

    /// <summary>Number of stages: 4, 6, 8, or 12.</summary>
    public int Stages
    {
        get => _stages;
        set => _stages = Math.Clamp(value, 2, MaxStages);
    }
    private int _stages = 6;

    private const int MaxStages = 12;
    private readonly double[] _zL = new double[MaxStages];
    private readonly double[] _zR = new double[MaxStages];
    private double _phase;
    private double _fbL, _fbR;

    public Phaser(int maxBufferSize = 4096) : base(maxBufferSize) { }

    protected override void ProcessInternal(double[] left, double[] right, int sampleCount)
    {
        double rate = Math.Clamp(Rate, 0.01, 10.0);
        double depth = Math.Clamp(Depth, 0.0, 1.0);
        double fb = Math.Clamp(Feedback, 0.0, 0.95);

        double phaseInc = MathUtils.TwoPi * rate / _sampleRate;

        // Sweep all-pass cutoff between 200 Hz and 2200 Hz scaled by depth
        double cutoffMin = 200.0;
        double cutoffMax = 200.0 + 2000.0 * depth;

        int stages = _stages;
        for (int i = 0; i < sampleCount; i++)
        {
            double lfo = (MathUtils.FastSin(_phase) + 1.0) * 0.5; // 0..1
            double cutoff = cutoffMin + (cutoffMax - cutoffMin) * lfo;

            // All-pass coefficient
            double tan = Math.Tan(Math.PI * cutoff / _sampleRate);
            double a = (tan - 1.0) / (tan + 1.0);

            double inL = left[i] + _fbL * fb;
            double inR = right[i] + _fbR * fb;

            double xL = inL;
            double xR = inR;
            for (int s = 0; s < stages; s++)
            {
                double yL = a * xL + _zL[s];
                _zL[s] = xL - a * yL;
                xL = yL;

                double yR = a * xR + _zR[s];
                _zR[s] = xR - a * yR;
                xR = yR;
            }

            _fbL = xL;
            _fbR = xR;

            // Wet = filtered signal
            left[i] = (left[i] + xL) * 0.5;
            right[i] = (right[i] + xR) * 0.5;

            _phase += phaseInc;
            if (_phase > MathUtils.TwoPi) _phase -= MathUtils.TwoPi;
        }
    }

    public override void Reset()
    {
        Array.Clear(_zL, 0, MaxStages);
        Array.Clear(_zR, 0, MaxStages);
        _phase = 0;
        _fbL = 0;
        _fbR = 0;
    }
}

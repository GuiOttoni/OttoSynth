using System;

namespace OttoSynth.Core.DSP.Effects;

/// <summary>
/// Stereo delay effect with feedback, separate L/R times, and optional ping-pong mode.
/// Uses a circular buffer for the delay line.
/// </summary>
public sealed class Delay : EffectBase
{
    public override string Name => "Delay";

    /// <summary>Left delay time in seconds (0..2).</summary>
    public double TimeLeft { get; set; } = 0.3;

    /// <summary>Right delay time in seconds (0..2).</summary>
    public double TimeRight { get; set; } = 0.45;

    /// <summary>Feedback amount (0..0.95).</summary>
    public double Feedback { get; set; } = 0.4;

    /// <summary>Ping-pong mode: left feeds into right and vice versa.</summary>
    public bool PingPong { get; set; }

    /// <summary>Low-pass filter on feedback path (0..1). 0=fully open, 1=very dark.</summary>
    public double Damping { get; set; } = 0.2;

    // Max delay is 2 seconds at 96000 Hz
    private const int MaxDelaySamples = 200_000;
    private readonly double[] _bufferL;
    private readonly double[] _bufferR;
    private int _writeIndex;

    // Damping low-pass state
    private double _dampL, _dampR;

    public Delay(int maxBufferSize = 4096) : base(maxBufferSize)
    {
        _bufferL = new double[MaxDelaySamples];
        _bufferR = new double[MaxDelaySamples];
    }

    public override void SetSampleRate(double sampleRate)
    {
        base.SetSampleRate(sampleRate);
        Reset();
    }

    protected override void ProcessInternal(double[] left, double[] right, int sampleCount)
    {
        int dlySamplesL = (int)Math.Clamp(TimeLeft * _sampleRate, 1.0, MaxDelaySamples - 1);
        int dlySamplesR = (int)Math.Clamp(TimeRight * _sampleRate, 1.0, MaxDelaySamples - 1);
        double fb = Math.Clamp(Feedback, 0.0, 0.95);
        double damp = Math.Clamp(Damping, 0.0, 0.99);
        double a = 1.0 - damp;

        for (int i = 0; i < sampleCount; i++)
        {
            int readL = (_writeIndex - dlySamplesL + MaxDelaySamples) % MaxDelaySamples;
            int readR = (_writeIndex - dlySamplesR + MaxDelaySamples) % MaxDelaySamples;

            double outL = _bufferL[readL];
            double outR = _bufferR[readR];

            // Damp the feedback signal (one-pole LP)
            _dampL = _dampL * damp + outL * a;
            _dampR = _dampR * damp + outR * a;

            double inputL = left[i];
            double inputR = right[i];

            double writeL, writeR;
            if (PingPong)
            {
                writeL = inputL + _dampR * fb;
                writeR = inputR + _dampL * fb;
            }
            else
            {
                writeL = inputL + _dampL * fb;
                writeR = inputR + _dampR * fb;
            }

            _bufferL[_writeIndex] = writeL;
            _bufferR[_writeIndex] = writeR;

            // Output = dry + delayed (wet path); base class handles dry/wet blend
            left[i] = outL;
            right[i] = outR;

            _writeIndex = (_writeIndex + 1) % MaxDelaySamples;
        }
    }

    public override void Reset()
    {
        Array.Clear(_bufferL, 0, _bufferL.Length);
        Array.Clear(_bufferR, 0, _bufferR.Length);
        _writeIndex = 0;
        _dampL = 0;
        _dampR = 0;
    }

    public override IReadOnlyList<EffectParameter> GetParameters() =>
    [
        new("TimeLeft",  "TIME L",   0, 2,   TimeLeft,  "s"),
        new("TimeRight", "TIME R",   0, 2,   TimeRight, "s"),
        new("Feedback",  "FDBK",     0, 0.95, Feedback, ""),
        new("Damping",   "DAMP",     0, 1,   Damping,   ""),
        new("Mix",       "MIX",      0, 1,   Mix,       ""),
    ];

    public override void SetParameter(string name, double value)
    {
        switch (name)
        {
            case "TimeLeft":  TimeLeft  = value; break;
            case "TimeRight": TimeRight = value; break;
            case "Feedback":  Feedback  = value; break;
            case "Damping":   Damping   = value; break;
            case "Mix":       Mix       = value; break;
        }
    }
}

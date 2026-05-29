using System;
using OttoSynth.Core.DSP.Utils;

namespace OttoSynth.Core.DSP.Effects;

/// <summary>
/// Flanger effect: short modulated delay (1-10ms) with strong feedback.
/// Produces metallic, jet-like sweeping sound.
/// </summary>
public sealed class Flanger : EffectBase
{
    public override string Name => "Flanger";

    /// <summary>LFO rate in Hz (0.01..10).</summary>
    public double Rate { get; set; } = 0.3;

    /// <summary>Modulation depth (0..1).</summary>
    public double Depth { get; set; } = 0.7;

    /// <summary>Feedback (-0.95..0.95). Negative feedback produces a different timbre.</summary>
    public double Feedback { get; set; } = 0.5;

    private const int MaxDelaySamples = 2048; // ~46ms @ 44100
    private readonly double[] _bufferL;
    private readonly double[] _bufferR;
    private int _writeIdx;
    private double _phase;

    private const double BaseDelaySeconds = 0.001; // 1ms
    private const double DepthMaxSeconds = 0.008;  // up to 8ms

    public Flanger(int maxBufferSize = 4096) : base(maxBufferSize)
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
        double rate = Math.Clamp(Rate, 0.01, 10.0);
        double depth = Math.Clamp(Depth, 0.0, 1.0);
        double fb = Math.Clamp(Feedback, -0.95, 0.95);

        double phaseInc = MathUtils.TwoPi * rate / _sampleRate;
        double baseDelay = BaseDelaySeconds * _sampleRate;
        double depthSamples = DepthMaxSeconds * depth * _sampleRate;

        for (int i = 0; i < sampleCount; i++)
        {
            double lfo = (MathUtils.FastSin(_phase) + 1.0) * 0.5; // 0..1
            double delayPos = baseDelay + depthSamples * lfo;

            double tapL = ReadLerp(_bufferL, _writeIdx - delayPos);
            double tapR = ReadLerp(_bufferR, _writeIdx - delayPos);

            double inL = left[i] + tapL * fb;
            double inR = right[i] + tapR * fb;

            _bufferL[_writeIdx] = inL;
            _bufferR[_writeIdx] = inR;

            // Output: dry + delayed
            left[i] = (left[i] + tapL) * 0.5;
            right[i] = (right[i] + tapR) * 0.5;

            _writeIdx = (_writeIdx + 1) % MaxDelaySamples;
            _phase += phaseInc;
            if (_phase > MathUtils.TwoPi) _phase -= MathUtils.TwoPi;
        }
    }

    private double ReadLerp(double[] buffer, double pos)
    {
        while (pos < 0) pos += MaxDelaySamples;
        int i0 = (int)pos % MaxDelaySamples;
        int i1 = (i0 + 1) % MaxDelaySamples;
        double frac = pos - Math.Floor(pos);
        return buffer[i0] + (buffer[i1] - buffer[i0]) * frac;
    }

    public override void Reset()
    {
        Array.Clear(_bufferL, 0, _bufferL.Length);
        Array.Clear(_bufferR, 0, _bufferR.Length);
        _writeIdx = 0;
        _phase = 0;
    }

    public override IReadOnlyList<EffectParameter> GetParameters() =>
    [
        new("Rate",     "RATE",  0.1, 5,   Rate,     "Hz"),
        new("Depth",    "DEPTH", 0,   1,   Depth,    ""),
        new("Feedback", "FDBK",  0,   0.9, Feedback, ""),
        new("Mix",      "MIX",   0,   1,   Mix,      ""),
    ];

    public override void SetParameter(string name, double value)
    {
        switch (name)
        {
            case "Rate":     Rate     = value; break;
            case "Depth":    Depth    = value; break;
            case "Feedback": Feedback = value; break;
            case "Mix":      Mix      = value; break;
        }
    }
}

using System;
using OttoSynth.Core.DSP.Utils;

namespace OttoSynth.Core.DSP.Effects;

/// <summary>
/// Chorus effect: 3-voice modulated delay with stereo spread.
/// Each voice is a short delay modulated by an LFO at a slightly different rate.
/// </summary>
public sealed class Chorus : EffectBase
{
    public override string Name => "Chorus";

    /// <summary>LFO modulation rate in Hz (0.1..5).</summary>
    public double Rate { get; set; } = 0.6;

    /// <summary>Modulation depth (0..1). Controls how much the delay time sweeps.</summary>
    public double Depth { get; set; } = 0.5;

    /// <summary>Feedback amount (0..0.9).</summary>
    public double Feedback { get; set; } = 0.2;

    private const int MaxDelaySamples = 4096; // ~93ms @ 44100
    private readonly double[] _bufferL;
    private readonly double[] _bufferR;
    private int _writeIdx;

    // LFO phase per voice
    private double _phase1, _phase2, _phase3;

    private const double BaseDelaySeconds = 0.015; // 15ms
    private const double DepthMaxSeconds = 0.012; // ±12ms

    public Chorus(int maxBufferSize = 4096) : base(maxBufferSize)
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
        double fb = Math.Clamp(Feedback, 0.0, 0.9);

        double phaseInc1 = MathUtils.TwoPi * rate / _sampleRate;
        double phaseInc2 = MathUtils.TwoPi * (rate * 0.87) / _sampleRate;
        double phaseInc3 = MathUtils.TwoPi * (rate * 1.13) / _sampleRate;

        double baseDelay = BaseDelaySeconds * _sampleRate;
        double depthSamples = DepthMaxSeconds * depth * _sampleRate;

        for (int i = 0; i < sampleCount; i++)
        {
            // 3 LFOs with stereo phase offsets
            double mod1 = MathUtils.FastSin(_phase1);
            double mod2 = MathUtils.FastSin(_phase2 + Math.PI / 3.0);
            double mod3 = MathUtils.FastSin(_phase3 + 2.0 * Math.PI / 3.0);

            double d1 = baseDelay + depthSamples * mod1;
            double d2 = baseDelay + depthSamples * mod2;
            double d3 = baseDelay + depthSamples * mod3;

            double tap1 = ReadLerp(_bufferL, _writeIdx - d1);
            double tap2 = ReadLerp(_bufferR, _writeIdx - d2);
            double tap3 = ReadLerp(_bufferL, _writeIdx - d3);
            double tap4 = ReadLerp(_bufferR, _writeIdx - d1 * 0.97);

            double outL = (left[i] * 0.7) + (tap1 + tap3) * 0.5;
            double outR = (right[i] * 0.7) + (tap2 + tap4) * 0.5;

            _bufferL[_writeIdx] = left[i] + tap1 * fb;
            _bufferR[_writeIdx] = right[i] + tap2 * fb;

            left[i] = outL;
            right[i] = outR;

            _writeIdx = (_writeIdx + 1) % MaxDelaySamples;
            _phase1 += phaseInc1;
            _phase2 += phaseInc2;
            _phase3 += phaseInc3;
            if (_phase1 > MathUtils.TwoPi) _phase1 -= MathUtils.TwoPi;
            if (_phase2 > MathUtils.TwoPi) _phase2 -= MathUtils.TwoPi;
            if (_phase3 > MathUtils.TwoPi) _phase3 -= MathUtils.TwoPi;
        }
    }

    private double ReadLerp(double[] buffer, double pos)
    {
        // Fractional read with linear interpolation
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
        _phase1 = 0;
        _phase2 = 0;
        _phase3 = 0;
    }
}

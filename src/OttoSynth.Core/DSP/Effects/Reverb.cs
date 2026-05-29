using System;

namespace OttoSynth.Core.DSP.Effects;

/// <summary>
/// Algorithmic reverb using a Feedback Delay Network (FDN).
/// Uses 4 parallel delay lines with a Hadamard mixing matrix, with damping
/// for natural decay characteristics.
/// </summary>
public sealed class Reverb : EffectBase
{
    public override string Name => "Reverb";

    /// <summary>Room size (0..1). Scales all delay times.</summary>
    public double Size { get; set; } = 0.6;

    /// <summary>Reverb decay (0..1). Higher = longer reverb tail.</summary>
    public double Decay { get; set; } = 0.7;

    /// <summary>High-frequency damping (0..1). Higher = darker reverb tail.</summary>
    public double Damping { get; set; } = 0.3;

    /// <summary>Pre-delay in seconds (0..0.2).</summary>
    public double PreDelay { get; set; } = 0.02;

    /// <summary>Stereo width (0..1). 0=mono, 1=full stereo.</summary>
    public double Width { get; set; } = 0.8;

    // FDN: 4 delay lines with prime-number-based lengths (in samples at 44100Hz)
    private static readonly int[] BaseDelays = { 1116, 1356, 1422, 1617 };

    private const int MaxDelay = 8192;
    private const int MaxPreDelay = 16384;

    private readonly double[][] _lines = new double[4][];
    private readonly int[] _writeIdx = new int[4];
    private readonly double[] _damp = new double[4];

    private readonly double[] _preDelayL;
    private readonly double[] _preDelayR;
    private int _preWriteIdx;

    public Reverb(int maxBufferSize = 4096) : base(maxBufferSize)
    {
        for (int i = 0; i < 4; i++)
        {
            _lines[i] = new double[MaxDelay];
        }
        _preDelayL = new double[MaxPreDelay];
        _preDelayR = new double[MaxPreDelay];
    }

    public override void SetSampleRate(double sampleRate)
    {
        base.SetSampleRate(sampleRate);
        Reset();
    }

    protected override void ProcessInternal(double[] left, double[] right, int sampleCount)
    {
        double sizeFactor = 0.5 + Size * 1.5; // 0.5..2.0
        double feedback = 0.5 + Decay * 0.49;  // 0.5..0.99
        double damp = Math.Clamp(Damping, 0.0, 0.99);
        double oneMinusDamp = 1.0 - damp;
        double width = Math.Clamp(Width, 0.0, 1.0);

        int preDelaySamples = (int)Math.Clamp(PreDelay * _sampleRate, 0.0, MaxPreDelay - 1);

        // Per-line delay (in samples, scaled)
        int d0 = (int)(BaseDelays[0] * sizeFactor) % MaxDelay;
        int d1 = (int)(BaseDelays[1] * sizeFactor) % MaxDelay;
        int d2 = (int)(BaseDelays[2] * sizeFactor) % MaxDelay;
        int d3 = (int)(BaseDelays[3] * sizeFactor) % MaxDelay;
        if (d0 < 1) d0 = 1;
        if (d1 < 1) d1 = 1;
        if (d2 < 1) d2 = 1;
        if (d3 < 1) d3 = 1;

        for (int i = 0; i < sampleCount; i++)
        {
            double inL = left[i];
            double inR = right[i];

            // Apply pre-delay: write current then read with offset
            _preDelayL[_preWriteIdx] = inL;
            _preDelayR[_preWriteIdx] = inR;
            int preReadIdx = (_preWriteIdx - preDelaySamples + MaxPreDelay) % MaxPreDelay;
            double preL = _preDelayL[preReadIdx];
            double preR = _preDelayR[preReadIdx];
            _preWriteIdx = (_preWriteIdx + 1) % MaxPreDelay;

            double input = (preL + preR) * 0.5; // Mono-sum for input to FDN

            // Read from each delay line
            int r0 = (_writeIdx[0] - d0 + MaxDelay) % MaxDelay;
            int r1 = (_writeIdx[1] - d1 + MaxDelay) % MaxDelay;
            int r2 = (_writeIdx[2] - d2 + MaxDelay) % MaxDelay;
            int r3 = (_writeIdx[3] - d3 + MaxDelay) % MaxDelay;

            double t0 = _lines[0][r0];
            double t1 = _lines[1][r1];
            double t2 = _lines[2][r2];
            double t3 = _lines[3][r3];

            // Apply damping (one-pole LP per line)
            _damp[0] = _damp[0] * damp + t0 * oneMinusDamp;
            _damp[1] = _damp[1] * damp + t1 * oneMinusDamp;
            _damp[2] = _damp[2] * damp + t2 * oneMinusDamp;
            _damp[3] = _damp[3] * damp + t3 * oneMinusDamp;

            // Hadamard mixing matrix (normalized 4x4)
            // M = 0.5 * | 1  1  1  1 |
            //          | 1 -1  1 -1 |
            //          | 1  1 -1 -1 |
            //          | 1 -1 -1  1 |
            double m0 = 0.5 * ( _damp[0] + _damp[1] + _damp[2] + _damp[3]);
            double m1 = 0.5 * ( _damp[0] - _damp[1] + _damp[2] - _damp[3]);
            double m2 = 0.5 * ( _damp[0] + _damp[1] - _damp[2] - _damp[3]);
            double m3 = 0.5 * ( _damp[0] - _damp[1] - _damp[2] + _damp[3]);

            // Write back (with input + feedback)
            _lines[0][_writeIdx[0]] = input + m0 * feedback;
            _lines[1][_writeIdx[1]] = input + m1 * feedback;
            _lines[2][_writeIdx[2]] = input + m2 * feedback;
            _lines[3][_writeIdx[3]] = input + m3 * feedback;

            _writeIdx[0] = (_writeIdx[0] + 1) % MaxDelay;
            _writeIdx[1] = (_writeIdx[1] + 1) % MaxDelay;
            _writeIdx[2] = (_writeIdx[2] + 1) % MaxDelay;
            _writeIdx[3] = (_writeIdx[3] + 1) % MaxDelay;

            // Output: lines 0+2 → left, 1+3 → right for stereo spread
            double outL = (t0 + t2) * 0.5;
            double outR = (t1 + t3) * 0.5;

            // Apply width (blend with mono)
            double mono = (outL + outR) * 0.5;
            outL = mono + (outL - mono) * width;
            outR = mono + (outR - mono) * width;

            left[i] = outL;
            right[i] = outR;
        }
    }

    public override void Reset()
    {
        for (int i = 0; i < 4; i++)
        {
            Array.Clear(_lines[i], 0, _lines[i].Length);
            _writeIdx[i] = 0;
            _damp[i] = 0.0;
        }
        Array.Clear(_preDelayL, 0, _preDelayL.Length);
        Array.Clear(_preDelayR, 0, _preDelayR.Length);
        _preWriteIdx = 0;
    }

    public override IReadOnlyList<EffectParameter> GetParameters() =>
    [
        new("Size",     "SIZE",      0,    1,    Size,     ""),
        new("Decay",    "DECAY",     0,    1,    Decay,    ""),
        new("Damping",  "DAMP",      0,    1,    Damping,  ""),
        new("PreDelay", "PRE-DLY",   0,    0.2,  PreDelay, "s"),
        new("Width",    "WIDTH",     0,    1,    Width,    ""),
        new("Mix",      "MIX",       0,    1,    Mix,      ""),
    ];

    public override void SetParameter(string name, double value)
    {
        switch (name)
        {
            case "Size":     Size     = value; break;
            case "Decay":    Decay    = value; break;
            case "Damping":  Damping  = value; break;
            case "PreDelay": PreDelay = value; break;
            case "Width":    Width    = value; break;
            case "Mix":      Mix      = value; break;
        }
    }
}

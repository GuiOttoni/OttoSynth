using System;
using System.Collections.Generic;
using OttoSynth.Core.DSP.Utils;

namespace OttoSynth.Core.DSP.Effects;

/// <summary>
/// Bit-depth reduction and sample-rate decimation (lo-fi effect).
/// Uses 4× oversampling to reduce quantisation aliasing.
/// </summary>
public sealed class BitCrusher : EffectBase
{
    public override string Name => "BitCrush";

    /// <summary>Effective bit depth (1..16). Lower = more quantisation noise.</summary>
    public double BitDepth { get; set; } = 8.0;

    /// <summary>Sample-rate divisor (1..32). 1 = no downsampling, 32 = very lo-fi.</summary>
    public double SampleRateDiv { get; set; } = 1.0;

    private double _holdL, _holdR;
    private int _holdCounter;

    private Oversampler? _oversampler;
    private int _lastMaxBuffer;

    public BitCrusher(int maxBufferSize = 4096) : base(maxBufferSize)
    {
        _lastMaxBuffer = maxBufferSize;
        _oversampler   = new Oversampler(4, maxBufferSize);
    }

    protected override void ProcessInternal(double[] left, double[] right, int sampleCount)
    {
        // Lazily resize if buffer grew
        if (sampleCount > _lastMaxBuffer)
        {
            _lastMaxBuffer = sampleCount;
            _oversampler   = new Oversampler(4, sampleCount);
        }

        int    div       = (int)Math.Clamp(SampleRateDiv, 1, 32);
        double bits      = Math.Clamp(BitDepth, 1.0, 16.0);
        double levels    = Math.Pow(2.0, bits - 1.0);
        double invLevels = 1.0 / levels;

        // Capture loop state for the closure (avoids repeated field access)
        double holdL = _holdL;
        double holdR = _holdR;
        int    hold  = _holdCounter;

        void Quantise(double[] l, double[] r, int n)
        {
            for (int i = 0; i < n; i++)
            {
                if (hold == 0)
                {
                    holdL = Math.Round(l[i] * levels) * invLevels;
                    holdR = Math.Round(r[i] * levels) * invLevels;
                }
                l[i] = holdL;
                r[i] = holdR;
                hold = (hold + 1) % div;
            }
        }

        _oversampler!.Process(left, right, sampleCount, Quantise);

        _holdL       = holdL;
        _holdR       = holdR;
        _holdCounter = hold;
    }

    public override void Reset()
    {
        _holdL = _holdR = 0;
        _holdCounter = 0;
        _oversampler?.Reset();
    }

    public override IReadOnlyList<EffectParameter> GetParameters() =>
    [
        new("BitDepth",      "BITS",    1,  16, BitDepth,      " bit"),
        new("SampleRateDiv", "SR DIV",  1,  32, SampleRateDiv, ":1"),
        new("Mix",           "MIX",     0,  1,  Mix,           ""),
    ];

    public override void SetParameter(string name, double value)
    {
        switch (name)
        {
            case "BitDepth":      BitDepth      = value; break;
            case "SampleRateDiv": SampleRateDiv = value; break;
            case "Mix":           Mix           = value; break;
        }
    }
}

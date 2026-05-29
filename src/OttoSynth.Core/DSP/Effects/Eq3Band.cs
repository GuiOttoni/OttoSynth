using System;

namespace OttoSynth.Core.DSP.Effects;

/// <summary>
/// 3-band parametric equalizer using biquad filters (RBJ audio cookbook).
/// Low shelf + mid peak + high shelf.
/// </summary>
public sealed class Eq3Band : EffectBase
{
    public override string Name => "EQ";

    // Low shelf
    public double LowFreq { get; set; } = 200.0;
    public double LowGainDb { get; set; } = 0.0;

    // Mid peak
    public double MidFreq { get; set; } = 1000.0;
    public double MidGainDb { get; set; } = 0.0;
    public double MidQ { get; set; } = 0.7;

    // High shelf
    public double HighFreq { get; set; } = 5000.0;
    public double HighGainDb { get; set; } = 0.0;

    // Biquad state per channel per band (low/mid/high → 6 biquads)
    private readonly Biquad _lowL = new();
    private readonly Biquad _lowR = new();
    private readonly Biquad _midL = new();
    private readonly Biquad _midR = new();
    private readonly Biquad _highL = new();
    private readonly Biquad _highR = new();

    private bool _coeffsDirty = true;
    private double _lastLowFreq, _lastLowGain;
    private double _lastMidFreq, _lastMidGain, _lastMidQ;
    private double _lastHighFreq, _lastHighGain;

    public Eq3Band(int maxBufferSize = 4096) : base(maxBufferSize) { }

    public override void SetSampleRate(double sampleRate)
    {
        base.SetSampleRate(sampleRate);
        _coeffsDirty = true;
        Reset();
    }

    private void UpdateCoeffs()
    {
        if (_lowFreq_NeedsUpdate())
        {
            _lowL.LowShelf(_sampleRate, LowFreq, LowGainDb);
            _lowR.LowShelf(_sampleRate, LowFreq, LowGainDb);
            _lastLowFreq = LowFreq;
            _lastLowGain = LowGainDb;
        }
        if (_midFreq_NeedsUpdate())
        {
            _midL.Peak(_sampleRate, MidFreq, MidQ, MidGainDb);
            _midR.Peak(_sampleRate, MidFreq, MidQ, MidGainDb);
            _lastMidFreq = MidFreq;
            _lastMidGain = MidGainDb;
            _lastMidQ = MidQ;
        }
        if (_highFreq_NeedsUpdate())
        {
            _highL.HighShelf(_sampleRate, HighFreq, HighGainDb);
            _highR.HighShelf(_sampleRate, HighFreq, HighGainDb);
            _lastHighFreq = HighFreq;
            _lastHighGain = HighGainDb;
        }
        _coeffsDirty = false;
    }

    private bool _lowFreq_NeedsUpdate() =>
        _coeffsDirty || LowFreq != _lastLowFreq || LowGainDb != _lastLowGain;
    private bool _midFreq_NeedsUpdate() =>
        _coeffsDirty || MidFreq != _lastMidFreq || MidGainDb != _lastMidGain || MidQ != _lastMidQ;
    private bool _highFreq_NeedsUpdate() =>
        _coeffsDirty || HighFreq != _lastHighFreq || HighGainDb != _lastHighGain;

    protected override void ProcessInternal(double[] left, double[] right, int sampleCount)
    {
        UpdateCoeffs();

        for (int i = 0; i < sampleCount; i++)
        {
            double l = left[i];
            double r = right[i];

            l = _lowL.Process(l);
            l = _midL.Process(l);
            l = _highL.Process(l);

            r = _lowR.Process(r);
            r = _midR.Process(r);
            r = _highR.Process(r);

            left[i] = l;
            right[i] = r;
        }
    }

    public override void Reset()
    {
        _lowL.Reset(); _lowR.Reset();
        _midL.Reset(); _midR.Reset();
        _highL.Reset(); _highR.Reset();
    }

    public override IReadOnlyList<EffectParameter> GetParameters() =>
    [
        new("LowFreq",    "LO FREQ",  20,   500,  LowFreq,    "Hz"),
        new("LowGainDb",  "LO GAIN",  -18,  18,   LowGainDb,  "dB", IsBipolar: true),
        new("MidFreq",    "MID FREQ", 200,  8000, MidFreq,    "Hz"),
        new("MidGainDb",  "MID GAIN", -18,  18,   MidGainDb,  "dB", IsBipolar: true),
        new("MidQ",       "MID Q",    0.1,  4,    MidQ,       ""),
        new("HighFreq",   "HI FREQ",  2000, 20000, HighFreq,  "Hz"),
        new("HighGainDb", "HI GAIN",  -18,  18,   HighGainDb, "dB", IsBipolar: true),
        new("Mix",        "MIX",      0,    1,    Mix,        ""),
    ];

    public override void SetParameter(string name, double value)
    {
        switch (name)
        {
            case "LowFreq":    LowFreq    = value; break;
            case "LowGainDb":  LowGainDb  = value; break;
            case "MidFreq":    MidFreq    = value; break;
            case "MidGainDb":  MidGainDb  = value; break;
            case "MidQ":       MidQ       = value; break;
            case "HighFreq":   HighFreq   = value; break;
            case "HighGainDb": HighGainDb = value; break;
            case "Mix":        Mix        = value; break;
        }
    }

    /// <summary>
    /// Simple biquad filter (RBJ audio cookbook).
    /// </summary>
    private sealed class Biquad
    {
        private double _b0 = 1, _b1, _b2;
        private double _a1, _a2;
        private double _z1L, _z2L;

        public double Process(double x)
        {
            // Direct Form II Transposed
            double y = _b0 * x + _z1L;
            _z1L = _b1 * x - _a1 * y + _z2L;
            _z2L = _b2 * x - _a2 * y;
            return y;
        }

        public void Reset()
        {
            _z1L = 0; _z2L = 0;
        }

        public void LowShelf(double sr, double freq, double gainDb)
        {
            double A = Math.Pow(10.0, gainDb / 40.0);
            double w0 = 2.0 * Math.PI * freq / sr;
            double cosW = Math.Cos(w0);
            double sinW = Math.Sin(w0);
            double S = 1.0;
            double alpha = sinW / 2.0 * Math.Sqrt((A + 1.0 / A) * (1.0 / S - 1.0) + 2.0);
            double twoSqrtAalpha = 2.0 * Math.Sqrt(A) * alpha;

            double b0 = A * ((A + 1) - (A - 1) * cosW + twoSqrtAalpha);
            double b1 = 2 * A * ((A - 1) - (A + 1) * cosW);
            double b2 = A * ((A + 1) - (A - 1) * cosW - twoSqrtAalpha);
            double a0 = (A + 1) + (A - 1) * cosW + twoSqrtAalpha;
            double a1 = -2 * ((A - 1) + (A + 1) * cosW);
            double a2 = (A + 1) + (A - 1) * cosW - twoSqrtAalpha;

            _b0 = b0 / a0;
            _b1 = b1 / a0;
            _b2 = b2 / a0;
            _a1 = a1 / a0;
            _a2 = a2 / a0;
        }

        public void HighShelf(double sr, double freq, double gainDb)
        {
            double A = Math.Pow(10.0, gainDb / 40.0);
            double w0 = 2.0 * Math.PI * freq / sr;
            double cosW = Math.Cos(w0);
            double sinW = Math.Sin(w0);
            double S = 1.0;
            double alpha = sinW / 2.0 * Math.Sqrt((A + 1.0 / A) * (1.0 / S - 1.0) + 2.0);
            double twoSqrtAalpha = 2.0 * Math.Sqrt(A) * alpha;

            double b0 = A * ((A + 1) + (A - 1) * cosW + twoSqrtAalpha);
            double b1 = -2 * A * ((A - 1) + (A + 1) * cosW);
            double b2 = A * ((A + 1) + (A - 1) * cosW - twoSqrtAalpha);
            double a0 = (A + 1) - (A - 1) * cosW + twoSqrtAalpha;
            double a1 = 2 * ((A - 1) - (A + 1) * cosW);
            double a2 = (A + 1) - (A - 1) * cosW - twoSqrtAalpha;

            _b0 = b0 / a0;
            _b1 = b1 / a0;
            _b2 = b2 / a0;
            _a1 = a1 / a0;
            _a2 = a2 / a0;
        }

        public void Peak(double sr, double freq, double q, double gainDb)
        {
            double A = Math.Pow(10.0, gainDb / 40.0);
            double w0 = 2.0 * Math.PI * freq / sr;
            double cosW = Math.Cos(w0);
            double sinW = Math.Sin(w0);
            double alpha = sinW / (2.0 * q);

            double b0 = 1 + alpha * A;
            double b1 = -2 * cosW;
            double b2 = 1 - alpha * A;
            double a0 = 1 + alpha / A;
            double a1 = -2 * cosW;
            double a2 = 1 - alpha / A;

            _b0 = b0 / a0;
            _b1 = b1 / a0;
            _b2 = b2 / a0;
            _a1 = a1 / a0;
            _a2 = a2 / a0;
        }
    }
}

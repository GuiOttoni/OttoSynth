using System;
using System.Collections.Generic;

namespace OttoSynth.Core.DSP.Effects;

/// <summary>
/// LFO-modulated amplitude effect (Tremolo).
/// In stereo mode L and R are modulated 180° out of phase for an auto-pan feel.
/// </summary>
public sealed class Tremolo : EffectBase
{
    public override string Name => "Tremolo";

    /// <summary>Modulation rate in Hz (0.1..20).</summary>
    public double Rate { get; set; } = 4.0;

    /// <summary>Modulation depth (0..1). 0 = no tremolo, 1 = silence on troughs.</summary>
    public double Depth { get; set; } = 0.6;

    /// <summary>When true, L and R are modulated in opposite phase (auto-pan).</summary>
    public bool Stereo { get; set; } = false;

    private double _phase;

    public Tremolo(int maxBufferSize = 4096) : base(maxBufferSize) { }

    protected override void ProcessInternal(double[] left, double[] right, int sampleCount)
    {
        double phaseInc = 2.0 * Math.PI * Math.Clamp(Rate, 0.1, 20.0) / _sampleRate;
        double depth = Math.Clamp(Depth, 0.0, 1.0);

        for (int i = 0; i < sampleCount; i++)
        {
            double sinVal = Math.Sin(_phase);
            double gainL = 1.0 - depth * 0.5 * (1.0 + sinVal);
            double gainR = Stereo
                ? 1.0 - depth * 0.5 * (1.0 - sinVal)
                : gainL;

            left[i]  *= gainL;
            right[i] *= gainR;

            _phase += phaseInc;
            if (_phase >= 2.0 * Math.PI) _phase -= 2.0 * Math.PI;
        }
    }

    public override void Reset() => _phase = 0.0;

    public override IReadOnlyList<EffectParameter> GetParameters() =>
    [
        new("Rate",   "RATE",   0.1,  20,  Rate,  " Hz"),
        new("Depth",  "DEPTH",  0,    1,   Depth, ""),
        new("Mix",    "MIX",    0,    1,   Mix,   ""),
    ];

    public override void SetParameter(string name, double value)
    {
        switch (name)
        {
            case "Rate":   Rate   = value; break;
            case "Depth":  Depth  = value; break;
            case "Stereo": Stereo = value > 0.5; break;
            case "Mix":    Mix    = value; break;
        }
    }
}

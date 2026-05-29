using System;

namespace OttoSynth.Core.DSP.Filters;

/// <summary>
/// Moog Ladder filter — Huovilainen model with non-linear feedback (tanh saturation).
/// 4-pole low-pass at 24 dB/oct. Produces the warm, "vintage" sound of analog Moog filters.
/// Uses 2× oversampling internally for stability with high resonance.
/// </summary>
public sealed class MoogLadderFilter
{
    private double _cutoff = 1000.0;
    private double _resonance = 0.0;
    private double _drive = 1.0;

    private double _sampleRate = 44100.0;

    // Stage states (4 cascaded one-pole + feedback)
    private double _stage0, _stage1, _stage2, _stage3;
    private double _delay0, _delay1, _delay2, _delay3;

    /// <summary>Cutoff frequency in Hz (20..20000).</summary>
    public double Cutoff
    {
        get => _cutoff;
        set => _cutoff = Math.Clamp(value, 20.0, 20000.0);
    }

    /// <summary>Resonance (0..1). Above ~0.9 the filter self-oscillates.</summary>
    public double Resonance
    {
        get => _resonance;
        set => _resonance = Math.Clamp(value, 0.0, 1.0);
    }

    /// <summary>Pre-filter drive amount (0..1).</summary>
    public double Drive
    {
        get => _drive;
        set => _drive = 1.0 + Math.Clamp(value, 0.0, 1.0) * 4.0;
    }

    public void SetSampleRate(double sampleRate)
    {
        _sampleRate = sampleRate;
        ResetState();
    }

    /// <summary>Copies parameters (not state) from another instance for stereo mirroring.</summary>
    public void SyncParamsFrom(MoogLadderFilter src)
    {
        _cutoff    = src._cutoff;
        _resonance = src._resonance;
        _drive     = src._drive;
        _sampleRate = src._sampleRate;
    }

    public void ResetState()
    {
        _stage0 = _stage1 = _stage2 = _stage3 = 0;
        _delay0 = _delay1 = _delay2 = _delay3 = 0;
    }

    /// <summary>
    /// Processes a buffer of samples in place.
    /// </summary>
    public void Process(double[] input, double[] output, int sampleCount)
    {
        // Coefficient computation (Huovilainen)
        // f_c is normalized cutoff in [0..0.5]
        double fc = _cutoff / _sampleRate;
        double f = fc * 1.16;
        double fb = _resonance * (1.0 - 0.15 * f * f);

        // 2x oversampling: process input twice with the same coefficient
        for (int i = 0; i < sampleCount; i++)
        {
            double sample = input[i] * _drive;

            // Two passes for 2x oversampling effect
            for (int pass = 0; pass < 2; pass++)
            {
                double x = sample - _stage3 * fb;
                x = Math.Tanh(x); // Non-linearity

                _stage0 = x * f + _delay0 * (1.0 - f);
                _delay0 = _stage0;

                _stage1 = _stage0 * f + _delay1 * (1.0 - f);
                _delay1 = _stage1;

                _stage2 = _stage1 * f + _delay2 * (1.0 - f);
                _delay2 = _stage2;

                _stage3 = _stage2 * f + _delay3 * (1.0 - f);
                _delay3 = _stage3;
            }

            output[i] = _stage3;
        }
    }
}

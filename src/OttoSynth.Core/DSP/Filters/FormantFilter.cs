using System;

namespace OttoSynth.Core.DSP.Filters;

/// <summary>
/// Vocal formant filter: 3 parallel Band-Pass SVFs tuned to vowel formant frequencies.
/// Vowel morphs smoothly across A→E→I→O→U as Vowel goes from 0 to 1.
/// Based on Peterson &amp; Barney (1952) average formant data.
/// </summary>
public sealed class FormantFilter
{
    // Vowel formant frequencies in Hz. Index: 0=A, 1=E, 2=I, 3=O, 4=U
    private static readonly double[,] VowelFormants =
    {
        {  730, 1090, 2440 }, // A
        {  270, 2290, 3010 }, // E
        {  390, 1990, 2550 }, // I
        {  570,  840, 2410 }, // O
        {  300,  870, 2240 }, // U
    };

    // Q values per formant (narrower bandwidth → more pronounced vowel character)
    private static readonly double[] FormantQ = { 10.0, 15.0, 20.0 };

    private double _vowel = 0.0;
    private double _shift = 1.0;
    private double _sampleRate = 44100.0;

    // SVF TPT state per formant (ic1, ic2)
    private double _ic1_1, _ic2_1;
    private double _ic1_2, _ic2_2;
    private double _ic1_3, _ic2_3;

    // Cached SVF BPF coefficients (a1, a2, a3, peakGain) per formant
    private double _a1_1, _a2_1, _a3_1, _gain_1;
    private double _a1_2, _a2_2, _a3_2, _gain_2;
    private double _a1_3, _a2_3, _a3_3, _gain_3;

    /// <summary>Vowel position (0=A, 0.25=E, 0.5=I, 0.75=O, 1.0=U).</summary>
    public double Vowel
    {
        get => _vowel;
        set { _vowel = Math.Clamp(value, 0.0, 1.0); UpdateCoefficients(); }
    }

    /// <summary>
    /// Shifts all formant frequencies by this factor.
    /// 0.5=small/high voice, 1.0=neutral, 2.0=large/deep voice.
    /// </summary>
    public double FormantShift
    {
        get => _shift;
        set { _shift = Math.Clamp(value, 0.5, 2.0); UpdateCoefficients(); }
    }

    public void SetSampleRate(double sampleRate)
    {
        _sampleRate = sampleRate;
        UpdateCoefficients();
    }

    public void SyncParamsFrom(FormantFilter src)
    {
        _vowel      = src._vowel;
        _shift      = src._shift;
        _sampleRate = src._sampleRate;
        UpdateCoefficients();
    }

    public void ResetState()
    {
        _ic1_1 = _ic2_1 = 0;
        _ic1_2 = _ic2_2 = 0;
        _ic1_3 = _ic2_3 = 0;
    }

    public void Process(double[] input, double[] output, int sampleCount)
    {
        double ic1_1 = _ic1_1, ic2_1 = _ic2_1;
        double ic1_2 = _ic1_2, ic2_2 = _ic2_2;
        double ic1_3 = _ic1_3, ic2_3 = _ic2_3;
        double a1_1 = _a1_1, a2_1 = _a2_1, a3_1 = _a3_1, g1 = _gain_1;
        double a1_2 = _a1_2, a2_2 = _a2_2, a3_2 = _a3_2, g2 = _gain_2;
        double a1_3 = _a1_3, a2_3 = _a2_3, a3_3 = _a3_3, g3 = _gain_3;

        for (int i = 0; i < sampleCount; i++)
        {
            double x = input[i];

            // F1 SVF BPF (v1 = BandPass output)
            double v3_1 = x - ic2_1;
            double v1_1 = a1_1 * ic1_1 + a2_1 * v3_1;
            double v2_1 = ic2_1 + a2_1 * ic1_1 + a3_1 * v3_1;
            ic1_1 = 2.0 * v1_1 - ic1_1;
            ic2_1 = 2.0 * v2_1 - ic2_1;

            // F2 SVF BPF
            double v3_2 = x - ic2_2;
            double v1_2 = a1_2 * ic1_2 + a2_2 * v3_2;
            double v2_2 = ic2_2 + a2_2 * ic1_2 + a3_2 * v3_2;
            ic1_2 = 2.0 * v1_2 - ic1_2;
            ic2_2 = 2.0 * v2_2 - ic2_2;

            // F3 SVF BPF
            double v3_3 = x - ic2_3;
            double v1_3 = a1_3 * ic1_3 + a2_3 * v3_3;
            double v2_3 = ic2_3 + a2_3 * ic1_3 + a3_3 * v3_3;
            ic1_3 = 2.0 * v1_3 - ic1_3;
            ic2_3 = 2.0 * v2_3 - ic2_3;

            // Sum formants with per-formant gain compensation (1/peak_gain normalises to ~unity)
            output[i] = v1_1 * g1 + v1_2 * g2 + v1_3 * g3;
        }

        _ic1_1 = ic1_1; _ic2_1 = ic2_1;
        _ic1_2 = ic1_2; _ic2_2 = ic2_2;
        _ic1_3 = ic1_3; _ic2_3 = ic2_3;
    }

    private void UpdateCoefficients()
    {
        // Interpolate formant frequencies between two adjacent vowels
        double v  = _vowel * 4.0; // 0..4
        int    i0 = (int)Math.Clamp(Math.Floor(v), 0, 3);
        double t  = v - i0;

        double f1 = (VowelFormants[i0, 0] * (1 - t) + VowelFormants[i0 + 1, 0] * t) * _shift;
        double f2 = (VowelFormants[i0, 1] * (1 - t) + VowelFormants[i0 + 1, 1] * t) * _shift;
        double f3 = (VowelFormants[i0, 2] * (1 - t) + VowelFormants[i0 + 1, 2] * t) * _shift;

        ComputeBPF(f1, FormantQ[0], out _a1_1, out _a2_1, out _a3_1, out _gain_1);
        ComputeBPF(f2, FormantQ[1], out _a1_2, out _a2_2, out _a3_2, out _gain_2);
        ComputeBPF(f3, FormantQ[2], out _a1_3, out _a2_3, out _a3_3, out _gain_3);
    }

    private void ComputeBPF(double freqHz, double q,
        out double a1, out double a2, out double a3, out double gain)
    {
        double safe = Math.Clamp(freqHz, 20.0, _sampleRate * 0.48);
        double g    = Math.Tan(Math.PI * safe / _sampleRate);
        double k    = 1.0 / q;
        a1   = 1.0 / (1.0 + g * (g + k));
        a2   = g * a1;
        a3   = g * a2;
        // SVF BPF peak gain ≈ g/k = g*Q. Invert + normalise across 3 formants (/3).
        gain = (1.0 / (g / k)) / 3.0;
    }
}

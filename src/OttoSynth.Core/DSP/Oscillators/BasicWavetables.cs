using System;

namespace OttoSynth.Core.DSP.Oscillators;

/// <summary>
/// Provides factory methods for generating basic wavetables.
/// Each wavetable is a 2D array: [frames][samples].
/// For basic shapes, there is 1 frame. Mipmap levels provide band-limited versions.
/// </summary>
public static class BasicWavetables
{
    /// <summary>Default number of samples per wavetable frame.</summary>
    public const int DefaultTableSize = 2048;

    /// <summary>Number of mipmap levels (one per octave, covering MIDI range).</summary>
    public const int MipmapLevels = 11; // Covers ~20Hz to ~20kHz

    /// <summary>
    /// Generates a sine wavetable (single frame, no harmonics — no mipmap needed).
    /// </summary>
    public static double[][] GenerateSine(int tableSize = DefaultTableSize)
    {
        var table = new double[1][];
        table[0] = new double[tableSize];

        for (int i = 0; i < tableSize; i++)
        {
            table[0][i] = Math.Sin(2.0 * Math.PI * i / tableSize);
        }

        return table;
    }

    /// <summary>
    /// Generates a sawtooth wavetable with mipmap levels for anti-aliasing.
    /// Each mipmap level has fewer harmonics to prevent aliasing at higher frequencies.
    /// </summary>
    public static double[][] GenerateSaw(int tableSize = DefaultTableSize, double sampleRate = 44100.0)
    {
        var tables = new double[MipmapLevels][];

        for (int level = 0; level < MipmapLevels; level++)
        {
            tables[level] = new double[tableSize];

            // Frequency for this mipmap level (lowest frequency that would use this level)
            double baseFreq = 20.0 * Math.Pow(2.0, level);
            int maxHarmonics = (int)(sampleRate / (2.0 * baseFreq));
            maxHarmonics = Math.Max(1, Math.Min(maxHarmonics, 512));

            // Additive synthesis: saw = sum of ((-1)^(k+1) * sin(k*x) / k)
            for (int i = 0; i < tableSize; i++)
            {
                double phase = 2.0 * Math.PI * i / tableSize;
                double value = 0.0;

                for (int k = 1; k <= maxHarmonics; k++)
                {
                    double sign = (k % 2 == 0) ? -1.0 : 1.0;
                    value += sign * Math.Sin(k * phase) / k;
                }

                tables[level][i] = value * (2.0 / Math.PI); // Normalize to [-1, 1]
            }
        }

        return tables;
    }

    /// <summary>
    /// Generates a square wavetable with mipmap levels for anti-aliasing.
    /// </summary>
    public static double[][] GenerateSquare(int tableSize = DefaultTableSize, double sampleRate = 44100.0)
    {
        var tables = new double[MipmapLevels][];

        for (int level = 0; level < MipmapLevels; level++)
        {
            tables[level] = new double[tableSize];

            double baseFreq = 20.0 * Math.Pow(2.0, level);
            int maxHarmonics = (int)(sampleRate / (2.0 * baseFreq));
            maxHarmonics = Math.Max(1, Math.Min(maxHarmonics, 512));

            // Additive synthesis: square = sum of (sin((2k-1)*x) / (2k-1)), odd harmonics only
            for (int i = 0; i < tableSize; i++)
            {
                double phase = 2.0 * Math.PI * i / tableSize;
                double value = 0.0;

                for (int k = 1; k <= maxHarmonics; k += 2)
                {
                    value += Math.Sin(k * phase) / k;
                }

                tables[level][i] = value * (4.0 / Math.PI); // Normalize to [-1, 1]
            }
        }

        return tables;
    }

    /// <summary>
    /// Generates a triangle wavetable with mipmap levels for anti-aliasing.
    /// </summary>
    public static double[][] GenerateTriangle(int tableSize = DefaultTableSize, double sampleRate = 44100.0)
    {
        var tables = new double[MipmapLevels][];

        for (int level = 0; level < MipmapLevels; level++)
        {
            tables[level] = new double[tableSize];

            double baseFreq = 20.0 * Math.Pow(2.0, level);
            int maxHarmonics = (int)(sampleRate / (2.0 * baseFreq));
            maxHarmonics = Math.Max(1, Math.Min(maxHarmonics, 512));

            // Additive synthesis: triangle = sum of ((-1)^((k-1)/2) * sin(k*x) / k^2), odd harmonics
            for (int i = 0; i < tableSize; i++)
            {
                double phase = 2.0 * Math.PI * i / tableSize;
                double value = 0.0;

                int n = 0;
                for (int k = 1; k <= maxHarmonics; k += 2)
                {
                    double sign = (n % 2 == 0) ? 1.0 : -1.0;
                    value += sign * Math.Sin(k * phase) / ((double)k * k);
                    n++;
                }

                tables[level][i] = value * (8.0 / (Math.PI * Math.PI)); // Normalize
            }
        }

        return tables;
    }

    /// <summary>25% duty-cycle pulse — bright, buzzy, thinner than square.</summary>
    public static double[][] GeneratePulse25(int tableSize = DefaultTableSize, double sampleRate = 44100.0)
        => GeneratePulse(0.25, tableSize, sampleRate);

    /// <summary>10% duty-cycle pulse — very thin, nasal, aggressive.</summary>
    public static double[][] GeneratePulse10(int tableSize = DefaultTableSize, double sampleRate = 44100.0)
        => GeneratePulse(0.10, tableSize, sampleRate);

    /// <summary>Soft sawtooth — harmonics roll off as 1/n² (warmer than saw).</summary>
    public static double[][] GenerateSoftSaw(int tableSize = DefaultTableSize, double sampleRate = 44100.0)
        => GenerateAdditive(tableSize, sampleRate,
            k => Math.Pow(-1.0, k + 1) / (k * k),
            normalize: Math.PI * Math.PI / 6.0);

    /// <summary>Bright sawtooth — harmonics roll off as 1/n^0.7 (brighter than saw).</summary>
    public static double[][] GenerateBrightSaw(int tableSize = DefaultTableSize, double sampleRate = 44100.0)
        => GenerateAdditive(tableSize, sampleRate,
            k => Math.Pow(-1.0, k + 1) / Math.Pow(k, 0.7),
            normalize: 1.8);

    /// <summary>Odd harmonics only with stronger amplitudes (more extreme than square).</summary>
    public static double[][] GenerateOddHarmonics(int tableSize = DefaultTableSize, double sampleRate = 44100.0)
        => GenerateAdditive(tableSize, sampleRate,
            k => (k % 2 == 1) ? 1.0 / k : 0.0,
            normalize: 4.0 / Math.PI);

    /// <summary>Even harmonics only — octave-heavy, hollow, cello-like.</summary>
    public static double[][] GenerateEvenHarmonics(int tableSize = DefaultTableSize, double sampleRate = 44100.0)
        => GenerateAdditive(tableSize, sampleRate,
            k => (k % 2 == 0) ? 1.0 / k : 0.0,
            normalize: 1.4);

    /// <summary>Harmonic series with 1/n^1.5 rolloff — between saw and soft, versatile.</summary>
    public static double[][] GenerateHarmonicSeries(int tableSize = DefaultTableSize, double sampleRate = 44100.0)
        => GenerateAdditive(tableSize, sampleRate,
            k => Math.Pow(-1.0, k + 1) / Math.Pow(k, 1.5),
            normalize: 1.4);

    /// <summary>Organ 8+4+2+1 footage — harmonics 1, 2, 4, 8 at equal amplitude.</summary>
    public static double[][] GenerateOrgan(int tableSize = DefaultTableSize, double sampleRate = 44100.0)
        => GenerateAdditive(tableSize, sampleRate,
            k => (k == 1 || k == 2 || k == 4 || k == 8) ? 1.0 : 0.0,
            normalize: 4.0);

    /// <summary>Violin-like — 1st harmonic weak, 2nd strong, then alternating.</summary>
    public static double[][] GenerateViolin(int tableSize = DefaultTableSize, double sampleRate = 44100.0)
        => GenerateAdditive(tableSize, sampleRate,
            k => k switch
            {
                1 => 0.4, 2 => 1.0, 3 => 0.7, 4 => 0.5,
                5 => 0.3, 6 => 0.2, 7 => 0.15, _ => 0.1 / k
            },
            normalize: 3.35);

    /// <summary>Metallic bell — inharmonic series (2.756, 5.404, ...) approximated as ratios.</summary>
    public static double[][] GenerateBell(int tableSize = DefaultTableSize, double sampleRate = 44100.0)
    {
        // Bell-like inharmonic partials at specific frequency ratios of fundamental
        double[] ratios    = [1.0, 2.756, 5.404, 8.933, 13.346, 18.64, 24.80];
        double[] amplitudes = [1.0, 0.7,   0.45,  0.28,  0.17,   0.10,  0.06];

        var tables = new double[MipmapLevels][];
        for (int level = 0; level < MipmapLevels; level++)
        {
            tables[level] = new double[tableSize];
            double baseFreq = 20.0 * Math.Pow(2.0, level);
            double nyquist = sampleRate * 0.5;

            double norm = 0;
            for (int p = 0; p < ratios.Length; p++)
                if (baseFreq * ratios[p] < nyquist) norm += amplitudes[p];
            if (norm < 0.001) norm = 1.0;

            for (int i = 0; i < tableSize; i++)
            {
                double phase = 2.0 * Math.PI * i / tableSize;
                double val = 0;
                for (int p = 0; p < ratios.Length; p++)
                {
                    double freq = baseFreq * ratios[p];
                    if (freq < nyquist)
                        val += amplitudes[p] * Math.Sin(ratios[p] * phase);
                }
                tables[level][i] = val / norm;
            }
        }
        return tables;
    }

    /// <summary>Half-rectified sine — single-polarity hump, warm and smooth.</summary>
    public static double[][] GenerateHalfSine(int tableSize = DefaultTableSize, double sampleRate = 44100.0)
        => GenerateAdditive(tableSize, sampleRate,
            k => k switch
            {
                1 => 0.5,
                _ => (k % 2 == 0) ? -2.0 / (Math.PI * (k * k - 1)) : 0.0
            },
            normalize: 1.0 / Math.PI + 0.5);

    /// <summary>Staircase — 4-step approximation, digital character.</summary>
    public static double[][] GenerateStaircase(int tableSize = DefaultTableSize, double sampleRate = 44100.0)
        => GenerateAdditive(tableSize, sampleRate,
            k => (k == 1 || k == 3 || k == 5 || k == 7) ? 1.0 / k : 0.0,
            normalize: 4.0 / Math.PI * 0.85);

    /// <summary>Warm pad — fundamental + 2nd + 5th harmonic, soft rolloff.</summary>
    public static double[][] GenerateWarmPad(int tableSize = DefaultTableSize, double sampleRate = 44100.0)
        => GenerateAdditive(tableSize, sampleRate,
            k => k switch
            {
                1 => 1.0, 2 => 0.5, 5 => 0.3, 8 => 0.15, _ => 0.05 / k
            },
            normalize: 2.1);

    /// <summary>Spectral — equal amplitudes for first 16 harmonics (harsh, electronic).</summary>
    public static double[][] GenerateSpectral(int tableSize = DefaultTableSize, double sampleRate = 44100.0)
        => GenerateAdditive(tableSize, sampleRate,
            k => k <= 16 ? 1.0 / 16.0 : 0.0,
            normalize: 1.0);

    // ─── Private Helpers ──────────────────────────────────────────

    private static double[][] GeneratePulse(double duty, int tableSize, double sampleRate)
    {
        var tables = new double[MipmapLevels][];
        for (int level = 0; level < MipmapLevels; level++)
        {
            tables[level] = new double[tableSize];
            double baseFreq = 20.0 * Math.Pow(2.0, level);
            int maxH = Math.Clamp((int)(sampleRate / (2.0 * baseFreq)), 1, 512);

            for (int i = 0; i < tableSize; i++)
            {
                double phase = 2.0 * Math.PI * i / tableSize;
                double val = 0;
                for (int k = 1; k <= maxH; k++)
                    val += Math.Sin(Math.PI * k * duty) * Math.Sin(k * phase) / k;
                tables[level][i] = val * (2.0 / Math.PI);
            }
        }
        return tables;
    }

    private static double[][] GenerateAdditive(int tableSize, double sampleRate,
        Func<int, double> getAmplitude, double normalize = 1.0)
    {
        var tables = new double[MipmapLevels][];
        for (int level = 0; level < MipmapLevels; level++)
        {
            tables[level] = new double[tableSize];
            double baseFreq = 20.0 * Math.Pow(2.0, level);
            int maxH = Math.Clamp((int)(sampleRate / (2.0 * baseFreq)), 1, 512);

            for (int i = 0; i < tableSize; i++)
            {
                double phase = 2.0 * Math.PI * i / tableSize;
                double val = 0;
                for (int k = 1; k <= maxH; k++)
                    val += getAmplitude(k) * Math.Sin(k * phase);
                tables[level][i] = val / normalize;
            }
        }
        return tables;
    }

    /// <summary>
    /// Selects the appropriate mipmap level for a given frequency.
    /// </summary>
    public static int GetMipmapLevel(double frequency)
    {
        if (frequency <= 20.0) return 0;
        int level = (int)(Math.Log2(frequency / 20.0));
        return Math.Clamp(level, 0, MipmapLevels - 1);
    }
}

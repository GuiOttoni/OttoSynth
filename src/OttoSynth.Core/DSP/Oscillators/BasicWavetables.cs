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

    /// <summary>
    /// Selects the appropriate mipmap level for a given frequency.
    /// </summary>
    /// <param name="frequency">The fundamental frequency of the note being played.</param>
    /// <returns>Index into the mipmap table array.</returns>
    public static int GetMipmapLevel(double frequency)
    {
        if (frequency <= 20.0) return 0;

        // Each level doubles the frequency: level = log2(freq/20)
        int level = (int)(Math.Log2(frequency / 20.0));
        return Math.Clamp(level, 0, MipmapLevels - 1);
    }
}

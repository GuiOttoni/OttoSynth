using System;
using System.Runtime.CompilerServices;

namespace OttoSynth.Core.DSP.Oscillators;

/// <summary>
/// Noise oscillator supporting white and pink noise generation.
/// Pink noise uses the Voss-McCartney algorithm for -3dB/octave rolloff.
/// Zero-allocation in the process loop.
/// </summary>
public sealed class NoiseOscillator
{
    public enum NoiseType
    {
        White,
        Pink
    }

    private NoiseType _noiseType;
    private double _level;

    // Fast random (xorshift64)
    private ulong _randomState;

    // Pink noise (Voss-McCartney) state — 16 octave bands
    private const int PinkRows = 16;
    private readonly double[] _pinkRows;
    private double _pinkRunningSum;
    private int _pinkIndex;
    private int _pinkIndexMask;

    // Incrementally updating `_pinkRunningSum` accumulates floating-point error.
    // Every PinkSumRefreshSamples we recompute the sum from `_pinkRows` to prevent drift.
    private const int PinkSumRefreshSamples = 1024;
    private int _pinkRefreshCounter;

    /// <summary>Type of noise to generate.</summary>
    public NoiseType Type
    {
        get => _noiseType;
        set => _noiseType = value;
    }

    /// <summary>Output level (0..1).</summary>
    public double Level
    {
        get => _level;
        set => _level = Math.Clamp(value, 0.0, 1.0);
    }

    public NoiseOscillator()
    {
        _noiseType = NoiseType.White;
        _level = 0.5;

        // Seed random state with a non-zero value
        _randomState = (ulong)Environment.TickCount64 | 1;

        // Initialize pink noise state
        _pinkRows = new double[PinkRows];
        _pinkRunningSum = 0.0;
        _pinkIndex = 0;
        _pinkIndexMask = (1 << PinkRows) - 1;
    }

    /// <summary>
    /// Reseeds the random generator (useful for deterministic testing).
    /// </summary>
    public void Reseed(ulong seed)
    {
        _randomState = seed | 1; // Ensure non-zero
    }

    /// <summary>
    /// Processes a block of noise samples and adds to the output buffers.
    /// </summary>
    public void Process(double[] outputLeft, double[] outputRight, int sampleCount)
    {
        if (_level <= 0.0001) return;

        double level = _level;

        for (int i = 0; i < sampleCount; i++)
        {
            double sample = _noiseType switch
            {
                NoiseType.White => NextWhite(),
                NoiseType.Pink => NextPink(),
                _ => 0.0
            };

            sample *= level;
            outputLeft[i] += sample;
            outputRight[i] += sample;
        }
    }

    /// <summary>
    /// Generates a single white noise sample in [-1, 1].
    /// Uses xorshift64 for speed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double NextWhite()
    {
        // xorshift64
        ulong x = _randomState;
        x ^= x << 13;
        x ^= x >> 7;
        x ^= x << 17;
        _randomState = x;

        // Convert to double in [-1, 1]
        return (double)(long)x / long.MaxValue;
    }

    /// <summary>
    /// Generates a single pink noise sample using Voss-McCartney algorithm.
    /// Each octave band updates at half the rate of the previous, giving -3dB/octave.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double NextPink()
    {
        _pinkIndex = (_pinkIndex + 1) & _pinkIndexMask;
        int k = _pinkIndex;

        if (k != 0)
        {
            // Find which rows need updating (trailing zeros of k)
            int numTrailingZeros = 0;
            int temp = k;
            while ((temp & 1) == 0)
            {
                numTrailingZeros++;
                temp >>= 1;
            }

            // Update the appropriate row
            if (numTrailingZeros < PinkRows)
            {
                _pinkRunningSum -= _pinkRows[numTrailingZeros];
                double newRandom = NextWhite() * 0.5;
                _pinkRows[numTrailingZeros] = newRandom;
                _pinkRunningSum += newRandom;
            }
        }

        // Periodically recompute the running sum from scratch to prevent
        // floating-point drift in the incremental updates.
        if (++_pinkRefreshCounter >= PinkSumRefreshSamples)
        {
            _pinkRefreshCounter = 0;
            double sum = 0.0;
            for (int r = 0; r < PinkRows; r++) sum += _pinkRows[r];
            _pinkRunningSum = sum;
        }

        // Add white noise for the top octave and normalize
        double whiteContribution = NextWhite() * (1.0 / PinkRows);
        double result = (_pinkRunningSum + whiteContribution) * 0.125; // Normalize to ~[-1, 1]

        return Math.Clamp(result, -1.0, 1.0);
    }
}

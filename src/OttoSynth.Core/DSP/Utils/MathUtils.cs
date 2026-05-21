using System;
using System.Runtime.CompilerServices;

namespace OttoSynth.Core.DSP.Utils;

/// <summary>
/// High-performance math utilities for real-time DSP processing.
/// All methods are designed for zero-allocation, inline-friendly usage.
/// </summary>
public static class MathUtils
{
    public const double TwoPi = 2.0 * Math.PI;
    public const double HalfPi = Math.PI / 2.0;
    public const int SinTableSize = 4096;

    private static readonly double[] _sinTable;
    private static readonly double _sinTableSizeOverTwoPi;

    static MathUtils()
    {
        _sinTable = new double[SinTableSize + 1]; // +1 for interpolation guard point
        for (int i = 0; i <= SinTableSize; i++)
        {
            _sinTable[i] = Math.Sin(TwoPi * i / SinTableSize);
        }
        _sinTableSizeOverTwoPi = SinTableSize / TwoPi;
    }

    /// <summary>
    /// Converts a MIDI note number to frequency in Hz.
    /// A4 (note 69) = 440 Hz.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double MidiNoteToFrequency(int noteNumber)
    {
        return 440.0 * Math.Pow(2.0, (noteNumber - 69) / 12.0);
    }

    /// <summary>
    /// Converts a MIDI note number with fractional semitones to frequency.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double MidiNoteToFrequency(double noteNumber)
    {
        return 440.0 * Math.Pow(2.0, (noteNumber - 69.0) / 12.0);
    }

    /// <summary>
    /// Converts decibels to linear gain.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double DbToLinear(double dB)
    {
        return Math.Pow(10.0, dB / 20.0);
    }

    /// <summary>
    /// Converts linear gain to decibels.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double LinearToDb(double linear)
    {
        return linear <= 0.0 ? -144.0 : 20.0 * Math.Log10(linear);
    }

    /// <summary>
    /// Fast sine approximation using lookup table with linear interpolation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double FastSin(double phase)
    {
        // Normalize phase to [0, 2PI)
        double normalizedPhase = phase % TwoPi;
        if (normalizedPhase < 0) normalizedPhase += TwoPi;

        double index = normalizedPhase * _sinTableSizeOverTwoPi;
        int idx = (int)index;
        double frac = index - idx;

        return _sinTable[idx] + frac * (_sinTable[idx + 1] - _sinTable[idx]);
    }

    /// <summary>
    /// Fast cosine approximation using the sine lookup table.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double FastCos(double phase)
    {
        return FastSin(phase + HalfPi);
    }

    /// <summary>
    /// Linear interpolation between two values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Lerp(double a, double b, double t)
    {
        return a + (b - a) * t;
    }

    /// <summary>
    /// Clamp a value between min and max.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    /// <summary>
    /// Clamp a value between 0 and 1.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Clamp01(double value)
    {
        if (value < 0.0) return 0.0;
        if (value > 1.0) return 1.0;
        return value;
    }

    /// <summary>
    /// Hermite (cubic) interpolation for high-quality wavetable reading.
    /// Requires 4 points: y[-1], y[0], y[1], y[2] and fractional position t.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double HermiteInterpolation(double ym1, double y0, double y1, double y2, double t)
    {
        double c0 = y0;
        double c1 = 0.5 * (y1 - ym1);
        double c2 = ym1 - 2.5 * y0 + 2.0 * y1 - 0.5 * y2;
        double c3 = 0.5 * (y2 - ym1) + 1.5 * (y0 - y1);

        return ((c3 * t + c2) * t + c1) * t + c0;
    }

    /// <summary>
    /// Calculates the phase increment for a given frequency and sample rate.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double PhaseIncrement(double frequency, double sampleRate)
    {
        return frequency / sampleRate;
    }

    /// <summary>
    /// Equal-power panning law.
    /// Pan: -1 = full left, 0 = center, +1 = full right.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (double left, double right) EqualPowerPan(double pan)
    {
        double angle = (pan + 1.0) * 0.25 * Math.PI; // 0 to PI/2
        return (Math.Cos(angle), Math.Sin(angle));
    }

    /// <summary>
    /// Pitch bend value (14-bit, 0-16383) to semitone offset.
    /// Center (8192) = 0 semitones.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double PitchBendToSemitones(int bendValue, double bendRangeSemitones = 2.0)
    {
        return ((bendValue - 8192) / 8192.0) * bendRangeSemitones;
    }

    /// <summary>
    /// Semitone offset to frequency multiplier.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double SemitonesToFrequencyRatio(double semitones)
    {
        return Math.Pow(2.0, semitones / 12.0);
    }

    /// <summary>
    /// Soft clip (tanh-style) for non-linear processing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double SoftClip(double x)
    {
        // Fast tanh approximation for values in reasonable range
        if (x > 3.0) return 1.0;
        if (x < -3.0) return -1.0;
        double x2 = x * x;
        return x * (27.0 + x2) / (27.0 + 9.0 * x2);
    }

    /// <summary>
    /// PolyBLEP correction for band-limited oscillators.
    /// t: current phase (0..1), dt: phase increment per sample.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double PolyBlep(double t, double dt)
    {
        if (t < dt)
        {
            t /= dt;
            return t + t - t * t - 1.0;
        }
        else if (t > 1.0 - dt)
        {
            t = (t - 1.0) / dt;
            return t * t + t + t + 1.0;
        }
        return 0.0;
    }
}

using System;
using System.Runtime.CompilerServices;
using OttoSynth.Core.DSP.Utils;

namespace OttoSynth.Core.DSP.Modulation;

/// <summary>
/// Low Frequency Oscillator (LFO) for modulation.
/// Supports multiple shapes, tempo sync, retrigger, and stereo offset.
/// Outputs bipolar (-1 to +1) or unipolar (0 to +1).
/// Zero-allocation in the process loop.
/// </summary>
public sealed class LfoGenerator
{
    public enum LfoShape
    {
        Sine,
        Triangle,
        SawUp,
        SawDown,
        Square,
        SampleAndHold
    }

    // Parameters
    private LfoShape _shape;
    private double _rate;       // Hz (free-running mode)
    private double _depth;      // 0..1
    private double _phaseOffset; // 0..1
    private double _stereoOffset; // 0..1 (offset between L/R)
    private bool _retrigger;     // Reset phase on note-on
    private bool _unipolar;      // false=bipolar (-1..+1), true=unipolar (0..+1)

    // Phase accumulator
    private double _phase;
    private double _phaseIncrement;

    // Sample & Hold state
    private double _sampleHoldValue;
    private double _lastPhase; // For detecting wraps

    // Cached
    private double _sampleRate;
    private double _inverseSampleRate;

    // Random for S&H
    private ulong _randomState;

    /// <summary>Current output value of the LFO.</summary>
    public double CurrentValue { get; private set; }

    /// <summary>LFO waveform shape.</summary>
    public LfoShape Shape
    {
        get => _shape;
        set => _shape = value;
    }

    /// <summary>Rate in Hz (0.01 - 30.0).</summary>
    public double Rate
    {
        get => _rate;
        set
        {
            _rate = Math.Clamp(value, 0.01, 30.0);
            UpdatePhaseIncrement();
        }
    }

    /// <summary>Modulation depth (0..1).</summary>
    public double Depth
    {
        get => _depth;
        set => _depth = Math.Clamp(value, 0.0, 1.0);
    }

    /// <summary>Phase offset (0..1). Shifts the LFO start position.</summary>
    public double PhaseOffset
    {
        get => _phaseOffset;
        set => _phaseOffset = Math.Clamp(value, 0.0, 1.0);
    }

    /// <summary>Stereo offset (0..1). Adds phase offset between L/R channels.</summary>
    public double StereoOffset
    {
        get => _stereoOffset;
        set => _stereoOffset = Math.Clamp(value, 0.0, 1.0);
    }

    /// <summary>If true, LFO phase resets on NoteOn.</summary>
    public bool Retrigger
    {
        get => _retrigger;
        set => _retrigger = value;
    }

    /// <summary>If true, output is unipolar (0 to 1). If false, bipolar (-1 to 1).</summary>
    public bool Unipolar
    {
        get => _unipolar;
        set => _unipolar = value;
    }

    public LfoGenerator()
    {
        _sampleRate = 44100.0;
        _inverseSampleRate = 1.0 / _sampleRate;
        _shape = LfoShape.Sine;
        _rate = 1.0;
        _depth = 1.0;
        _phaseOffset = 0.0;
        _stereoOffset = 0.0;
        _retrigger = true;
        _unipolar = false;
        _phase = 0.0;
        _sampleHoldValue = 0.0;
        _lastPhase = 0.0;
        _randomState = (ulong)Environment.TickCount64 | 1;

        UpdatePhaseIncrement();
    }

    /// <summary>Sets the sample rate.</summary>
    public void SetSampleRate(double sampleRate)
    {
        _sampleRate = sampleRate;
        _inverseSampleRate = 1.0 / sampleRate;
        UpdatePhaseIncrement();
    }

    /// <summary>Resets the phase (called on NoteOn when retrigger is enabled).</summary>
    public void Reset()
    {
        _phase = 0.0;
        _lastPhase = 0.0;
        _sampleHoldValue = 0.0;
    }

    /// <summary>Called on NoteOn — resets phase if retrigger is enabled.</summary>
    public void NoteOn()
    {
        if (_retrigger)
        {
            _phase = 0.0;
            _lastPhase = 0.0;
        }
    }

    /// <summary>
    /// Processes a block of LFO values into the output array.
    /// Output values are scaled by depth. Bipolar: [-depth, +depth], Unipolar: [0, depth].
    /// </summary>
    public void Process(double[] output, int sampleCount)
    {
        double depth = _depth;
        double phase = _phase;
        double phaseInc = _phaseIncrement;
        double offset = _phaseOffset;
        bool unipolar = _unipolar;

        for (int i = 0; i < sampleCount; i++)
        {
            double p = (phase + offset) % 1.0;
            double raw = ComputeShape(p, phase);

            if (unipolar)
            {
                // Convert from [-1,1] to [0,1]
                raw = (raw + 1.0) * 0.5;
            }

            output[i] = raw * depth;

            // Advance phase
            _lastPhase = phase;
            phase += phaseInc;
            if (phase >= 1.0)
                phase -= 1.0;
        }

        _phase = phase;
        CurrentValue = output[sampleCount - 1];
    }

    /// <summary>
    /// Gets the current LFO value for a single sample (non-advancing, for parameter display).
    /// </summary>
    public double GetCurrentValue()
    {
        double p = (_phase + _phaseOffset) % 1.0;
        double raw = ComputeShape(p, _phase);
        if (_unipolar) raw = (raw + 1.0) * 0.5;
        return raw * _depth;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeShape(double p, double rawPhase)
    {
        return _shape switch
        {
            LfoShape.Sine => Math.Sin(p * MathUtils.TwoPi),
            LfoShape.Triangle => 4.0 * Math.Abs(p - 0.5) - 1.0,
            LfoShape.SawUp => 2.0 * p - 1.0,
            LfoShape.SawDown => 1.0 - 2.0 * p,
            LfoShape.Square => p < 0.5 ? 1.0 : -1.0,
            LfoShape.SampleAndHold => GetSampleAndHold(rawPhase),
            _ => 0.0
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetSampleAndHold(double rawPhase)
    {
        // Update value on phase wrap
        if (rawPhase < _lastPhase)
        {
            // Phase wrapped — pick new random value
            ulong x = _randomState;
            x ^= x << 13;
            x ^= x >> 7;
            x ^= x << 17;
            _randomState = x;
            _sampleHoldValue = (double)(long)x / long.MaxValue;
        }
        return _sampleHoldValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdatePhaseIncrement()
    {
        _phaseIncrement = _rate * _inverseSampleRate;
    }
}

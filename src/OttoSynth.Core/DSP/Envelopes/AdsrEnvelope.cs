using System;
using System.Runtime.CompilerServices;

namespace OttoSynth.Core.DSP.Envelopes;

/// <summary>
/// ADSR (Attack-Decay-Sustain-Release) envelope generator.
/// Supports adjustable curves for each stage (linear to exponential).
/// Zero-allocation in the process loop.
/// </summary>
public sealed class AdsrEnvelope
{
    /// <summary>Envelope states.</summary>
    public enum State
    {
        Idle,
        Attack,
        Decay,
        Sustain,
        Release
    }

    // Parameters (in seconds for A, D, R; 0..1 for S)
    private double _attackTime;
    private double _decayTime;
    private double _sustainLevel;
    private double _releaseTime;

    // Curve shapes: -1 = logarithmic, 0 = linear, +1 = exponential
    private double _attackCurve;
    private double _decayCurve;
    private double _releaseCurve;

    // Internal state
    private State _state;
    private double _currentValue;
    private double _releaseStartValue;
    private double _stageProgress; // 0..1 progress within current stage
    private double _stageIncrement; // per-sample increment for stage progress

    // Cached values
    private double _sampleRate;
    private double _inverseSampleRate;

    /// <summary>Current envelope output value (0..1).</summary>
    public double CurrentValue => _currentValue;

    /// <summary>Current state of the envelope.</summary>
    public State CurrentState => _state;

    /// <summary>Whether the envelope is producing sound (not idle).</summary>
    public bool IsActive => _state != State.Idle;

    public AdsrEnvelope()
    {
        _sampleRate = 44100.0;
        _inverseSampleRate = 1.0 / _sampleRate;
        _state = State.Idle;
        _currentValue = 0.0;

        // Default envelope
        _attackTime = 0.01;  // 10ms
        _decayTime = 0.3;    // 300ms
        _sustainLevel = 0.7;
        _releaseTime = 0.5;  // 500ms
        _attackCurve = 0.0;  // linear
        _decayCurve = 0.0;
        _releaseCurve = 0.0;
    }

    /// <summary>Sets the sample rate.</summary>
    public void SetSampleRate(double sampleRate)
    {
        _sampleRate = sampleRate;
        _inverseSampleRate = 1.0 / sampleRate;
    }

    // --- Parameter Properties ---

    /// <summary>Attack time in seconds (0.001 - 10.0).</summary>
    public double AttackTime
    {
        get => _attackTime;
        set => _attackTime = Math.Clamp(value, 0.001, 10.0);
    }

    /// <summary>Decay time in seconds (0.001 - 10.0).</summary>
    public double DecayTime
    {
        get => _decayTime;
        set => _decayTime = Math.Clamp(value, 0.001, 10.0);
    }

    /// <summary>Sustain level (0.0 - 1.0).</summary>
    public double SustainLevel
    {
        get => _sustainLevel;
        set => _sustainLevel = Math.Clamp(value, 0.0, 1.0);
    }

    /// <summary>Release time in seconds (0.001 - 10.0).</summary>
    public double ReleaseTime
    {
        get => _releaseTime;
        set => _releaseTime = Math.Clamp(value, 0.001, 10.0);
    }

    /// <summary>Attack curve (-1=log, 0=linear, 1=exp).</summary>
    public double AttackCurve
    {
        get => _attackCurve;
        set => _attackCurve = Math.Clamp(value, -1.0, 1.0);
    }

    /// <summary>Decay curve (-1=log, 0=linear, 1=exp).</summary>
    public double DecayCurve
    {
        get => _decayCurve;
        set => _decayCurve = Math.Clamp(value, -1.0, 1.0);
    }

    /// <summary>Release curve (-1=log, 0=linear, 1=exp).</summary>
    public double ReleaseCurve
    {
        get => _releaseCurve;
        set => _releaseCurve = Math.Clamp(value, -1.0, 1.0);
    }

    /// <summary>
    /// Triggers the envelope (note on).
    /// </summary>
    public void NoteOn()
    {
        _state = State.Attack;
        _stageProgress = 0.0;
        _stageIncrement = CalculateIncrement(_attackTime);
        // Don't reset _currentValue — allows smooth retrigger from current position
    }

    /// <summary>
    /// Releases the envelope (note off).
    /// </summary>
    public void NoteOff()
    {
        if (_state == State.Idle) return;

        _state = State.Release;
        _releaseStartValue = _currentValue;
        _stageProgress = 0.0;
        _stageIncrement = CalculateIncrement(_releaseTime);
    }

    /// <summary>
    /// Resets the envelope to idle state with zero output.
    /// </summary>
    public void Reset()
    {
        _state = State.Idle;
        _currentValue = 0.0;
        _stageProgress = 0.0;
    }

    /// <summary>
    /// Forces a rapid fadeout for voice stealing (anti-click).
    /// </summary>
    public void ForceRelease(double fadeTimeMs = 3.0)
    {
        _state = State.Release;
        _releaseStartValue = _currentValue;
        _stageProgress = 0.0;
        _stageIncrement = CalculateIncrement(fadeTimeMs / 1000.0);
    }

    /// <summary>
    /// Processes a block of envelope values into the output array.
    /// Returns the number of active samples (0 if went idle during processing).
    /// </summary>
    /// <param name="output">Array to write envelope values into.</param>
    /// <param name="sampleCount">Number of samples to process.</param>
    /// <returns>True if the envelope is still active after processing.</returns>
    public bool Process(double[] output, int sampleCount)
    {
        for (int i = 0; i < sampleCount; i++)
        {
            ProcessSample();
            output[i] = _currentValue;
        }

        return _state != State.Idle;
    }

    /// <summary>
    /// Processes a single envelope sample.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessSample()
    {
        switch (_state)
        {
            case State.Idle:
                _currentValue = 0.0;
                break;

            case State.Attack:
                _stageProgress += _stageIncrement;
                if (_stageProgress >= 1.0)
                {
                    _currentValue = 1.0;
                    _state = State.Decay;
                    _stageProgress = 0.0;
                    _stageIncrement = CalculateIncrement(_decayTime);
                }
                else
                {
                    _currentValue = ApplyCurve(_stageProgress, _attackCurve);
                }
                break;

            case State.Decay:
                _stageProgress += _stageIncrement;
                if (_stageProgress >= 1.0)
                {
                    _currentValue = _sustainLevel;
                    _state = State.Sustain;
                }
                else
                {
                    double shaped = ApplyCurve(_stageProgress, _decayCurve);
                    _currentValue = 1.0 - (1.0 - _sustainLevel) * shaped;
                }
                break;

            case State.Sustain:
                _currentValue = _sustainLevel;
                break;

            case State.Release:
                _stageProgress += _stageIncrement;
                if (_stageProgress >= 1.0)
                {
                    _currentValue = 0.0;
                    _state = State.Idle;
                }
                else
                {
                    double shaped = ApplyCurve(_stageProgress, _releaseCurve);
                    _currentValue = _releaseStartValue * (1.0 - shaped);
                }
                break;
        }
    }

    /// <summary>
    /// Applies a curve shape to a linear value (0..1).
    /// curve: -1 = logarithmic, 0 = linear, +1 = exponential.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ApplyCurve(double linearValue, double curve)
    {
        if (curve == 0.0)
            return linearValue;

        if (curve > 0.0)
        {
            // Exponential: more time at low values, quick ramp at end
            return Math.Pow(linearValue, 1.0 + curve * 3.0);
        }
        else
        {
            // Logarithmic: quick ramp at start, more time at high values
            return 1.0 - Math.Pow(1.0 - linearValue, 1.0 + (-curve) * 3.0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateIncrement(double timeInSeconds)
    {
        double samples = timeInSeconds * _sampleRate;
        return samples > 0 ? 1.0 / samples : 1.0;
    }
}

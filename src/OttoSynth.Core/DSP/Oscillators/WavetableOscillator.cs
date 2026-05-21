using System;
using System.Runtime.CompilerServices;
using OttoSynth.Core.DSP.Utils;

namespace OttoSynth.Core.DSP.Oscillators;

/// <summary>
/// Wavetable oscillator with Hermite interpolation and mipmap-based anti-aliasing.
/// Supports multi-frame wavetables with smooth morphing between frames.
/// Designed for zero-allocation in the audio processing loop.
///
/// Includes per-oscillator "Warp" modes (Serum/Vital-inspired) that distort
/// the phase or amplitude as the wavetable is read.
/// </summary>
public sealed class WavetableOscillator
{
    /// <summary>
    /// Per-oscillator warp modes. They re-shape how the wavetable is read,
    /// producing additional harmonics without changing the underlying table.
    /// </summary>
    public enum WaveWarp
    {
        /// <summary>No warp (clean wavetable).</summary>
        None,
        /// <summary>Phase distortion: bends the phase non-linearly (Serum's "Bend +").</summary>
        Bend,
        /// <summary>Asymmetric phase: stretches one half of the cycle (Vital's "Asym").</summary>
        Asym,
        /// <summary>Hard sync: phase wraps multiple times per logical cycle.</summary>
        Sync,
        /// <summary>FM-like: cross-modulates phase with a fast sine.</summary>
        FM,
        /// <summary>Wave folding: folds the amplitude back when it exceeds a threshold.</summary>
        Fold,
        /// <summary>Soft saturation (tanh-based, before output).</summary>
        Drive,
    }

    // Wavetable data: [mipmapLevel][sample] for single-frame, or [frame][sample] for multi-frame
    private double[][] _wavetable;
    private int _tableSize;
    private int _frameCount;
    private bool _hasMipmap;

    // Phase accumulator
    private double _phase;
    private double _phaseIncrement;

    // Parameters
    private double _frequency;
    private double _level;
    private double _pan;
    private double _wavetablePosition; // 0..1 for morphing between frames
    private int _coarseTune; // semitones
    private double _fineTune; // cents

    // Warp (Serum/Vital-style per-oscillator effect)
    private WaveWarp _warp;
    private double _warpAmount; // 0..1
    private double _fmPhase; // Internal phase for FM warp

    // Cached values
    private double _sampleRate;
    private double _inverseSampleRate;
    private double _panLeft;
    private double _panRight;

    public WavetableOscillator()
    {
        _sampleRate = 44100.0;
        _inverseSampleRate = 1.0 / _sampleRate;
        _level = 0.8;
        _pan = 0.0;
        _wavetablePosition = 0.0;
        _coarseTune = 0;
        _fineTune = 0.0;
        _phase = 0.0;

        UpdatePan();

        // Load default sine wavetable
        SetWavetable(BasicWavetables.GenerateSine(), hasMipmap: false);
    }

    /// <summary>
    /// Sets the sample rate. Called when the host changes sample rate.
    /// </summary>
    public void SetSampleRate(double sampleRate)
    {
        _sampleRate = sampleRate;
        _inverseSampleRate = 1.0 / sampleRate;
        UpdatePhaseIncrement();
    }

    /// <summary>
    /// Loads a wavetable. If hasMipmap is true, each index is a mipmap level.
    /// If false, each index is a wavetable frame for morphing.
    /// </summary>
    public void SetWavetable(double[][] wavetable, bool hasMipmap)
    {
        _wavetable = wavetable;
        _frameCount = wavetable.Length;
        _tableSize = wavetable[0].Length;
        _hasMipmap = hasMipmap;
    }

    /// <summary>
    /// Sets the base frequency for this oscillator (before tuning).
    /// Typically called from NoteOn with MIDI note frequency.
    /// </summary>
    public void SetFrequency(double frequency)
    {
        _frequency = frequency;
        UpdatePhaseIncrement();
    }

    /// <summary>Level/volume of this oscillator (0..1).</summary>
    public double Level
    {
        get => _level;
        set => _level = MathUtils.Clamp01(value);
    }

    /// <summary>Pan position (-1=left, 0=center, 1=right).</summary>
    public double Pan
    {
        get => _pan;
        set
        {
            _pan = MathUtils.Clamp(value, -1.0, 1.0);
            UpdatePan();
        }
    }

    /// <summary>Wavetable position for morphing (0..1).</summary>
    public double WavetablePosition
    {
        get => _wavetablePosition;
        set => _wavetablePosition = MathUtils.Clamp01(value);
    }

    /// <summary>Coarse tuning in semitones (-48..+48).</summary>
    public int CoarseTune
    {
        get => _coarseTune;
        set
        {
            _coarseTune = Math.Clamp(value, -48, 48);
            UpdatePhaseIncrement();
        }
    }

    /// <summary>Fine tuning in cents (-100..+100).</summary>
    public double FineTune
    {
        get => _fineTune;
        set
        {
            _fineTune = Math.Clamp(value, -100.0, 100.0);
            UpdatePhaseIncrement();
        }
    }

    /// <summary>Warp mode (Serum/Vital-style per-oscillator effect).</summary>
    public WaveWarp Warp
    {
        get => _warp;
        set => _warp = value;
    }

    /// <summary>Warp amount (0..1). 0 = no warp.</summary>
    public double WarpAmount
    {
        get => _warpAmount;
        set => _warpAmount = MathUtils.Clamp01(value);
    }

    /// <summary>
    /// Resets the phase accumulator (called on NoteOn with phase reset).
    /// </summary>
    public void ResetPhase()
    {
        _phase = 0.0;
    }

    /// <summary>
    /// Processes a block of audio samples into the output buffer.
    /// This is the main audio generation method — zero allocation.
    /// </summary>
    /// <param name="outputLeft">Left channel output buffer.</param>
    /// <param name="outputRight">Right channel output buffer.</param>
    /// <param name="sampleCount">Number of samples to generate.</param>
    public void Process(double[] outputLeft, double[] outputRight, int sampleCount)
    {
        if (_wavetable == null || _level <= 0.0001)
        {
            return; // Silent — don't process
        }

        double level = _level;
        double panL = _panLeft;
        double panR = _panRight;
        double phase = _phase;
        double phaseInc = _phaseIncrement;
        int tableSize = _tableSize;
        int tableMask = tableSize - 1; // Assumes power-of-2 table size
        WaveWarp warp = _warp;
        double warpAmt = _warpAmount;
        bool warpActive = warp != WaveWarp.None && warpAmt > 0.0001;

        // Select the appropriate wavetable frame/mipmap level
        double[] table = SelectTable();

        // FM warp uses a modulator running 7× faster than the carrier
        double fmInc = phaseInc * 7.0;

        for (int i = 0; i < sampleCount; i++)
        {
            // Apply phase warp (modify the read position based on warp mode)
            double warpedPhase = phase;
            if (warpActive)
                warpedPhase = ApplyPhaseWarp(phase, warp, warpAmt, ref _fmPhase, fmInc);

            // Read wavetable with Hermite interpolation
            double readIndex = warpedPhase * tableSize;
            int idx0 = (int)readIndex;
            double frac = readIndex - idx0;

            // Four points for Hermite interpolation (with wrapping)
            int idxM1 = (idx0 - 1) & tableMask;
            int idx1 = (idx0 + 1) & tableMask;
            int idx2 = (idx0 + 2) & tableMask;
            idx0 &= tableMask;

            double sample = MathUtils.HermiteInterpolation(
                table[idxM1], table[idx0], table[idx1], table[idx2], frac);

            // Apply amplitude warp (after wavetable read)
            if (warpActive)
                sample = ApplyAmpWarp(sample, warp, warpAmt);

            // Apply level and panning
            sample *= level;
            outputLeft[i] += sample * panL;
            outputRight[i] += sample * panR;

            // Advance phase
            phase += phaseInc;
            if (phase >= 1.0)
                phase -= 1.0;
        }

        _phase = phase;
    }

    /// <summary>
    /// Applies a phase-domain warp transformation.
    /// Returns the modified read position in [0, 1).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ApplyPhaseWarp(double phase, WaveWarp warp, double amount, ref double fmPhase, double fmInc)
    {
        switch (warp)
        {
            case WaveWarp.Bend:
            {
                // Phase distortion: bends the curve like a soft S
                // amount=0 → identity, amount=1 → strong S curve
                double t = phase;
                double a = amount * 0.9;
                // Smoothstep + linear blend
                double smooth = t * t * (3.0 - 2.0 * t);
                return t + (smooth - t) * a;
            }

            case WaveWarp.Asym:
            {
                // Asymmetric: speeds up the first half, slows down the second
                double split = 0.5 - amount * 0.45; // 0.5..0.05
                if (phase < split)
                    return phase * 0.5 / split;
                else
                    return 0.5 + (phase - split) * 0.5 / (1.0 - split);
            }

            case WaveWarp.Sync:
            {
                // Hard sync: the read phase cycles faster than the master phase
                // amount=0 → 1× sync (no change), amount=1 → 4× sync
                double mult = 1.0 + amount * 3.0;
                double scaled = phase * mult;
                return scaled - Math.Floor(scaled);
            }

            case WaveWarp.FM:
            {
                // FM-like phase modulation
                fmPhase += fmInc;
                if (fmPhase >= 1.0) fmPhase -= 1.0;
                double mod = MathUtils.FastSin(fmPhase * MathUtils.TwoPi);
                double p = phase + mod * amount * 0.15;
                // Wrap into [0,1)
                p -= Math.Floor(p);
                return p;
            }

            default:
                return phase;
        }
    }

    /// <summary>
    /// Applies an amplitude-domain warp transformation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ApplyAmpWarp(double sample, WaveWarp warp, double amount)
    {
        switch (warp)
        {
            case WaveWarp.Fold:
            {
                // Wave folding: amplifies and folds the signal
                double gain = 1.0 + amount * 4.0;
                double x = sample * gain;
                while (x > 1.0) x = 2.0 - x;
                while (x < -1.0) x = -2.0 - x;
                return x;
            }

            case WaveWarp.Drive:
            {
                // Soft saturation (tanh-style)
                double gain = 1.0 + amount * 9.0;
                return MathUtils.SoftClip(sample * gain);
            }

            default:
                return sample;
        }
    }

    /// <summary>
    /// Selects the appropriate wavetable based on frequency (mipmap) or position (morphing).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double[] SelectTable()
    {
        if (_hasMipmap)
        {
            // Mipmap: select based on frequency
            double actualFreq = _frequency * MathUtils.SemitonesToFrequencyRatio(
                _coarseTune + _fineTune / 100.0);
            int level = BasicWavetables.GetMipmapLevel(actualFreq);
            level = Math.Clamp(level, 0, _frameCount - 1);
            return _wavetable[level];
        }
        else if (_frameCount == 1)
        {
            return _wavetable[0];
        }
        else
        {
            // Multi-frame: morph based on wavetable position
            // For now, return nearest frame. Phase 2 will add interpolation between frames.
            int frameIndex = (int)(_wavetablePosition * (_frameCount - 1));
            frameIndex = Math.Clamp(frameIndex, 0, _frameCount - 1);
            return _wavetable[frameIndex];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdatePhaseIncrement()
    {
        double tuningRatio = MathUtils.SemitonesToFrequencyRatio(
            _coarseTune + _fineTune / 100.0);
        _phaseIncrement = (_frequency * tuningRatio) * _inverseSampleRate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdatePan()
    {
        (_panLeft, _panRight) = MathUtils.EqualPowerPan(_pan);
    }
}

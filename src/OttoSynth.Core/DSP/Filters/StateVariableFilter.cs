using System;
using System.Runtime.CompilerServices;

namespace OttoSynth.Core.DSP.Filters;

/// <summary>
/// Multi-mode filter that routes to different DSP algorithms based on FilterMode.
///
/// SVF modes use the Simper TPT topology (unconditionally stable, used in Surge/Vital).
/// K35 modes emulate the Korg MS-20 two-pole filter with tanh feedback.
/// MoogLadder delegates to the Huovilainen 4-pole ladder.
/// Comb modes create metallic resonances via a delay line.
/// </summary>
public sealed class StateVariableFilter
{
    // ─── Filter mode enum ────────────────────────────────────────

    public enum FilterMode
    {
        // ── Simper TPT State Variable Filter (unconditionally stable)
        LowPass,       // LP  12dB/oct
        HighPass,      // HP  12dB/oct
        BandPass,      // BP   6dB/oct peak
        Notch,         // Notch 12dB/oct
        AllPass,       // All-pass (phase rotation only)
        Peak,          // Peaking EQ / Bell

        // ── Moog Ladder (4-pole, 24 dB/oct, warm)
        MoogLadder,

        // ── K35  (Korg MS-20 inspired, non-linear 2-pole)
        K35LP,
        K35HP,

        // ── Comb
        CombPositive,  // IIR comb — adds resonant peaks
        CombNegative,  // FIR comb — creates notches

        // ── Formant (vocal tract simulation)
        Formant,       // 3 parallel BP filters morphing A→E→I→O→U
    }

    // ─── Public parameters ───────────────────────────────────────

    private FilterMode _mode     = FilterMode.LowPass;
    private double _cutoff       = 20000.0;
    private double _resonance    = 0.0;
    private double _drive        = 0.0;
    private double _keyTracking  = 0.0;
    private bool   _is24dB       = false;
    private double _sampleRate   = 44100.0;
    private double _baseCutoff   = 20000.0;
    private double _noteFrequency = 440.0;
    private bool   _bypass       = true;

    // ─── Simper TPT SVF state ─────────────────────────────────────
    // ic1eq / ic2eq are the "capacitor voltages" in the TPT topology.

    private double _ic1, _ic2;         // stage 1
    private double _ic1b, _ic2b;       // stage 2 (for 24 dB cascade)

    // Cached Simper coefficients (updated in UpdateCoefficients)
    private double _a1, _a2, _a3, _svfK;

    // ─── Moog Ladder ─────────────────────────────────────────────

    private readonly MoogLadderFilter _moog = new();

    // ─── Formant ─────────────────────────────────────────────────

    private readonly FormantFilter _formant = new();

    // ─── K35 state ───────────────────────────────────────────────

    private double _k35G, _k35K;          // freq and resonance coefficients
    private double _k35S1L, _k35S2L;      // LP stage states
    private double _k35S1H, _k35S2H;      // HP stage states
    private double _k35FbL, _k35FbH;      // 1-sample delayed output for feedback

    // ─── Comb state ───────────────────────────────────────────────

    private const int CombBufSize = 8192;
    private readonly double[] _combBuf = new double[CombBufSize];
    private int    _combWritePos;
    private double _combLpState;     // one-pole LP in feedback path (damping)
    private int    _combDelayLen;
    private double _combFeedback;

    // ─── Properties ──────────────────────────────────────────────

    /// <summary>Filter algorithm / output selection.</summary>
    public FilterMode Mode
    {
        get => _mode;
        set { _mode = value; UpdateCoefficients(); }   // ← fix: was missing UpdateCoefficients()
    }

    /// <summary>Cutoff frequency in Hz (20 – 20 000).</summary>
    public double Cutoff
    {
        get => _cutoff;
        set
        {
            _cutoff     = Math.Clamp(value, 20.0, 20000.0);
            _baseCutoff = _cutoff;
            UpdateCoefficients();
        }
    }

    /// <summary>Resonance 0 – 1. At 1.0 the SVF / K35 self-oscillates.</summary>
    public double Resonance
    {
        get => _resonance;
        set { _resonance = Math.Clamp(value, 0.0, 1.0); UpdateCoefficients(); }
    }

    /// <summary>Pre-filter drive / saturation 0 – 1.</summary>
    public double Drive
    {
        get => _drive;
        set => _drive = Math.Clamp(value, 0.0, 1.0);
    }

    /// <summary>Key-tracking amount 0 – 1.</summary>
    public double KeyTracking
    {
        get => _keyTracking;
        set => _keyTracking = Math.Clamp(value, 0.0, 1.0);
    }

    /// <summary>If true, SVF is cascaded for 24 dB/oct. No effect on Moog / Comb / Formant.</summary>
    public bool Is24dB
    {
        get => _is24dB;
        set => _is24dB = value;
    }

    /// <summary>Vowel position for Formant mode (0=A, 0.25=E, 0.5=I, 0.75=O, 1.0=U).</summary>
    public double FormantVowel
    {
        get => _formant.Vowel;
        set { _formant.Vowel = value; }
    }

    /// <summary>Formant frequency scale factor (0.5=small voice, 1.0=neutral, 2.0=deep voice).</summary>
    public double FormantShift
    {
        get => _formant.FormantShift;
        set { _formant.FormantShift = value; }
    }

    // ─── Constructor ─────────────────────────────────────────────

    public StateVariableFilter()
    {
        UpdateCoefficients();
    }

    // ─── Public API ──────────────────────────────────────────────

    public void SetSampleRate(double sampleRate)
    {
        _sampleRate = sampleRate;
        _moog.SetSampleRate(sampleRate);
        _formant.SetSampleRate(sampleRate);
        UpdateCoefficients();
    }

    public void SetNoteFrequency(double frequency)
    {
        _noteFrequency = frequency;
        if (_keyTracking > 0.001)
            UpdateCutoffWithKeyTracking();
    }

    /// <summary>Clears all internal filter memory. Call on NoteOn to avoid transients.</summary>
    public void ResetState()
    {
        _ic1 = _ic2 = _ic1b = _ic2b = 0;
        _k35S1L = _k35S2L = _k35FbL = 0;
        _k35S1H = _k35S2H = _k35FbH = 0;
        _combLpState = 0;
        // Do NOT clear the whole comb buffer on NoteOn — causes clicks.
        // It decays on its own via the feedback < 1.
        _moog.ResetState();
        _formant.ResetState();
    }

    /// <summary>Clears everything including the comb delay buffer.</summary>
    public void HardReset()
    {
        ResetState();
        Array.Clear(_combBuf, 0, CombBufSize);
        _combWritePos = 0;
    }

    /// <summary>
    /// Copies all parameters from <paramref name="src"/> into this instance without touching
    /// internal filter state (ic1/ic2, K35 states, comb buffer).
    /// Call once per audio block on a right-channel mirror instance to keep params in sync.
    /// </summary>
    public void SyncParamsFrom(StateVariableFilter src)
    {
        _mode          = src._mode;
        _bypass        = src._bypass;
        _is24dB        = src._is24dB;
        _drive         = src._drive;
        _keyTracking   = src._keyTracking;
        _noteFrequency = src._noteFrequency;
        _cutoff        = src._cutoff;
        _baseCutoff    = src._baseCutoff;
        _resonance     = src._resonance;
        UpdateCoefficients();
        _moog.SyncParamsFrom(src._moog);
        _formant.SyncParamsFrom(src._formant);
    }

    /// <summary>Process a mono block in-place (or input → output).</summary>
    public void Process(double[] input, double[] output, int sampleCount)
    {
        if (_bypass)
        {
            if (input != output)
                Array.Copy(input, output, sampleCount);
            return;
        }

        double driveGain = 1.0 + _drive * 4.0; // 1× – 5×

        switch (_mode)
        {
            case FilterMode.LowPass:
            case FilterMode.HighPass:
            case FilterMode.BandPass:
            case FilterMode.Notch:
            case FilterMode.AllPass:
            case FilterMode.Peak:
                ProcessSVF(input, output, sampleCount, driveGain);
                break;

            case FilterMode.MoogLadder:
                _moog.Process(input, output, sampleCount);
                break;

            case FilterMode.K35LP:
                ProcessK35LP(input, output, sampleCount, driveGain);
                break;

            case FilterMode.K35HP:
                ProcessK35HP(input, output, sampleCount, driveGain);
                break;

            case FilterMode.CombPositive:
                ProcessCombPositive(input, output, sampleCount, driveGain);
                break;

            case FilterMode.CombNegative:
                ProcessCombNegative(input, output, sampleCount, driveGain);
                break;

            case FilterMode.Formant:
                _formant.Process(input, output, sampleCount);
                break;

            default:
                if (input != output)
                    Array.Copy(input, output, sampleCount);
                break;
        }
    }

    // ─── Simper TPT SVF ──────────────────────────────────────────

    private void ProcessSVF(double[] input, double[] output, int sampleCount, double driveGain)
    {
        double ic1 = _ic1, ic2 = _ic2;
        double ic1b = _ic1b, ic2b = _ic2b;
        double a1 = _a1, a2 = _a2, a3 = _a3, k = _svfK;
        bool do24 = _is24dB;
        FilterMode mode = _mode;
        bool hasDrive = _drive > 0.001;

        for (int i = 0; i < sampleCount; i++)
        {
            double x = input[i];
            if (hasDrive)
            {
                x *= driveGain;
                x = FastTanh(x);
            }

            // Simper TPT per-sample equations (algebraically solved, no iteration)
            double v3 = x - ic2;
            double v1 = a1 * ic1 + a2 * v3;
            double v2 = ic2 + a2 * ic1 + a3 * v3;
            ic1 = 2.0 * v1 - ic1;
            ic2 = 2.0 * v2 - ic2;

            double stage1 = SvfOutput(mode, x, v1, v2, k);

            if (do24)
            {
                // 24 dB: cascade a second identical SVF stage
                double v3b = stage1 - ic2b;
                double v1b = a1 * ic1b + a2 * v3b;
                double v2b = ic2b + a2 * ic1b + a3 * v3b;
                ic1b = 2.0 * v1b - ic1b;
                ic2b = 2.0 * v2b - ic2b;
                output[i] = SvfOutput(mode, stage1, v1b, v2b, k);
            }
            else
            {
                output[i] = stage1;
            }
        }

        _ic1 = ic1; _ic2 = ic2;
        _ic1b = ic1b; _ic2b = ic2b;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double SvfOutput(FilterMode mode, double x, double v1, double v2, double k)
    {
        return mode switch
        {
            FilterMode.LowPass  => v2,
            FilterMode.HighPass => x - k * v1 - v2,
            FilterMode.BandPass => v1,
            FilterMode.Notch    => x - k * v1,
            FilterMode.AllPass  => x - 2.0 * k * v1,
            FilterMode.Peak     => x - k * v1 - 2.0 * v2,
            _                   => v2,
        };
    }

    // ─── K35  (Korg MS-20 inspired) ──────────────────────────────

    private void ProcessK35LP(double[] input, double[] output, int sampleCount, double driveGain)
    {
        double s1 = _k35S1L, s2 = _k35S2L, fb = _k35FbL;
        double g = _k35G, k = _k35K;
        bool hasDrive = _drive > 0.001;

        for (int i = 0; i < sampleCount; i++)
        {
            double x = input[i];
            if (hasDrive) { x = FastTanh(x * driveGain); }

            // Non-linear resonance feedback (1-sample delay)
            double xIn = x - k * FastTanh(fb);

            // Stage 1: one-pole LP (TPT bilinear)
            double v1  = g * (xIn - s1);
            double lp1 = v1 + s1;
            s1 = lp1 + v1;              // TPT state: s = 2y − s_prev

            // Stage 2: one-pole LP
            double v2  = g * (lp1 - s2);
            double lp2 = v2 + s2;
            s2 = lp2 + v2;

            fb = lp2;                   // Store output for next feedback
            output[i] = lp2 * (1.0 + k * 0.28); // Gain compensation
        }

        _k35S1L = s1; _k35S2L = s2; _k35FbL = fb;
    }

    private void ProcessK35HP(double[] input, double[] output, int sampleCount, double driveGain)
    {
        double s1 = _k35S1H, s2 = _k35S2H, fb = _k35FbH;
        double g = _k35G, k = _k35K;
        bool hasDrive = _drive > 0.001;

        for (int i = 0; i < sampleCount; i++)
        {
            double x = input[i];
            if (hasDrive) { x = FastTanh(x * driveGain); }

            // Feedback from previous HP output
            double xIn = x - k * FastTanh(fb);

            // Stage 1: one-pole HP (LP complement via TPT)
            double v1  = g * (xIn - s1);
            double lp1 = v1 + s1;
            s1 = lp1 + v1;
            double hp1 = xIn - lp1;

            // Stage 2: one-pole HP
            double v2  = g * (hp1 - s2);
            double lp2 = v2 + s2;
            s2 = lp2 + v2;
            double hp2 = hp1 - lp2;

            fb = hp2;
            output[i] = hp2 * (1.0 + k * 0.28);
        }

        _k35S1H = s1; _k35S2H = s2; _k35FbH = fb;
    }

    // ─── Comb ────────────────────────────────────────────────────

    private void ProcessCombPositive(double[] input, double[] output, int sampleCount, double driveGain)
    {
        int   writePos  = _combWritePos;
        int   delayLen  = _combDelayLen;
        double fbAmount = _combFeedback;
        double lpState  = _combLpState;
        bool hasDrive   = _drive > 0.001;

        for (int i = 0; i < sampleCount; i++)
        {
            double x = input[i];
            if (hasDrive) { x = FastTanh(x * driveGain); }

            int readPos = (writePos - delayLen + CombBufSize) % CombBufSize;
            double delayed = _combBuf[readPos];

            // One-pole LP in feedback (high-freq damping, more natural sound)
            lpState = delayed * 0.65 + lpState * 0.35;

            double y = x + fbAmount * lpState;
            _combBuf[writePos] = y;
            writePos = (writePos + 1) % CombBufSize;

            output[i] = y;
        }

        _combWritePos = writePos;
        _combLpState  = lpState;
    }

    private void ProcessCombNegative(double[] input, double[] output, int sampleCount, double driveGain)
    {
        int    writePos = _combWritePos;
        int    delayLen = _combDelayLen;
        double ffAmount = _combFeedback;
        bool hasDrive   = _drive > 0.001;

        for (int i = 0; i < sampleCount; i++)
        {
            double x = input[i];
            if (hasDrive) { x = FastTanh(x * driveGain); }

            int readPos = (writePos - delayLen + CombBufSize) % CombBufSize;
            double delayed = _combBuf[readPos];

            _combBuf[writePos] = x;                   // FIR: store input, not output
            writePos = (writePos + 1) % CombBufSize;

            output[i] = x - ffAmount * delayed;
        }

        _combWritePos = writePos;
    }

    // ─── Coefficient update ───────────────────────────────────────

    private void UpdateCoefficients()
    {
        // ── Bypass logic ─────────────────────────────────────────
        // LP fully open at max cutoff with no resonance → passthrough
        // Cutoff is clamped to 20000 Hz, so bypass when at that ceiling.
        if (_mode == FilterMode.LowPass && _cutoff >= 20000.0 && _resonance < 0.01)
        {
            _bypass = true;
            return;
        }
        // HP fully open at min cutoff with no resonance → passthrough
        if (_mode == FilterMode.HighPass && _cutoff <= 22.0 && _resonance < 0.01)
        {
            _bypass = true;
            return;
        }
        _bypass = false;

        double safeCutoff = Math.Min(_cutoff, _sampleRate * 0.49);

        // ── Simper TPT SVF coefficients ──────────────────────────
        // g = tan(pi * fc/fs)  — frequency warp via bilinear transform
        double g  = Math.Tan(Math.PI * safeCutoff / _sampleRate);
        // Q range: 0.5 (flat) … 25 (self-oscillation)
        double qv = 0.5 + _resonance * 24.5;
        _svfK     = 1.0 / qv;
        _a1       = 1.0 / (1.0 + g * (g + _svfK));
        _a2       = g   * _a1;
        _a3       = g   * _a2;

        // ── Moog Ladder ──────────────────────────────────────────
        _moog.Cutoff    = _cutoff;
        _moog.Resonance = _resonance;
        _moog.Drive     = _drive;

        // ── K35 coefficients ─────────────────────────────────────
        double k35Wc = Math.Tan(Math.PI * safeCutoff / _sampleRate);
        _k35G = k35Wc / (1.0 + k35Wc);  // TPT one-pole coefficient (bilinear)
        _k35K = _resonance * 1.95;       // feedback [0..1.95]; >2 = self-oscillation

        // ── Comb coefficients ─────────────────────────────────────
        _combDelayLen = Math.Clamp((int)(_sampleRate / Math.Max(safeCutoff, 20.0)),
                                   1, CombBufSize - 1);
        _combFeedback = _resonance * 0.97;   // < 1.0 always; 0.97 at max resonance
    }

    private void UpdateCutoffWithKeyTracking()
    {
        // Linear key tracking: interpolate between base cutoff and note frequency
        double tracked = _baseCutoff + (_noteFrequency - 440.0) * _keyTracking;
        _cutoff = Math.Clamp(tracked, 20.0, 20000.0);
        UpdateCoefficients();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double FastTanh(double x)
    {
        if (x >  3.0) return  1.0;
        if (x < -3.0) return -1.0;
        double x2 = x * x;
        return x * (27.0 + x2) / (27.0 + 9.0 * x2);
    }
}

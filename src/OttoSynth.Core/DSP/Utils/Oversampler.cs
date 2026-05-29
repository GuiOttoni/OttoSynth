using System;

namespace OttoSynth.Core.DSP.Utils;

/// <summary>
/// Stereo oversampler for non-linear audio effects.
/// Upsamples by <see cref="Factor"/> using linear interpolation, lets the caller apply
/// the non-linearity at the elevated rate, then downsamples via a 1-pole IIR filter.
/// Zero allocation after construction.
/// </summary>
public sealed class Oversampler
{
    public int Factor { get; }

    private readonly double[] _upL;
    private readonly double[] _upR;

    // 1-pole IIR decimation state (cutoff ≈ Nyquist/Factor)
    private double _iirL, _iirR;
    private readonly double _iirCoeff; // e^(-2π·fc/fs_up)

    /// <param name="factor">Oversampling ratio (2 or 4).</param>
    /// <param name="maxInputSize">Maximum number of input samples per call.</param>
    public Oversampler(int factor, int maxInputSize)
    {
        Factor = factor;
        _upL   = new double[maxInputSize * factor];
        _upR   = new double[maxInputSize * factor];

        // IIR coefficient: cutoff at 0.45 × original Nyquist relative to upsampled rate
        // fc_normalised = 0.45 / factor → coeff = e^(-2π·fc)
        double fc = 0.45 / factor;
        _iirCoeff = Math.Exp(-2.0 * Math.PI * fc);
    }

    /// <summary>
    /// Upsamples <paramref name="left"/> and <paramref name="right"/> in-place by <see cref="Factor"/>,
    /// invokes <paramref name="processAtHighRate"/> on the upsampled buffers, then downsamples
    /// back to the original <paramref name="count"/> samples.
    /// </summary>
    public void Process(double[] left, double[] right, int count,
        Action<double[], double[], int> processAtHighRate)
    {
        int upCount = count * Factor;

        // ── Upsample (linear interpolation) ──────────────────────
        double prevL = _upL[0]; // keep continuity across block boundaries
        double prevR = _upR[0];

        for (int i = 0; i < count; i++)
        {
            double curL = left[i];
            double curR = right[i];
            for (int s = 0; s < Factor; s++)
            {
                double t = s * (1.0 / Factor);
                _upL[i * Factor + s] = prevL + (curL - prevL) * t;
                _upR[i * Factor + s] = prevR + (curR - prevR) * t;
            }
            prevL = curL;
            prevR = curR;
        }

        // ── Non-linear process at high rate ───────────────────────
        processAtHighRate(_upL, _upR, upCount);

        // ── Downsample (1-pole IIR anti-imaging filter + decimate) ──
        double c  = _iirCoeff;
        double c1 = 1.0 - c;
        double iL = _iirL;
        double iR = _iirR;

        for (int i = 0; i < count; i++)
        {
            // Filter all Factor samples, take only the last
            for (int s = 0; s < Factor; s++)
            {
                iL = c * iL + c1 * _upL[i * Factor + s];
                iR = c * iR + c1 * _upR[i * Factor + s];
            }
            left[i]  = iL;
            right[i] = iR;
        }

        _iirL = iL;
        _iirR = iR;
    }

    public void Reset()
    {
        _iirL = _iirR = 0.0;
        Array.Clear(_upL, 0, _upL.Length);
        Array.Clear(_upR, 0, _upR.Length);
    }
}

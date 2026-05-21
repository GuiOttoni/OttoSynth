namespace OttoSynth.Core.DSP.Utils;

/// <summary>
/// Single-pole DC offset removal filter.
/// Difference equation: y[n] = x[n] - x[n-1] + R * y[n-1]
/// Where R is close to 1 (e.g. 0.995 at 44100Hz, giving ~5Hz corner frequency).
/// </summary>
public sealed class DcBlocker
{
    private double _r;
    private double _xPrevL, _yPrevL;
    private double _xPrevR, _yPrevR;

    public DcBlocker(double sampleRate = 44100.0)
    {
        // Set corner frequency to ~5 Hz
        SetSampleRate(sampleRate);
    }

    public void SetSampleRate(double sampleRate)
    {
        // R = 1 - 2*pi*fc/fs
        _r = 1.0 - 2.0 * System.Math.PI * 5.0 / sampleRate;
        if (_r < 0.95) _r = 0.95;
        if (_r > 0.9999) _r = 0.9999;
    }

    public void Reset()
    {
        _xPrevL = _yPrevL = 0;
        _xPrevR = _yPrevR = 0;
    }

    public void ProcessStereo(double[] left, double[] right, int sampleCount)
    {
        for (int i = 0; i < sampleCount; i++)
        {
            double xL = left[i];
            double yL = xL - _xPrevL + _r * _yPrevL;
            _xPrevL = xL;
            _yPrevL = yL;
            left[i] = yL;

            double xR = right[i];
            double yR = xR - _xPrevR + _r * _yPrevR;
            _xPrevR = xR;
            _yPrevR = yR;
            right[i] = yR;
        }
    }
}

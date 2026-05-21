using System;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OttoSynth.UI.Controls;

/// <summary>
/// Real-time frequency spectrum analyzer.
/// Uses a built-in FFT implementation (no external dependency).
/// </summary>
public class SpectrumAnalyzer : Control
{
    public static readonly DependencyProperty SamplesProperty = DependencyProperty.Register(
        nameof(Samples), typeof(double[]), typeof(SpectrumAnalyzer),
        new PropertyMetadata(null, (d, _) => ((SpectrumAnalyzer)d).InvalidateVisual()));

    public static readonly DependencyProperty SampleRateProperty = DependencyProperty.Register(
        nameof(SampleRate), typeof(double), typeof(SpectrumAnalyzer),
        new PropertyMetadata(44100.0));

    public static readonly DependencyProperty BarBrushProperty = DependencyProperty.Register(
        nameof(BarBrush), typeof(Brush), typeof(SpectrumAnalyzer),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x41))));

    public double[]? Samples { get => (double[]?)GetValue(SamplesProperty); set => SetValue(SamplesProperty, value); }
    public double SampleRate { get => (double)GetValue(SampleRateProperty); set => SetValue(SampleRateProperty, value); }
    public Brush BarBrush { get => (Brush)GetValue(BarBrushProperty); set => SetValue(BarBrushProperty, value); }

    private const int FftSize = 512;
    private readonly double[] _fftBuffer = new double[FftSize];
    private readonly Complex[] _fftWorkspace = new Complex[FftSize];
    private readonly double[] _magnitudes = new double[FftSize / 2];
    private readonly double[] _smoothedMagnitudes = new double[FftSize / 2];

    public SpectrumAnalyzer()
    {
        Background = new SolidColorBrush(Color.FromRgb(0x01, 0x04, 0x02));
        ClipToBounds = true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        dc.DrawRectangle(Background, null, new Rect(0, 0, w, h));

        var samples = Samples;
        if (samples == null || samples.Length < FftSize) return;

        // Copy and Hann-window
        for (int i = 0; i < FftSize; i++)
        {
            double window = 0.5 * (1.0 - Math.Cos(2 * Math.PI * i / (FftSize - 1)));
            _fftBuffer[i] = samples[i] * window;
            _fftWorkspace[i] = new Complex(_fftBuffer[i], 0);
        }

        // In-place FFT
        Fft(_fftWorkspace);

        // Convert to magnitudes (in dB)
        for (int i = 0; i < FftSize / 2; i++)
        {
            double mag = _fftWorkspace[i].Magnitude / FftSize;
            double db = mag > 1e-9 ? 20.0 * Math.Log10(mag) : -90.0;
            db = Math.Max(-90.0, db);
            // Normalize: -90dB → 0, 0dB → 1
            double normalized = (db + 90.0) / 90.0;
            _magnitudes[i] = normalized;
            // Smooth
            _smoothedMagnitudes[i] = _smoothedMagnitudes[i] * 0.75 + _magnitudes[i] * 0.25;
        }

        // Render: log-scale frequency on X axis (20Hz - 20kHz)
        int numBars = 64;
        double barWidth = w / numBars;
        for (int b = 0; b < numBars; b++)
        {
            // Map bar position to frequency (logarithmic)
            double freqMin = 20.0;
            double freqMax = 20000.0;
            double freqAtBar = freqMin * Math.Pow(freqMax / freqMin, b / (double)(numBars - 1));
            int binIndex = (int)(freqAtBar / SampleRate * FftSize);
            binIndex = Math.Clamp(binIndex, 0, FftSize / 2 - 1);

            double mag = _smoothedMagnitudes[binIndex];
            double barHeight = mag * h;
            if (barHeight < 1) continue;

            double x = b * barWidth;
            dc.DrawRectangle(BarBrush, null,
                new Rect(x + 1, h - barHeight, barWidth - 2, barHeight));
        }
    }

    /// <summary>
    /// In-place iterative Cooley-Tukey radix-2 FFT.
    /// </summary>
    private static void Fft(Complex[] data)
    {
        int n = data.Length;
        if (n <= 1) return;

        // Bit-reversal permutation
        int bits = (int)Math.Log2(n);
        for (int i = 0; i < n; i++)
        {
            int rev = ReverseBits(i, bits);
            if (rev > i)
            {
                (data[i], data[rev]) = (data[rev], data[i]);
            }
        }

        for (int size = 2; size <= n; size *= 2)
        {
            int half = size / 2;
            double angle = -2.0 * Math.PI / size;
            Complex wStep = new(Math.Cos(angle), Math.Sin(angle));
            for (int i = 0; i < n; i += size)
            {
                Complex w = Complex.One;
                for (int j = 0; j < half; j++)
                {
                    Complex u = data[i + j];
                    Complex t = w * data[i + j + half];
                    data[i + j] = u + t;
                    data[i + j + half] = u - t;
                    w *= wStep;
                }
            }
        }
    }

    private static int ReverseBits(int x, int bits)
    {
        int result = 0;
        for (int i = 0; i < bits; i++)
        {
            result = (result << 1) | (x & 1);
            x >>= 1;
        }
        return result;
    }
}

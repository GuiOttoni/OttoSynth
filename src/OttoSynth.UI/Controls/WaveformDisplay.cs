using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OttoSynth.UI.Controls;

/// <summary>
/// Real-time waveform display. Set Samples to refresh the display.
/// Designed to be called from a DispatcherTimer at 30-60fps.
/// </summary>
public class WaveformDisplay : Control
{
    public static readonly DependencyProperty SamplesProperty = DependencyProperty.Register(
        nameof(Samples), typeof(double[]), typeof(WaveformDisplay),
        new PropertyMetadata(null, (d, _) => ((WaveformDisplay)d).InvalidateVisual()));

    public static readonly DependencyProperty WaveformBrushProperty = DependencyProperty.Register(
        nameof(WaveformBrush), typeof(Brush), typeof(WaveformDisplay),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x41))));

    public static readonly DependencyProperty GridBrushProperty = DependencyProperty.Register(
        nameof(GridBrush), typeof(Brush), typeof(WaveformDisplay),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x0D, 0x29, 0x14))));

    public double[]? Samples { get => (double[]?)GetValue(SamplesProperty); set => SetValue(SamplesProperty, value); }
    public Brush WaveformBrush { get => (Brush)GetValue(WaveformBrushProperty); set => SetValue(WaveformBrushProperty, value); }
    public Brush GridBrush { get => (Brush)GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }

    public WaveformDisplay()
    {
        Background = new SolidColorBrush(Color.FromRgb(0x01, 0x04, 0x02));
        ClipToBounds = true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Background
        dc.DrawRectangle(Background, null, new Rect(0, 0, w, h));

        // Grid lines (horizontal center, top/bottom thirds)
        var gridPen = new Pen(GridBrush, 0.5);
        dc.DrawLine(gridPen, new Point(0, h / 2.0), new Point(w, h / 2.0));
        dc.DrawLine(gridPen, new Point(0, h / 4.0), new Point(w, h / 4.0));
        dc.DrawLine(gridPen, new Point(0, h * 0.75), new Point(w, h * 0.75));

        var samples = Samples;
        if (samples == null || samples.Length < 2) return;

        double halfH = h / 2.0;
        double xStep = w / (samples.Length - 1);

        var pen = new Pen(WaveformBrush, 1.5)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        var sg = new StreamGeometry();
        using (var ctx = sg.Open())
        {
            ctx.BeginFigure(new Point(0, halfH - samples[0] * halfH * 0.95), false, false);
            for (int i = 1; i < samples.Length; i++)
            {
                double x = i * xStep;
                double y = halfH - samples[i] * halfH * 0.95;
                if (y < 0) y = 0;
                if (y > h) y = h;
                ctx.LineTo(new Point(x, y), true, false);
            }
        }
        sg.Freeze();
        dc.DrawGeometry(null, pen, sg);
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OttoSynth.UI.Controls;

/// <summary>
/// LFO shape visualization. Renders the selected waveform across two cycles.
/// </summary>
public class LfoDisplay : Control
{
    public enum LfoShape
    {
        Sine,
        Triangle,
        SawUp,
        SawDown,
        Square,
        SampleHold,
    }

    public static readonly DependencyProperty ShapeProperty = DependencyProperty.Register(
        nameof(Shape), typeof(LfoShape), typeof(LfoDisplay),
        new PropertyMetadata(LfoShape.Sine, (d, _) => ((LfoDisplay)d).InvalidateVisual()));

    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
        nameof(LineBrush), typeof(Brush), typeof(LfoDisplay),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x00))));

    public LfoShape Shape { get => (LfoShape)GetValue(ShapeProperty); set => SetValue(ShapeProperty, value); }
    public Brush LineBrush { get => (Brush)GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    public LfoDisplay()
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

        // Center line
        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(0x0D, 0x29, 0x14)), 0.5);
        dc.DrawLine(gridPen, new Point(0, h / 2.0), new Point(w, h / 2.0));

        var pen = new Pen(LineBrush, 2.0)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        int samples = 200;
        double xStep = w / samples;
        double halfH = h / 2.0;
        double amp = halfH * 0.85;

        var sg = new StreamGeometry();
        using (var ctx = sg.Open())
        {
            ctx.BeginFigure(new Point(0, halfH - Sample(0.0) * amp), false, false);
            for (int i = 1; i <= samples; i++)
            {
                double t = i / (double)samples * 2.0; // two cycles
                double y = halfH - Sample(t) * amp;
                ctx.LineTo(new Point(i * xStep, y), true, false);
            }
        }
        sg.Freeze();
        dc.DrawGeometry(null, pen, sg);
    }

    private static readonly Random _rng = new(42);
    private double Sample(double t)
    {
        double phase = t - Math.Floor(t);
        return Shape switch
        {
            LfoShape.Sine => Math.Sin(2 * Math.PI * phase),
            LfoShape.Triangle => phase < 0.5 ? 4 * phase - 1 : 3 - 4 * phase,
            LfoShape.SawUp => 2 * phase - 1,
            LfoShape.SawDown => 1 - 2 * phase,
            LfoShape.Square => phase < 0.5 ? 1 : -1,
            LfoShape.SampleHold => SampleHoldValue(t),
            _ => 0
        };
    }

    private static readonly double[] _shValues =
    {
        0.5, -0.3, 0.7, -0.6, 0.2, -0.9, 0.4, -0.1
    };

    private static double SampleHoldValue(double t)
    {
        int idx = (int)(t * 8) % _shValues.Length;
        if (idx < 0) idx += _shValues.Length;
        return _shValues[idx];
    }
}

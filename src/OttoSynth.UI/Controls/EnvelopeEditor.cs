using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OttoSynth.UI.Controls;

/// <summary>
/// Visual ADSR envelope display.
/// Renders the ADSR curve with the times in seconds.
/// </summary>
public class EnvelopeEditor : Control
{
    public static readonly DependencyProperty AttackProperty = DependencyProperty.Register(
        nameof(Attack), typeof(double), typeof(EnvelopeEditor),
        new PropertyMetadata(0.01, OnChanged));

    public static readonly DependencyProperty DecayProperty = DependencyProperty.Register(
        nameof(Decay), typeof(double), typeof(EnvelopeEditor),
        new PropertyMetadata(0.3, OnChanged));

    public static readonly DependencyProperty SustainProperty = DependencyProperty.Register(
        nameof(Sustain), typeof(double), typeof(EnvelopeEditor),
        new PropertyMetadata(0.7, OnChanged));

    public static readonly DependencyProperty ReleaseProperty = DependencyProperty.Register(
        nameof(Release), typeof(double), typeof(EnvelopeEditor),
        new PropertyMetadata(0.5, OnChanged));

    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
        nameof(LineBrush), typeof(Brush), typeof(EnvelopeEditor),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x41))));

    public double Attack { get => (double)GetValue(AttackProperty); set => SetValue(AttackProperty, value); }
    public double Decay { get => (double)GetValue(DecayProperty); set => SetValue(DecayProperty, value); }
    public double Sustain { get => (double)GetValue(SustainProperty); set => SetValue(SustainProperty, value); }
    public double Release { get => (double)GetValue(ReleaseProperty); set => SetValue(ReleaseProperty, value); }
    public Brush LineBrush { get => (Brush)GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    public EnvelopeEditor()
    {
        Background = new SolidColorBrush(Color.FromRgb(0x01, 0x04, 0x02));
        ClipToBounds = true;
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((EnvelopeEditor)d).InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        dc.DrawRectangle(Background, null, new Rect(0, 0, w, h));

        // Grid
        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(0x0D, 0x29, 0x14)), 0.5);
        for (int i = 1; i < 4; i++)
        {
            double y = h * i / 4.0;
            dc.DrawLine(gridPen, new Point(0, y), new Point(w, y));
        }

        // Compute envelope points. Total visible time = Attack + Decay + 0.3s sustain + Release
        double sustainDuration = 0.3;
        double totalTime = Attack + Decay + sustainDuration + Release;
        if (totalTime < 0.001) totalTime = 0.001;

        double pad = 4.0;
        double drawW = w - pad * 2.0;
        double drawH = h - pad * 2.0;
        double scale = drawW / totalTime;

        // Points
        double x0 = pad;
        double y0 = pad + drawH; // bottom
        double xA = x0 + Attack * scale;
        double yA = pad; // peak
        double xD = xA + Decay * scale;
        double yD = pad + drawH * (1.0 - Math.Clamp(Sustain, 0.0, 1.0));
        double xS = xD + sustainDuration * scale;
        double yS = yD;
        double xR = xS + Release * scale;
        double yR = pad + drawH;

        // Draw envelope line
        var pen = new Pen(LineBrush, 2.0)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        var sg = new StreamGeometry();
        using (var ctx = sg.Open())
        {
            ctx.BeginFigure(new Point(x0, y0), false, false);
            ctx.LineTo(new Point(xA, yA), true, false);
            ctx.LineTo(new Point(xD, yD), true, false);
            ctx.LineTo(new Point(xS, yS), true, false);
            ctx.LineTo(new Point(xR, yR), true, false);
        }
        sg.Freeze();
        dc.DrawGeometry(null, pen, sg);

        // Point dots
        var dotBrush = LineBrush;
        dc.DrawEllipse(dotBrush, null, new Point(xA, yA), 3, 3);
        dc.DrawEllipse(dotBrush, null, new Point(xD, yD), 3, 3);
        dc.DrawEllipse(dotBrush, null, new Point(xS, yS), 3, 3);
        dc.DrawEllipse(dotBrush, null, new Point(xR, yR), 3, 3);
    }
}

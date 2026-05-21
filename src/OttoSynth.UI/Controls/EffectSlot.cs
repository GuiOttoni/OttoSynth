using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OttoSynth.UI.Controls;

/// <summary>
/// A single slot in an effects rack. Shows the effect's name, bypass toggle, and mix.
/// </summary>
public class EffectSlot : Control
{
    public static readonly DependencyProperty EffectNameProperty = DependencyProperty.Register(
        nameof(EffectName), typeof(string), typeof(EffectSlot),
        new PropertyMetadata("Effect", (d, _) => ((EffectSlot)d).InvalidateVisual()));

    public static readonly DependencyProperty IsBypassedProperty = DependencyProperty.Register(
        nameof(IsBypassed), typeof(bool), typeof(EffectSlot),
        new FrameworkPropertyMetadata(false,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((EffectSlot)d).InvalidateVisual()));

    public static readonly DependencyProperty MixProperty = DependencyProperty.Register(
        nameof(Mix), typeof(double), typeof(EffectSlot),
        new FrameworkPropertyMetadata(1.0,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((EffectSlot)d).InvalidateVisual()));

    public string EffectName { get => (string)GetValue(EffectNameProperty); set => SetValue(EffectNameProperty, value); }
    public bool IsBypassed { get => (bool)GetValue(IsBypassedProperty); set => SetValue(IsBypassedProperty, value); }
    public double Mix { get => (double)GetValue(MixProperty); set => SetValue(MixProperty, value); }

    public EffectSlot()
    {
        Width = 120;
        Height = 60;
        Background = new SolidColorBrush(Color.FromRgb(0x07, 0x1A, 0x0E));
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Background (Matrix-style)
        Brush bg = IsBypassed
            ? new SolidColorBrush(Color.FromRgb(0x03, 0x06, 0x05))
            : new SolidColorBrush(Color.FromRgb(0x07, 0x1A, 0x0E));
        Brush border = IsBypassed
            ? new SolidColorBrush(Color.FromRgb(0x1A, 0x4D, 0x23))
            : new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x41));

        // Sharp corners for terminal feel
        dc.DrawRectangle(bg, new System.Windows.Media.Pen(border, 1.5), new Rect(2, 2, w - 4, h - 4));

        // Name (monospace + green)
        var nameBrush = IsBypassed
            ? new SolidColorBrush(Color.FromRgb(0x2D, 0x6B, 0x36))
            : new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x41));
        var ft = new FormattedText(EffectName.ToUpperInvariant(),
            System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Cascadia Mono"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            11, nameBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        ft.TextAlignment = TextAlignment.Center;
        ft.MaxTextWidth = w;
        dc.DrawText(ft, new Point(0, 8));

        // Mix bar
        double barY = h - 16;
        var trackBrush = new SolidColorBrush(Color.FromRgb(0x0A, 0x29, 0x14));
        dc.DrawRectangle(trackBrush, null, new Rect(8, barY, w - 16, 4));
        double fillW = (w - 16) * System.Math.Clamp(Mix, 0.0, 1.0);
        var fillBrush = IsBypassed
            ? new SolidColorBrush(Color.FromRgb(0x2D, 0x6B, 0x36))
            : new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x41));
        dc.DrawRectangle(fillBrush, null, new Rect(8, barY, fillW, 4));
    }
}

using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OttoSynth.UI.Controls;

/// <summary>
/// Simple visual display of the modulation matrix.
/// Renders source → destination connections as a grid of cells whose color/size
/// represents the modulation amount.
/// </summary>
public class ModMatrixGrid : Control
{
    public class Route
    {
        public string Source { get; set; } = "";
        public string Destination { get; set; } = "";
        public double Amount { get; set; }
    }

    public static readonly DependencyProperty RoutesProperty = DependencyProperty.Register(
        nameof(Routes), typeof(IList<Route>), typeof(ModMatrixGrid),
        new PropertyMetadata(null, (d, _) => ((ModMatrixGrid)d).InvalidateVisual()));

    public IList<Route>? Routes { get => (IList<Route>?)GetValue(RoutesProperty); set => SetValue(RoutesProperty, value); }

    public ModMatrixGrid()
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

        var routes = Routes;
        if (routes == null || routes.Count == 0)
        {
            var ft = new FormattedText("[ NO ROUTES ]",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, new Typeface("Cascadia Mono"), 11,
                new SolidColorBrush(Color.FromRgb(0x2D, 0x6B, 0x36)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point((w - ft.Width) / 2, (h - ft.Height) / 2));
            return;
        }

        double rowH = 22;
        var typeface = new Typeface("Cascadia Mono");
        var labelBrush = new SolidColorBrush(Color.FromRgb(0xB0, 0xFF, 0xB0));
        var positiveBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x41));
        var negativeBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x3C));
        var trackBrush = new SolidColorBrush(Color.FromRgb(0x0A, 0x29, 0x14));

        double y = 4;
        for (int i = 0; i < routes.Count; i++)
        {
            var r = routes[i];
            string srcDst = $"{r.Source} → {r.Destination}";
            var ft = new FormattedText(srcDst,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, 11, labelBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(8, y));

            // Bar showing amount, centered around mid-width
            double barX = w * 0.55;
            double barW = w - barX - 12;
            double midX = barX + barW / 2.0;
            dc.DrawRectangle(trackBrush, null, new Rect(barX, y + 4, barW, 8));
            double amt = System.Math.Clamp(r.Amount, -1.0, 1.0);
            double fillW = barW * 0.5 * System.Math.Abs(amt);
            Brush fill = amt >= 0 ? positiveBrush : negativeBrush;
            if (amt >= 0)
                dc.DrawRectangle(fill, null, new Rect(midX, y + 4, fillW, 8));
            else
                dc.DrawRectangle(fill, null, new Rect(midX - fillW, y + 4, fillW, 8));

            y += rowH;
            if (y >= h - rowH) break;
        }
    }
}

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace OttoSynth.UI.Controls;

/// <summary>
/// Rotary knob control.
/// - Drag ↕ to change value   (Shift+drag = fine mode)
/// - Double-click              = enter value manually
/// - Ctrl+double-click         = reset to default
/// - Mouse wheel               = increment / decrement
/// </summary>
public class SynthKnob : Control
{
    static SynthKnob()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SynthKnob),
            new FrameworkPropertyMetadata(typeof(SynthKnob)));
    }

    // ─── Dependency Properties ──────────────────────────────────

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(SynthKnob),
        new PropertyMetadata(0.0, OnValueChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(SynthKnob),
        new PropertyMetadata(1.0, OnValueChanged));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(SynthKnob),
        new FrameworkPropertyMetadata(0.5,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.Journal,
            OnValueChanged, CoerceValue));

    public static readonly DependencyProperty DefaultValueProperty = DependencyProperty.Register(
        nameof(DefaultValue), typeof(double), typeof(SynthKnob),
        new PropertyMetadata(0.5));

    public static readonly DependencyProperty IsBipolarProperty = DependencyProperty.Register(
        nameof(IsBipolar), typeof(bool), typeof(SynthKnob),
        new PropertyMetadata(false, OnValueChanged));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(SynthKnob),
        new PropertyMetadata("", OnValueChanged));

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(SynthKnob),
        new PropertyMetadata(""));

    public static readonly DependencyProperty ValueFormatProperty = DependencyProperty.Register(
        nameof(ValueFormat), typeof(string), typeof(SynthKnob),
        new PropertyMetadata("F2"));

    public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
        nameof(AccentBrush), typeof(Brush), typeof(SynthKnob),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x41))));

    public double Minimum      { get => (double)GetValue(MinimumProperty);      set => SetValue(MinimumProperty, value); }
    public double Maximum      { get => (double)GetValue(MaximumProperty);      set => SetValue(MaximumProperty, value); }
    public double Value        { get => (double)GetValue(ValueProperty);        set => SetValue(ValueProperty, value); }
    public double DefaultValue { get => (double)GetValue(DefaultValueProperty); set => SetValue(DefaultValueProperty, value); }
    public bool   IsBipolar    { get => (bool)GetValue(IsBipolarProperty);      set => SetValue(IsBipolarProperty, value); }
    public string Label        { get => (string)GetValue(LabelProperty);        set => SetValue(LabelProperty, value); }
    public string Unit         { get => (string)GetValue(UnitProperty);         set => SetValue(UnitProperty, value); }
    public string ValueFormat  { get => (string)GetValue(ValueFormatProperty);  set => SetValue(ValueFormatProperty, value); }
    public Brush  AccentBrush  { get => (Brush)GetValue(AccentBrushProperty);   set => SetValue(AccentBrushProperty, value); }

    public event RoutedPropertyChangedEventHandler<double>? ValueChanged;

    // ─── Interaction state ──────────────────────────────────────
    private Point?  _dragStart;
    private double  _dragStartValue;
    private Popup?  _inputPopup;
    private TextBox? _inputBox;
    private bool    _discardInput;

    public SynthKnob()
    {
        MinWidth  = 52;
        MinHeight = 60;
        Background = Brushes.Transparent;
        Focusable  = true;
        ToolTip    = "Drag ↕ to change  |  Shift+drag: fine\nDouble-click: enter value  |  Ctrl+dbl: reset";
    }

    private static object CoerceValue(DependencyObject d, object baseValue)
    {
        var knob = (SynthKnob)d;
        double v = (double)baseValue;
        return Math.Max(knob.Minimum, Math.Min(knob.Maximum, v));
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var knob = (SynthKnob)d;
        knob.InvalidateVisual();
        if (e.Property == ValueProperty)
            knob.ValueChanged?.Invoke(knob,
                new RoutedPropertyChangedEventArgs<double>((double)e.OldValue, (double)e.NewValue));
    }

    // ─── Mouse interaction ──────────────────────────────────────

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton != MouseButton.Left) return;

        if (e.ClickCount == 2)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
                Value = DefaultValue;
            else
                ShowInputPopup();
            e.Handled = true;
            return;
        }

        CaptureMouse();
        _dragStart      = e.GetPosition(this);
        _dragStartValue = Value;
        Focus();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragStart is { } start && IsMouseCaptured)
        {
            var    current     = e.GetPosition(this);
            double dy          = start.Y - current.Y;
            double sensitivity = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 0.001 : 0.005;
            double range       = Maximum - Minimum;
            Value = _dragStartValue + dy * sensitivity * range;
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
            _dragStart = null;
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        double step = (Maximum - Minimum) * ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 0.001 : 0.01);
        Value += e.Delta > 0 ? step : -step;
        e.Handled = true;
    }

    // ─── Manual value input popup ───────────────────────────────

    private void ShowInputPopup()
    {
        _discardInput = false;

        _inputBox = new TextBox
        {
            Width  = 90,
            Height = 28,
            Background = new SolidColorBrush(Color.FromRgb(0x07, 0x1A, 0x0E)),
            Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x41)),
            CaretBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x41)),
            SelectionBrush = new SolidColorBrush(Color.FromArgb(0x55, 0x00, 0xFF, 0x41)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x41)),
            BorderThickness = new Thickness(0),
            FontFamily = new FontFamily("Cascadia Mono"),
            FontSize   = 11,
            Text = Value.ToString("G6", CultureInfo.InvariantCulture),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment   = VerticalAlignment.Center,
            Padding = new Thickness(4, 2, 4, 2),
        };

        _inputBox.KeyDown += InputBox_KeyDown;

        var border = new Border
        {
            Background      = new SolidColorBrush(Color.FromRgb(0x07, 0x1A, 0x0E)),
            BorderBrush     = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x41)),
            BorderThickness = new Thickness(1),
            Padding         = new Thickness(2),
            Child           = _inputBox,
        };
        border.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color       = Color.FromRgb(0x00, 0xFF, 0x41),
            BlurRadius  = 8,
            ShadowDepth = 0,
            Opacity     = 0.6,
        };

        _inputPopup = new Popup
        {
            Child            = border,
            PlacementTarget  = this,
            Placement        = PlacementMode.Center,
            AllowsTransparency = true,
            StaysOpen        = false,
        };
        _inputPopup.Closed += InputPopup_Closed;
        _inputPopup.IsOpen  = true;

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
            (Action)(() => { _inputBox?.Focus(); _inputBox?.SelectAll(); }));
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            _inputPopup!.IsOpen = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _discardInput = true;
            _inputPopup!.IsOpen = false;
            e.Handled = true;
        }
    }

    private void InputPopup_Closed(object? sender, EventArgs e)
    {
        if (!_discardInput && _inputBox is { } box)
        {
            if (double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                Value = v;
        }
        _discardInput = false;
        _inputPopup   = null;
        _inputBox     = null;
    }

    // ─── Visual Rendering ───────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        bool hasLabel    = !string.IsNullOrEmpty(Label);
        double labelH    = hasLabel ? 13 : 0;
        double valueH    = 13;
        double knobAreaH = h - labelH - valueH - 4;
        if (knobAreaH < 22) knobAreaH = 22;

        double cx     = w / 2.0;
        double cy     = labelH + knobAreaH / 2.0;
        double radius = Math.Min(w, knobAreaH) / 2.0 - 3;
        if (radius < 10) radius = 10;

        // Label
        if (hasLabel)
        {
            var ft = MakeText(Label, 9, Color.FromRgb(0x4F, 0xA6, 0x59), w);
            dc.DrawText(ft, new Point(0, 0));
        }

        // Track ring
        var trackPen = new Pen(new SolidColorBrush(Color.FromRgb(0x0A, 0x29, 0x14)), 3.5);
        dc.DrawEllipse(null, trackPen, new Point(cx, cy), radius, radius);

        // Active arc
        double norm       = Math.Clamp((Value - Minimum) / Math.Max(1e-9, Maximum - Minimum), 0.0, 1.0);
        double startAngle = 135.0;
        double valueAngle;

        if (IsBipolar)
        {
            double centerAngle = startAngle + 135.0;
            double offset      = (norm - 0.5) * 270.0;
            valueAngle = centerAngle + offset;
            DrawArc(dc, cx, cy, radius, centerAngle, valueAngle, AccentBrush, 3.5);
        }
        else
        {
            valueAngle = startAngle + norm * 270.0;
            DrawArc(dc, cx, cy, radius, startAngle, valueAngle, AccentBrush, 3.5);
        }

        // Indicator line
        double iRad = valueAngle * Math.PI / 180.0;
        double ix   = cx + Math.Cos(iRad) * radius * 0.65;
        double iy   = cy + Math.Sin(iRad) * radius * 0.65;
        double ox   = cx + Math.Cos(iRad) * radius * 1.0;
        double oy   = cy + Math.Sin(iRad) * radius * 1.0;
        var indPen  = new Pen(AccentBrush, 2.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawLine(indPen, new Point(ix, iy), new Point(ox, oy));

        // Center disc
        var centerBrush = new LinearGradientBrush(
            Color.FromRgb(0x07, 0x1A, 0x0E),
            Color.FromRgb(0x01, 0x04, 0x02),
            new Point(0, 0), new Point(0, 1));
        dc.DrawEllipse(centerBrush,
            new Pen(new SolidColorBrush(Color.FromRgb(0x1A, 0x4D, 0x23)), 1),
            new Point(cx, cy), radius * 0.58, radius * 0.58);

        // Value text
        var valFt = MakeText(FormatValue(Value), 9, Color.FromRgb(0xB0, 0xFF, 0xB0), w,
            FontWeights.Medium);
        dc.DrawText(valFt, new Point(0, h - valueH - 1));
    }

    private static FormattedText MakeText(string text, double size, Color color, double maxWidth,
        FontWeight? weight = null)
    {
        var tf = new Typeface(
            new FontFamily("Cascadia Mono"),
            FontStyles.Normal,
            weight ?? FontWeights.Normal,
            FontStretches.Normal);
        var ft = new FormattedText(text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            tf, size,
            new SolidColorBrush(color),
            96.0)
        {
            TextAlignment = TextAlignment.Center,
            MaxTextWidth  = Math.Max(1, maxWidth),
            Trimming      = TextTrimming.CharacterEllipsis,
        };
        return ft;
    }

    private void DrawArc(DrawingContext dc, double cx, double cy, double r,
        double startDeg, double endDeg, Brush brush, double thickness)
    {
        if (Math.Abs(endDeg - startDeg) < 0.5) return;

        double sr = startDeg * Math.PI / 180.0;
        double er = endDeg   * Math.PI / 180.0;
        var start = new Point(cx + Math.Cos(sr) * r, cy + Math.Sin(sr) * r);
        var end   = new Point(cx + Math.Cos(er) * r, cy + Math.Sin(er) * r);

        bool isLarge  = Math.Abs(endDeg - startDeg) > 180.0;
        var direction = endDeg > startDeg ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;

        var fig = new PathFigure { StartPoint = start };
        fig.Segments.Add(new ArcSegment(end, new Size(r, r), 0, isLarge, direction, true));

        var geom = new PathGeometry();
        geom.Figures.Add(fig);

        dc.DrawGeometry(null,
            new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round },
            geom);
    }

    private string FormatValue(double v)
    {
        string fmt = ValueFormat ?? "F2";
        string s;
        try { s = v.ToString(fmt, CultureInfo.InvariantCulture); }
        catch { s = v.ToString("F2"); }
        return string.IsNullOrEmpty(Unit) ? s : $"{s}{Unit}";
    }
}

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace OttoSynth.UI.Controls;

/// <summary>
/// Horizontal slider with double-click manual value entry.
/// - Double-click        = enter value manually
/// - Ctrl+double-click   = reset to DefaultValue
/// </summary>
public class SynthSlider : Slider
{
    static SynthSlider()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SynthSlider),
            new FrameworkPropertyMetadata(typeof(SynthSlider)));
    }

    public static readonly DependencyProperty DefaultValueProperty = DependencyProperty.Register(
        nameof(DefaultValue), typeof(double), typeof(SynthSlider),
        new PropertyMetadata(0.0));

    public double DefaultValue
    {
        get => (double)GetValue(DefaultValueProperty);
        set => SetValue(DefaultValueProperty, value);
    }

    private Popup?  _inputPopup;
    private TextBox? _inputBox;
    private bool    _discardInput;

    public SynthSlider()
    {
        ToolTip = "Drag to change  |  Double-click: enter value  |  Ctrl+dbl: reset";
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton != MouseButton.Left || e.ClickCount != 2) return;

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            Value = Math.Max(Minimum, Math.Min(Maximum, DefaultValue));
        else
            ShowInputPopup();
        e.Handled = true;
    }

    private void ShowInputPopup()
    {
        _discardInput = false;

        var accentBrush = (TryFindResource("AccentPrimaryBrush") as Brush)
                          ?? new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x41));
        var bgBrush     = (TryFindResource("BackgroundSecondaryBrush") as Brush)
                          ?? new SolidColorBrush(Color.FromRgb(0x07, 0x1A, 0x0E));

        _inputBox = new TextBox
        {
            Width  = 90,
            Height = 28,
            Background = bgBrush,
            Foreground = accentBrush,
            CaretBrush = accentBrush,
            SelectionBrush = accentBrush,
            BorderThickness = new Thickness(0),
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize   = 11,
            Text = Value.ToString("G6", CultureInfo.InvariantCulture),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment   = VerticalAlignment.Center,
            Padding = new Thickness(4, 2, 4, 2),
        };
        _inputBox.KeyDown += InputBox_KeyDown;

        var accentColor = accentBrush is SolidColorBrush scb
            ? scb.Color
            : Color.FromRgb(0x00, 0xFF, 0x41);

        var popupBorder = new Border
        {
            Background      = bgBrush,
            BorderBrush     = accentBrush,
            BorderThickness = new Thickness(1),
            Padding         = new Thickness(2),
            Child           = _inputBox,
        };
        popupBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color       = accentColor,
            BlurRadius  = 8,
            ShadowDepth = 0,
            Opacity     = 0.6,
        };

        _inputPopup = new Popup
        {
            Child            = popupBorder,
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
                Value = Math.Max(Minimum, Math.Min(Maximum, v));
        }
        _discardInput = false;
        _inputPopup   = null;
        _inputBox     = null;
    }
}

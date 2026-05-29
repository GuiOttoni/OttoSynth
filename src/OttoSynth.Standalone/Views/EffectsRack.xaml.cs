using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OttoSynth.Core;
using OttoSynth.Core.DSP.Effects;
using OttoSynth.UI.Controls;

namespace OttoSynth.Standalone.Views;

public partial class EffectsRack : UserControl
{
    private SynthEngine? _engine;
    private int _selectedIndex = -1;

    private static readonly ChannelRoute[] ChannelCycle =
        [ChannelRoute.Both, ChannelRoute.Left, ChannelRoute.Right];

    private static string ChannelLabel(ChannelRoute r) => r switch
    {
        ChannelRoute.Left  => "L",
        ChannelRoute.Right => "R",
        _                  => "ST"
    };

    // Resolves brush from current theme at call time — picks up theme changes on next Refresh()
    private SolidColorBrush R(string key) =>
        (TryFindResource(key) as SolidColorBrush) ?? Brushes.Gray;

    private FontFamily ThemeFont =>
        (TryFindResource("FontFamilyMono") as FontFamily) ?? new FontFamily("Cascadia Mono");

    public EffectsRack() => InitializeComponent();

    public void Initialize(SynthEngine engine)
    {
        _engine = engine;
        Refresh();
    }

    public void Refresh()
    {
        SlotsPanel.Children.Clear();
        ParamsPanel.Children.Clear();
        _selectedIndex = -1;

        if (_engine == null) return;

        for (int i = 0; i < _engine.Effects.Count; i++)
            SlotsPanel.Children.Add(BuildSlot(i, _engine.Effects[i]));
    }

    private UIElement BuildSlot(int idx, IEffect fx)
    {
        bool bypassed = fx.Bypass;
        var eb    = fx as EffectBase;
        var route = eb?.Channel ?? ChannelRoute.Both;

        string bgKey     = bypassed ? "BackgroundDeepBrush"  : "BackgroundPrimaryBrush";
        string accentKey = bypassed ? "TextTertiaryBrush"    : "AccentPrimaryBrush";

        var border = new Border
        {
            Width           = 100,
            Height          = 50,
            Margin          = new Thickness(2, 0, 2, 0),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(2),
            Cursor          = Cursors.Hand,
            ToolTip         = $"{fx.Name}\nLeft-click: edit  |  Right-click: bypass/enable"
        };
        border.SetResourceReference(Border.BackgroundProperty,   bgKey);
        border.SetResourceReference(Border.BorderBrushProperty,  accentKey);

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });

        // ── Top row: [CH badge] [name] [×]
        var topGrid = new Grid();
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Channel badge
        var chText = new TextBlock
        {
            Text                = ChannelLabel(route),
            FontFamily          = ThemeFont,
            FontSize            = 8,
            FontWeight          = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        chText.SetResourceReference(TextBlock.ForegroundProperty, accentKey);

        var chBtn = new Border
        {
            Width               = 20,
            Height              = 14,
            BorderThickness     = new Thickness(1),
            CornerRadius        = new CornerRadius(2),
            Margin              = new Thickness(3, 0, 2, 0),
            VerticalAlignment   = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background          = bypassed
                ? R("BackgroundDeepBrush")
                : new SolidColorBrush(Color.FromArgb(60, 0, 200, 80)),
            Child = chText
        };
        chBtn.SetResourceReference(Border.BorderBrushProperty, accentKey);
        Grid.SetColumn(chBtn, 0);
        chBtn.MouseLeftButtonDown += (_, e) =>
        {
            if (_engine == null) return;
            var fxNow = _engine.Effects[idx];
            if (fxNow is not EffectBase ebNow) return;
            int ci = Array.IndexOf(ChannelCycle, ebNow.Channel);
            ebNow.Channel = ChannelCycle[(ci + 1) % ChannelCycle.Length];
            Refresh();
            SelectSlot(idx);
            e.Handled = true;
        };
        topGrid.Children.Add(chBtn);

        // Name label
        var nameBlock = new TextBlock
        {
            Text                = fx.Name.ToUpperInvariant(),
            FontFamily          = ThemeFont,
            FontSize            = 10,
            FontWeight          = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        nameBlock.SetResourceReference(TextBlock.ForegroundProperty, accentKey);
        Grid.SetColumn(nameBlock, 1);
        topGrid.Children.Add(nameBlock);

        // Remove × button
        var removeText = new TextBlock
        {
            Text                = "×",
            FontSize            = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        removeText.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiaryBrush");

        var removeBtn = new Border
        {
            Width             = 16,
            Height            = 14,
            Background        = Brushes.Transparent,
            Margin            = new Thickness(2, 0, 3, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child             = removeText
        };
        Grid.SetColumn(removeBtn, 2);
        removeBtn.MouseLeftButtonDown += (_, e) =>
        {
            _engine?.Effects.RemoveAt(idx);
            Refresh();
            e.Handled = true;
        };
        removeBtn.MouseEnter += (_, _) =>
            removeText.Foreground = new SolidColorBrush(Colors.OrangeRed);
        removeBtn.MouseLeave += (_, _) =>
            removeText.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiaryBrush");
        topGrid.Children.Add(removeBtn);

        Grid.SetRow(topGrid, 0);
        grid.Children.Add(topGrid);

        // ── Mix bar
        var barContainer = new Grid { Margin = new Thickness(6, 0, 6, 2) };
        var track = new Border { Height = 3, VerticalAlignment = VerticalAlignment.Bottom };
        track.SetResourceReference(Border.BackgroundProperty, "KnobTrackBrush");

        var fill = new Border
        {
            Height              = 3,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width               = Math.Clamp(fx.Mix, 0, 1) * (100 - 12),
            VerticalAlignment   = VerticalAlignment.Bottom
        };
        fill.SetResourceReference(Border.BackgroundProperty, accentKey);

        barContainer.Children.Add(track);
        barContainer.Children.Add(fill);
        Grid.SetRow(barContainer, 1);
        grid.Children.Add(barContainer);

        border.Child = grid;

        // ── Mouse events
        border.MouseLeftButtonDown += (_, e) =>
        {
            if (e.Handled) return;
            SelectSlot(idx);
        };
        border.MouseRightButtonDown += (_, e) =>
        {
            if (_engine == null) return;
            _engine.Effects[idx].Bypass = !_engine.Effects[idx].Bypass;
            Refresh();
            if (_selectedIndex == idx) SelectSlot(idx);
            e.Handled = true;
        };

        if (_selectedIndex == idx)
        {
            border.BorderBrush     = new SolidColorBrush(Colors.White);
            border.BorderThickness = new Thickness(1.5);
        }

        return border;
    }

    private void SelectSlot(int idx)
    {
        _selectedIndex = idx;
        ParamsPanel.Children.Clear();

        if (_engine == null || idx < 0 || idx >= _engine.Effects.Count) return;

        var fx = _engine.Effects[idx];
        if (fx is not EffectBase eb) return;

        // Rebuild slots to update selection border
        SlotsPanel.Children.Clear();
        for (int i = 0; i < _engine.Effects.Count; i++)
            SlotsPanel.Children.Add(BuildSlot(i, _engine.Effects[i]));

        // "CH:" label
        var chLabel = new TextBlock
        {
            Text              = "CH",
            FontFamily        = ThemeFont,
            FontSize          = 9,
            FontWeight        = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 4, 0)
        };
        chLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        ParamsPanel.Children.Add(chLabel);

        // Channel toggle buttons: ST / L / R
        foreach (var route in ChannelCycle)
        {
            bool isActive = eb.Channel == route;

            var btnText = new TextBlock
            {
                Text                = ChannelLabel(route),
                FontFamily          = ThemeFont,
                FontSize            = 9,
                FontWeight          = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
            };
            if (isActive)
                btnText.SetResourceReference(TextBlock.ForegroundProperty, "BackgroundDeepBrush");
            else
                btnText.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiaryBrush");

            var btn = new Border
            {
                Width             = 28,
                Height            = 22,
                Margin            = new Thickness(1, 0, 1, 0),
                BorderThickness   = new Thickness(1),
                CornerRadius      = new CornerRadius(2),
                Cursor            = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Child             = btnText
            };
            if (isActive)
            {
                btn.SetResourceReference(Border.BackgroundProperty,  "AccentPrimaryBrush");
                btn.SetResourceReference(Border.BorderBrushProperty, "AccentPrimaryBrush");
            }
            else
            {
                btn.SetResourceReference(Border.BackgroundProperty,  "BackgroundPrimaryBrush");
                btn.SetResourceReference(Border.BorderBrushProperty, "TextTertiaryBrush");
            }

            var r = route;
            btn.MouseLeftButtonDown += (_, e) =>
            {
                eb.Channel = r;
                SelectSlot(idx);
                e.Handled = true;
            };
            ParamsPanel.Children.Add(btn);
        }

        // Separator
        var sep = new Border
        {
            Width = 1, Height = 30,
            Margin            = new Thickness(6, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        sep.SetResourceReference(Border.BackgroundProperty, "TextTertiaryBrush");
        ParamsPanel.Children.Add(sep);

        // Parameter knobs
        foreach (var p in eb.GetParameters())
        {
            string fmt = p.Unit is " Hz" or "s" or " bit" or ":1" or "dB" ? "F1" : "P0";
            var knob = new SynthKnob
            {
                Label             = p.Label,
                Minimum           = p.Min,
                Maximum           = p.Max,
                Value             = p.Value,
                DefaultValue      = p.Value,
                Unit              = p.Unit,
                IsBipolar         = p.IsBipolar,
                ValueFormat       = fmt,
                Margin            = new Thickness(2),
                Width             = 62,
                Height            = 62,
                VerticalAlignment = VerticalAlignment.Center
            };
            string paramName = p.Name;
            knob.ValueChanged += (_, e) =>
            {
                eb.SetParameter(paramName, e.NewValue);
                if (paramName == "Mix") Refresh();
            };
            ParamsPanel.Children.Add(knob);
        }
    }

    private void AddEffectBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_engine == null) return;
        var menu = new ContextMenu();
        menu.SetResourceReference(ContextMenu.BackgroundProperty,  "BackgroundSecondaryBrush");
        menu.SetResourceReference(ContextMenu.BorderBrushProperty, "AccentPrimaryBrush");

        foreach (var type in Enum.GetValues<EffectType>())
        {
            var item = new MenuItem
            {
                Header     = type.ToString(),
                FontFamily = ThemeFont,
                FontSize   = 11,
            };
            item.SetResourceReference(MenuItem.ForegroundProperty, "AccentPrimaryBrush");
            item.SetResourceReference(MenuItem.BackgroundProperty, "BackgroundSecondaryBrush");

            var t = type;
            item.Click += (_, _) =>
            {
                var fx = EffectFactory.Create(t);
                fx.Bypass = false;
                fx.Mix    = 0.5;
                _engine.Effects.Add(fx);
                _selectedIndex = _engine.Effects.Count - 1;
                Refresh();
                SelectSlot(_selectedIndex);
            };
            menu.Items.Add(item);
        }
        menu.PlacementTarget = AddEffectBtn;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }
}

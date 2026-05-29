using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OttoSynth.Core;
using OttoSynth.Core.DSP;

namespace OttoSynth.Standalone.Views;

public partial class ArpSequencerPanel : UserControl
{
    private SynthEngine? _engine;

    // Resolve theme brush at call time so theme switches are reflected on next Refresh()
    private SolidColorBrush R(string key) =>
        (TryFindResource(key) as SolidColorBrush) ?? Brushes.Gray;

    private FontFamily ThemeFont =>
        (TryFindResource("FontFamilyMono") as FontFamily) ?? new FontFamily("Cascadia Mono");

    private static readonly string[] NoteNames =
        ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    private static string NoteName(int n) =>
        $"{NoteNames[n % 12]}{n / 12 - 1}";

    private static string RateLabel(NoteRate r) => r switch
    {
        NoteRate.Whole        => "1/1",
        NoteRate.Half         => "1/2",
        NoteRate.Quarter      => "1/4",
        NoteRate.Eighth       => "1/8",
        NoteRate.Sixteenth    => "1/16",
        NoteRate.ThirtySecond => "1/32",
        _                     => r.ToString()
    };

    public ArpSequencerPanel() => InitializeComponent();

    public void Initialize(SynthEngine engine)
    {
        _engine = engine;
        BuildArpControls();
        BuildSeqControls();
        BuildSteps();
    }

    // ─── Refresh playhead (called from UI timer) ────────────────
    public void RefreshPlayhead()
    {
        if (_engine == null) return;
        int current = _engine.Sequencer.CurrentStep;
        int count   = _engine.Sequencer.StepCount;

        for (int i = 0; i < StepsPanel.Children.Count; i++)
        {
            if (StepsPanel.Children[i] is Border cell)
            {
                bool isPlayhead = _engine.Sequencer.Enabled && i == current;
                bool isActive   = _engine.Sequencer.Steps[i].Active;
                StyleStepCell(cell, i, isActive, isPlayhead);
            }
        }
    }

    // ─── ARP controls ───────────────────────────────────────────
    private void BuildArpControls()
    {
        ArpControlsPanel.Children.Clear();
        if (_engine == null) return;
        var arp = _engine.Arpeggiator;

        // Enable toggle
        ArpControlsPanel.Children.Add(MakeEnableToggle("ARP", arp.Enabled, v => arp.Enabled = v));

        // Pattern
        ArpControlsPanel.Children.Add(MakeLabel("PATTERN"));
        var patCombo = MakeCombo(80);
        foreach (var p in Enum.GetValues<ArpPattern>()) patCombo.Items.Add(p.ToString());
        patCombo.SelectedItem = arp.Pattern.ToString();
        patCombo.SelectionChanged += (_, _) =>
        {
            if (Enum.TryParse<ArpPattern>((string)patCombo.SelectedItem, out var p))
                arp.Pattern = p;
        };
        ArpControlsPanel.Children.Add(patCombo);

        // Rate
        ArpControlsPanel.Children.Add(MakeLabel("RATE"));
        var rateCombo = MakeCombo(60);
        foreach (var r in Enum.GetValues<NoteRate>()) rateCombo.Items.Add(RateLabel(r));
        rateCombo.SelectedItem = RateLabel(arp.Rate);
        rateCombo.SelectionChanged += (_, _) =>
        {
            foreach (NoteRate r in Enum.GetValues<NoteRate>())
                if (RateLabel(r) == (string)rateCombo.SelectedItem)
                    arp.Rate = r;
        };
        ArpControlsPanel.Children.Add(rateCombo);

        // Octaves
        ArpControlsPanel.Children.Add(MakeLabel("OCT"));
        var octCombo = MakeCombo(46);
        for (int o = 1; o <= 4; o++) octCombo.Items.Add(o.ToString());
        octCombo.SelectedItem = arp.OctaveRange.ToString();
        octCombo.SelectionChanged += (_, _) =>
        {
            if (int.TryParse((string)octCombo.SelectedItem, out int o)) arp.OctaveRange = o;
        };
        ArpControlsPanel.Children.Add(octCombo);

        // Hold toggle
        ArpControlsPanel.Children.Add(MakeToggleButton("HOLD", arp.Hold, v => arp.Hold = v));
    }

    // ─── SEQ controls ───────────────────────────────────────────
    private void BuildSeqControls()
    {
        SeqControlsPanel.Children.Clear();
        if (_engine == null) return;
        var seq = _engine.Sequencer;

        // Enable toggle
        SeqControlsPanel.Children.Add(MakeEnableToggle("SEQ", seq.Enabled, v => seq.Enabled = v));

        // BPM
        SeqControlsPanel.Children.Add(MakeLabel("BPM"));
        var bpmBox = new TextBox
        {
            Width  = 44,
            Height = 22,
            Text   = _engine.Bpm.ToString(),
            TextAlignment      = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 8, 0),
            FontFamily = ThemeFont,
            FontSize   = 10,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(2, 1, 2, 1)
        };
        bpmBox.SetResourceReference(TextBox.BackgroundProperty,  "BackgroundSecondaryBrush");
        bpmBox.SetResourceReference(TextBox.ForegroundProperty,  "AccentPrimaryBrush");
        bpmBox.SetResourceReference(TextBox.BorderBrushProperty, "TextTertiaryBrush");
        bpmBox.LostFocus += (_, _) =>
        {
            if (int.TryParse(bpmBox.Text, out int bpm))
                _engine.Bpm = Math.Clamp(bpm, 20, 300);
            bpmBox.Text = _engine.Bpm.ToString();
        };
        bpmBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Return) bpmBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        };
        SeqControlsPanel.Children.Add(bpmBox);

        // Steps count
        SeqControlsPanel.Children.Add(MakeLabel("STEPS"));
        var stepsCombo = MakeCombo(50);
        foreach (int n in new[] { 4, 8, 12, 16, 24, 32 }) stepsCombo.Items.Add(n.ToString());
        stepsCombo.SelectedItem = Math.Min(seq.StepCount, 32).ToString();
        stepsCombo.SelectionChanged += (_, _) =>
        {
            if (int.TryParse((string)stepsCombo.SelectedItem, out int n))
            {
                seq.StepCount = n;
                BuildSteps();
            }
        };
        SeqControlsPanel.Children.Add(stepsCombo);

        // Rate
        SeqControlsPanel.Children.Add(MakeLabel("RATE"));
        var rateCombo = MakeCombo(60);
        foreach (var r in Enum.GetValues<NoteRate>()) rateCombo.Items.Add(RateLabel(r));
        rateCombo.SelectedItem = RateLabel(seq.Rate);
        rateCombo.SelectionChanged += (_, _) =>
        {
            foreach (NoteRate r in Enum.GetValues<NoteRate>())
                if (RateLabel(r) == (string)rateCombo.SelectedItem)
                    seq.Rate = r;
        };
        SeqControlsPanel.Children.Add(rateCombo);

        // Clear all steps button
        var clearBtn = MakeSmallButton("CLEAR", () =>
        {
            foreach (var s in _engine.Sequencer.Steps) s.Active = false;
            BuildSteps();
        });
        SeqControlsPanel.Children.Add(clearBtn);

        // Fill all steps button
        var fillBtn = MakeSmallButton("FILL", () =>
        {
            int count = _engine.Sequencer.StepCount;
            for (int i = 0; i < count; i++) _engine.Sequencer.Steps[i].Active = true;
            BuildSteps();
        });
        SeqControlsPanel.Children.Add(fillBtn);
    }

    // ─── Step grid ──────────────────────────────────────────────
    private void BuildSteps()
    {
        StepsPanel.Children.Clear();
        if (_engine == null) return;

        int count = Math.Clamp(_engine.Sequencer.StepCount, 1, SequencerEngine.MaxSteps);
        for (int i = 0; i < count; i++)
            StepsPanel.Children.Add(BuildStepCell(i));
    }

    private UIElement BuildStepCell(int idx)
    {
        var step     = _engine!.Sequencer.Steps[idx];
        bool isActive = step.Active;

        var cell = new Border
        {
            Width           = 58,
            Height          = 108,
            Margin          = new Thickness(2, 0, 2, 0),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(2),
            Cursor          = Cursors.Hand,
            ToolTip         = "Click: toggle  |  Scroll: note  |  Shift+Scroll: velocity"
        };
        StyleStepCell(cell, idx, isActive, false);

        // Inner grid
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });       // step #
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // note
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });     // velocity bar

        // Step number
        var numLabel = new TextBlock
        {
            Text                = (idx + 1).ToString("D2"),
            FontFamily          = ThemeFont,
            FontSize            = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin              = new Thickness(0, 2, 0, 0)
        };
        numLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiaryBrush");
        Grid.SetRow(numLabel, 0);
        grid.Children.Add(numLabel);

        // Note name
        var noteLabel = new TextBlock
        {
            Text                = NoteName(step.Note),
            FontFamily          = ThemeFont,
            FontSize            = 11,
            FontWeight          = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center
        };
        noteLabel.SetResourceReference(TextBlock.ForegroundProperty,
            isActive ? "AccentPrimaryBrush" : "TextTertiaryBrush");
        Grid.SetRow(noteLabel, 1);
        grid.Children.Add(noteLabel);

        // Velocity bar
        var velTrack = new Border
        {
            Height              = 6,
            VerticalAlignment   = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        velTrack.SetResourceReference(Border.BackgroundProperty, "KnobTrackBrush");

        var velFill = new Border
        {
            Height              = 6,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width               = (step.Velocity / 127.0) * 58,
            VerticalAlignment   = VerticalAlignment.Bottom
        };
        velFill.SetResourceReference(Border.BackgroundProperty,
            isActive ? "AccentSecondaryBrush" : "TextTertiaryBrush");

        var velContainer = new Grid();
        velContainer.Children.Add(velTrack);
        velContainer.Children.Add(velFill);
        Grid.SetRow(velContainer, 2);
        grid.Children.Add(velContainer);

        cell.Child = grid;

        // ── Interactions ─────────────────────────────────────────
        cell.MouseLeftButtonDown += (_, e) =>
        {
            step.Active = !step.Active;
            var rebuilt = (Border)BuildStepCell(idx);
            StepsPanel.Children[idx] = rebuilt;
            e.Handled = true;
        };

        cell.MouseWheel += (_, e) =>
        {
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            if (shift)
            {
                step.Velocity = Math.Clamp(step.Velocity + (e.Delta > 0 ? 10 : -10), 1, 127);
            }
            else
            {
                step.Note = Math.Clamp(step.Note + (e.Delta > 0 ? 1 : -1), 0, 127);
            }
            var rebuilt = (Border)BuildStepCell(idx);
            StepsPanel.Children[idx] = rebuilt;
            e.Handled = true;
        };

        return cell;
    }

    private void StyleStepCell(Border cell, int idx, bool isActive, bool isPlayhead)
    {
        if (isPlayhead)
        {
            cell.BorderBrush     = new SolidColorBrush(Colors.White);
            cell.BorderThickness = new Thickness(1.5);
            cell.Background      = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
        }
        else if (isActive)
        {
            cell.SetResourceReference(Border.BorderBrushProperty, "AccentPrimaryBrush");
            cell.BorderThickness = new Thickness(1);
            cell.SetResourceReference(Border.BackgroundProperty,  "BackgroundSecondaryBrush");
        }
        else
        {
            cell.SetResourceReference(Border.BorderBrushProperty, "TextTertiaryBrush");
            cell.BorderThickness = new Thickness(1);
            cell.SetResourceReference(Border.BackgroundProperty,  "BackgroundDeepBrush");
        }
    }

    // ─── Control factory helpers ─────────────────────────────────

    private UIElement MakeEnableToggle(string label, bool initialActive, Action<bool> onToggle)
    {
        bool active = initialActive;

        var text = new TextBlock
        {
            Text                = label,
            FontFamily          = ThemeFont,
            FontSize            = 10,
            FontWeight          = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center
        };

        var btn = new Border
        {
            Width           = 48,
            Height          = 22,
            Margin          = new Thickness(0, 0, 8, 0),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(2),
            Cursor          = Cursors.Hand,
            Child           = text
        };

        void Apply()
        {
            if (active)
            {
                btn.SetResourceReference(Border.BackgroundProperty,  "AccentPrimaryBrush");
                btn.SetResourceReference(Border.BorderBrushProperty, "AccentPrimaryBrush");
                text.SetResourceReference(TextBlock.ForegroundProperty, "BackgroundDeepBrush");
            }
            else
            {
                btn.SetResourceReference(Border.BackgroundProperty,  "BackgroundPrimaryBrush");
                btn.SetResourceReference(Border.BorderBrushProperty, "TextTertiaryBrush");
                text.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            }
        }
        Apply();

        btn.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            active = !active;
            Apply();
            onToggle(active);
        };
        return btn;
    }

    private UIElement MakeToggleButton(string label, bool initialActive, Action<bool> onToggle)
    {
        bool active = initialActive;

        var text = new TextBlock
        {
            Text                = label,
            FontFamily          = ThemeFont,
            FontSize            = 10,
            FontWeight          = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center
        };

        var btn = new Border
        {
            Height          = 22,
            Margin          = new Thickness(6, 0, 0, 0),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(2),
            Cursor          = Cursors.Hand,
            Padding         = new Thickness(8, 0, 8, 0),
            Child           = text
        };

        void Apply()
        {
            btn.SetResourceReference(Border.BackgroundProperty,
                active ? "AccentPrimaryBrush" : "BackgroundPrimaryBrush");
            btn.SetResourceReference(Border.BorderBrushProperty,
                active ? "AccentPrimaryBrush" : "TextTertiaryBrush");
            text.SetResourceReference(TextBlock.ForegroundProperty,
                active ? "BackgroundDeepBrush" : "TextTertiaryBrush");
        }
        Apply();

        btn.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            active = !active;
            Apply();
            onToggle(active);
        };
        return btn;
    }

    private UIElement MakeSmallButton(string label, Action onClick)
    {
        var btn = new Border
        {
            Height          = 22,
            Margin          = new Thickness(6, 0, 0, 0),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(2),
            Cursor          = Cursors.Hand,
            Padding         = new Thickness(6, 0, 6, 0)
        };
        btn.SetResourceReference(Border.BackgroundProperty,  "BackgroundPrimaryBrush");
        btn.SetResourceReference(Border.BorderBrushProperty, "TextTertiaryBrush");

        var text = new TextBlock
        {
            Text                = label,
            FontFamily          = ThemeFont,
            FontSize            = 9,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

        btn.Child = text;
        btn.MouseLeftButtonDown += (_, _) => onClick();
        btn.MouseEnter += (_, _) => btn.SetResourceReference(Border.BorderBrushProperty, "AccentPrimaryBrush");
        btn.MouseLeave += (_, _) => btn.SetResourceReference(Border.BorderBrushProperty, "TextTertiaryBrush");
        return btn;
    }

    private TextBlock MakeLabel(string text) => new()
    {
        Text              = text,
        FontFamily        = ThemeFont,
        FontSize          = 9,
        VerticalAlignment = VerticalAlignment.Center,
        Margin            = new Thickness(4, 0, 3, 0)
    };

    private ComboBox MakeCombo(int width)
    {
        var cb = new ComboBox
        {
            Width  = width,
            Height = 22,
            Margin = new Thickness(0, 0, 8, 0)
        };
        return cb;
    }
}

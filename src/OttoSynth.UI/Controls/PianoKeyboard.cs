using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OttoSynth.UI.Controls;

/// <summary>
/// On-screen piano keyboard. Renders white and black keys, supports mouse clicks
/// (with sliding when dragged), and fires NoteOn / NoteOff events.
/// Colors are driven by DependencyProperties so each theme can style them.
/// </summary>
public class PianoKeyboard : Control
{
    private static readonly PropertyChangedCallback Redraw =
        (d, _) => ((PianoKeyboard)d).InvalidateVisual();

    // ── Range ──────────────────────────────────────────────────────
    public static readonly DependencyProperty StartNoteProperty = DependencyProperty.Register(
        nameof(StartNote), typeof(int), typeof(PianoKeyboard), new PropertyMetadata(48, Redraw));

    public static readonly DependencyProperty EndNoteProperty = DependencyProperty.Register(
        nameof(EndNote), typeof(int), typeof(PianoKeyboard), new PropertyMetadata(84, Redraw));

    public int StartNote { get => (int)GetValue(StartNoteProperty); set => SetValue(StartNoteProperty, value); }
    public int EndNote   { get => (int)GetValue(EndNoteProperty);   set => SetValue(EndNoteProperty,   value); }

    // ── Key colours ────────────────────────────────────────────────
    public static readonly DependencyProperty KeyWhiteBrushProperty = DependencyProperty.Register(
        nameof(KeyWhiteBrush), typeof(Brush), typeof(PianoKeyboard),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0xB0, 0xFF, 0xB0)), Redraw));

    public static readonly DependencyProperty KeyWhiteActiveBrushProperty = DependencyProperty.Register(
        nameof(KeyWhiteActiveBrush), typeof(Brush), typeof(PianoKeyboard),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x41)), Redraw));

    public static readonly DependencyProperty KeyBlackBrushProperty = DependencyProperty.Register(
        nameof(KeyBlackBrush), typeof(Brush), typeof(PianoKeyboard),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x07, 0x1A, 0x0E)), Redraw));

    public static readonly DependencyProperty KeyBlackActiveBrushProperty = DependencyProperty.Register(
        nameof(KeyBlackActiveBrush), typeof(Brush), typeof(PianoKeyboard),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x00)), Redraw));

    public static readonly DependencyProperty KeyBorderBrushProperty = DependencyProperty.Register(
        nameof(KeyBorderBrush), typeof(Brush), typeof(PianoKeyboard),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x1A, 0x4D, 0x23)), Redraw));

    public Brush KeyWhiteBrush       { get => (Brush)GetValue(KeyWhiteBrushProperty);       set => SetValue(KeyWhiteBrushProperty,       value); }
    public Brush KeyWhiteActiveBrush { get => (Brush)GetValue(KeyWhiteActiveBrushProperty); set => SetValue(KeyWhiteActiveBrushProperty, value); }
    public Brush KeyBlackBrush       { get => (Brush)GetValue(KeyBlackBrushProperty);       set => SetValue(KeyBlackBrushProperty,       value); }
    public Brush KeyBlackActiveBrush { get => (Brush)GetValue(KeyBlackActiveBrushProperty); set => SetValue(KeyBlackActiveBrushProperty, value); }
    public Brush KeyBorderBrush      { get => (Brush)GetValue(KeyBorderBrushProperty);      set => SetValue(KeyBorderBrushProperty,      value); }

    // ── Events ─────────────────────────────────────────────────────
    public event EventHandler<int>? NoteOn;
    public event EventHandler<int>? NoteOff;

    private readonly bool[] _activeNotes = new bool[128];
    private int _hoverNote = -1;

    public PianoKeyboard()
    {
        Focusable = true;
    }

    /// <summary>Highlights a key as currently held down. Does not fire NoteOn.</summary>
    public void SetNoteActive(int note, bool active)
    {
        if (note < 0 || note >= 128) return;
        _activeNotes[note] = active;
        InvalidateVisual();
    }

    private static bool IsBlackKey(int note)
    {
        int n = note % 12;
        return n == 1 || n == 3 || n == 6 || n == 8 || n == 10;
    }

    private int CountWhiteKeys()
    {
        int count = 0;
        for (int n = StartNote; n < EndNote; n++)
            if (!IsBlackKey(n)) count++;
        return count;
    }

    private int NoteAt(Point p)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return -1;

        int whiteCount = CountWhiteKeys();
        if (whiteCount == 0) return -1;

        double whiteW = w / whiteCount;
        double blackW = whiteW * 0.6;
        double blackH = h * 0.6;

        // Check black keys first (drawn on top)
        int whiteIndex = 0;
        for (int n = StartNote; n < EndNote; n++)
        {
            if (!IsBlackKey(n))
            {
                whiteIndex++;
            }
            else if (p.Y <= blackH)
            {
                double xPos = (whiteIndex - 1) * whiteW + whiteW - blackW / 2.0;
                if (p.X >= xPos && p.X < xPos + blackW) return n;
            }
        }

        // Fall back to white key
        int wi = (int)(p.X / whiteW);
        whiteIndex = 0;
        for (int n = StartNote; n < EndNote; n++)
        {
            if (!IsBlackKey(n))
            {
                if (whiteIndex == wi) return n;
                whiteIndex++;
            }
        }
        return -1;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        int whiteCount = CountWhiteKeys();
        if (whiteCount == 0) return;

        // Fill background
        if (Background != null)
            dc.DrawRectangle(Background, null, new Rect(0, 0, w, h));

        double whiteW = w / whiteCount;
        double blackW = whiteW * 0.6;
        double blackH = h * 0.6;

        var keyPen   = new Pen(KeyBorderBrush,  1);
        var blackPen = new Pen(Brushes.Black,   1);

        // White keys
        int whiteIndex = 0;
        for (int n = StartNote; n < EndNote; n++)
        {
            if (!IsBlackKey(n))
            {
                Brush b = _activeNotes[n] ? KeyWhiteActiveBrush : KeyWhiteBrush;
                var rect = new Rect(whiteIndex * whiteW + 1, 2, whiteW - 2, h - 4);
                dc.DrawRoundedRectangle(b, keyPen, rect, 3, 3);
                whiteIndex++;
            }
        }

        // Black keys on top
        whiteIndex = 0;
        for (int n = StartNote; n < EndNote; n++)
        {
            if (!IsBlackKey(n))
            {
                whiteIndex++;
            }
            else
            {
                Brush b = _activeNotes[n] ? KeyBlackActiveBrush : KeyBlackBrush;
                double xPos = (whiteIndex - 1) * whiteW + whiteW - blackW / 2.0;
                var rect = new Rect(xPos, 2, blackW, blackH);
                dc.DrawRoundedRectangle(b, blackPen, rect, 2, 2);
            }
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        int note = NoteAt(e.GetPosition(this));
        if (note >= 0)
        {
            _hoverNote = note;
            CaptureMouse();
            FireNoteOn(note);
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (_hoverNote >= 0)
        {
            FireNoteOff(_hoverNote);
            _hoverNote = -1;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.LeftButton == MouseButtonState.Pressed && IsMouseCaptured)
        {
            int note = NoteAt(e.GetPosition(this));
            if (note != _hoverNote)
            {
                if (_hoverNote >= 0) FireNoteOff(_hoverNote);
                if (note >= 0) FireNoteOn(note);
                _hoverNote = note;
            }
        }
    }

    private void FireNoteOn(int note)
    {
        SetNoteActive(note, true);
        NoteOn?.Invoke(this, note);
    }

    private void FireNoteOff(int note)
    {
        SetNoteActive(note, false);
        NoteOff?.Invoke(this, note);
    }
}

using System;

namespace OttoSynth.Core.DSP;

public enum ArpPattern { Up, Down, UpDown, Random, AsPlayed }

/// <summary>Step rate expressed as note value denominator (1=whole, 16=sixteenth, etc.).</summary>
public enum NoteRate { Whole = 1, Half = 2, Quarter = 4, Eighth = 8, Sixteenth = 16, ThirtySecond = 32 }

/// <summary>
/// Real-time arpeggiator. Holds MIDI notes and generates a pattern of NoteOn/NoteOff events
/// on the audio thread via Tick(). Thread-safe: NoteOn/NoteOff may arrive from the MIDI thread.
///
/// Zero-allocation contract:
/// - Tick() (audio thread) makes no allocations and acquires the lock only briefly to snapshot notes.
/// - Held notes are stored in pre-allocated arrays (max 16 notes) kept sorted by insertion.
/// - Pattern building writes into a pre-allocated buffer (max 16 notes × 4 octaves).
/// </summary>
public sealed class ArpeggiatorEngine
{
    /// <summary>Maximum simultaneously held notes.</summary>
    public const int MaxHeldNotes = 16;

    /// <summary>Maximum pattern length: MaxHeldNotes × max OctaveRange.</summary>
    private const int MaxPatternLength = MaxHeldNotes * 4;

    public bool       Enabled     { get; set; }
    public ArpPattern Pattern     { get; set; } = ArpPattern.Up;
    public NoteRate   Rate        { get; set; } = NoteRate.Sixteenth;
    public int        OctaveRange { get; set; } = 1;   // 1–4
    public bool       Hold        { get; set; }

    // Pre-allocated sorted-by-insertion held/latched notes (audio-thread reads snapshot).
    private readonly int[] _heldNotes    = new int[MaxHeldNotes];
    private int            _heldCount;
    private readonly int[] _latchedNotes = new int[MaxHeldNotes];
    private int            _latchedCount;

    // Audio-thread scratch: snapshot of held notes + built pattern. Sized at max.
    private readonly int[] _snapshot = new int[MaxHeldNotes];
    private readonly int[] _pattern  = new int[MaxPatternLength];

    private readonly object _noteLock = new();
    private readonly Random _rng      = new();

    private int  _stepIndex;
    private int  _direction        = 1;
    private long _nextStepSample;
    private bool _initialized;
    private volatile int _activeNote = -1;

    public int ActiveNote => _activeNote;

    // ─── MIDI thread calls ──────────────────────────────────────

    public void NoteOn(int note)
    {
        lock (_noteLock)
        {
            InsertSorted(_heldNotes, ref _heldCount, note);
            if (Hold)
                InsertSorted(_latchedNotes, ref _latchedCount, note);
        }
    }

    public void NoteOff(int note)
    {
        lock (_noteLock)
        {
            RemoveSorted(_heldNotes, ref _heldCount, note);
            if (!Hold)
                RemoveSorted(_latchedNotes, ref _latchedCount, note);
        }
    }

    // ─── Audio thread calls ─────────────────────────────────────

    /// <summary>
    /// Called once per audio buffer from the audio thread.
    /// Generates NoteOn/NoteOff callbacks at the correct sample positions.
    /// Zero-allocation: snapshots into pre-allocated buffers under a brief lock.
    /// </summary>
    public void Tick(long samplePos, int sampleCount, int sampleRate, int bpm,
        Action<int, int> noteOn, Action<int> noteOff)
    {
        if (!Enabled)
        {
            if (_activeNote >= 0) { noteOff(_activeNote); _activeNote = -1; }
            _initialized = false;
            return;
        }

        double samplesPerStep = sampleRate * 60.0 / Math.Max(1, bpm) * 4.0 / (int)Rate;

        if (!_initialized)
        {
            _nextStepSample = samplePos;
            _initialized    = true;
        }

        while (_nextStepSample < samplePos + sampleCount)
        {
            // Brief lock — copy sorted notes into the pre-allocated snapshot buffer.
            int snapCount;
            lock (_noteLock)
            {
                bool useLatched = Hold && _latchedCount > 0;
                int  count      = useLatched ? _latchedCount : _heldCount;
                int[] src       = useLatched ? _latchedNotes : _heldNotes;
                Array.Copy(src, _snapshot, count);
                snapCount = count;
            }

            // Release previous note
            if (_activeNote >= 0) { noteOff(_activeNote); _activeNote = -1; }

            if (snapCount > 0)
            {
                int patternCount = BuildPattern(_snapshot, snapCount);
                if (patternCount > 0)
                {
                    _stepIndex = Math.Clamp(_stepIndex, 0, patternCount - 1);
                    int note   = _pattern[_stepIndex];
                    noteOn(note, 100);
                    _activeNote = note;
                    AdvanceStep(patternCount);
                }
            }

            _nextStepSample += (long)samplesPerStep;
        }
    }

    public void Reset(Action<int> noteOff)
    {
        if (_activeNote >= 0) { noteOff(_activeNote); _activeNote = -1; }
        _stepIndex    = 0;
        _direction    = 1;
        _initialized  = false;
        lock (_noteLock) { _heldCount = 0; _latchedCount = 0; }
    }

    // ─── Helpers ────────────────────────────────────────────────

    /// <summary>
    /// Builds the arpeggio pattern from a sorted snapshot of held notes.
    /// Writes into <see cref="_pattern"/>. Returns the pattern length.
    /// Zero-allocation. <paramref name="held"/> must already be sorted ascending.
    /// </summary>
    private int BuildPattern(int[] held, int heldCount)
    {
        int octaves = Math.Clamp(OctaveRange, 1, 4);
        int patternCount = 0;

        for (int oct = 0; oct < octaves; oct++)
        {
            for (int i = 0; i < heldCount; i++)
            {
                int shifted = held[i] + oct * 12;
                if (shifted <= 127 && patternCount < MaxPatternLength)
                    _pattern[patternCount++] = shifted;
            }
        }

        if (Pattern == ArpPattern.Down)
            ReverseInPlace(_pattern, patternCount);

        return patternCount;
    }

    private static void ReverseInPlace(int[] arr, int count)
    {
        for (int i = 0, j = count - 1; i < j; i++, j--)
            (arr[i], arr[j]) = (arr[j], arr[i]);
    }

    /// <summary>Inserts a value into a sorted array, maintaining ascending order. No-op if full or already present.</summary>
    private static void InsertSorted(int[] arr, ref int count, int value)
    {
        if (count >= MaxHeldNotes) return;
        // Linear search for insertion point. At MaxHeldNotes=16 this is faster than BinarySearch.
        int pos = 0;
        while (pos < count && arr[pos] < value) pos++;
        if (pos < count && arr[pos] == value) return; // duplicate
        // Shift right
        for (int i = count; i > pos; i--) arr[i] = arr[i - 1];
        arr[pos] = value;
        count++;
    }

    /// <summary>Removes a value from a sorted array. No-op if not present.</summary>
    private static void RemoveSorted(int[] arr, ref int count, int value)
    {
        int pos = 0;
        while (pos < count && arr[pos] != value) pos++;
        if (pos == count) return;
        for (int i = pos; i < count - 1; i++) arr[i] = arr[i + 1];
        count--;
    }

    private void AdvanceStep(int count)
    {
        switch (Pattern)
        {
            case ArpPattern.Up:
            case ArpPattern.Down:
            case ArpPattern.AsPlayed:
                _stepIndex = (_stepIndex + 1) % count;
                break;
            case ArpPattern.UpDown:
                _stepIndex += _direction;
                if (_stepIndex >= count)
                {
                    _direction = -1;
                    _stepIndex = Math.Max(0, count - 2);
                }
                else if (_stepIndex < 0)
                {
                    _direction = 1;
                    _stepIndex = Math.Min(1, count - 1);
                }
                break;
            case ArpPattern.Random:
                _stepIndex = _rng.Next(count);
                break;
        }
    }
}

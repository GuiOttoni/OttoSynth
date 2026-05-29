using System;
using System.Collections.Generic;

namespace OttoSynth.Core.DSP;

public enum ArpPattern { Up, Down, UpDown, Random, AsPlayed }

/// <summary>Step rate expressed as note value denominator (1=whole, 16=sixteenth, etc.).</summary>
public enum NoteRate { Whole = 1, Half = 2, Quarter = 4, Eighth = 8, Sixteenth = 16, ThirtySecond = 32 }

/// <summary>
/// Real-time arpeggiator. Holds MIDI notes and generates a pattern of NoteOn/NoteOff events
/// on the audio thread via Tick(). Thread-safe: NoteOn/NoteOff may arrive from the MIDI thread.
/// </summary>
public sealed class ArpeggiatorEngine
{
    public bool       Enabled     { get; set; }
    public ArpPattern Pattern     { get; set; } = ArpPattern.Up;
    public NoteRate   Rate        { get; set; } = NoteRate.Sixteenth;
    public int        OctaveRange { get; set; } = 1;   // 1–4
    public bool       Hold        { get; set; }

    private readonly List<int> _heldNotes    = new();
    private readonly List<int> _latchedNotes = new();
    private readonly object    _noteLock     = new();
    private readonly Random    _rng          = new();

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
            if (!_heldNotes.Contains(note))
                _heldNotes.Add(note);
            if (Hold && !_latchedNotes.Contains(note))
                _latchedNotes.Add(note);
        }
    }

    public void NoteOff(int note)
    {
        lock (_noteLock)
        {
            _heldNotes.Remove(note);
            if (!Hold)
                _latchedNotes.Remove(note);
        }
    }

    // ─── Audio thread calls ─────────────────────────────────────

    /// <summary>
    /// Called once per audio buffer from the audio thread.
    /// Generates NoteOn/NoteOff callbacks at the correct sample positions.
    /// </summary>
    public void Tick(long samplePos, int sampleCount, int sampleRate, int bpm,
        Action<int, int> noteOn, Action<int> noteOff)
    {
        if (!Enabled)
        {
            // Clean up active note when arp is disabled
            if (_activeNote >= 0)
            {
                noteOff(_activeNote);
                _activeNote = -1;
            }
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
            List<int> notes;
            lock (_noteLock)
            {
                var src = Hold && _latchedNotes.Count > 0 ? _latchedNotes : _heldNotes;
                notes = new List<int>(src);
            }

            // Release previous note
            if (_activeNote >= 0)
            {
                noteOff(_activeNote);
                _activeNote = -1;
            }

            if (notes.Count > 0)
            {
                var pattern = BuildPattern(notes);
                if (pattern.Count > 0)
                {
                    _stepIndex = Math.Clamp(_stepIndex, 0, pattern.Count - 1);
                    int note   = pattern[_stepIndex];
                    noteOn(note, 100);
                    _activeNote = note;
                    AdvanceStep(pattern.Count);
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
        lock (_noteLock) { _heldNotes.Clear(); _latchedNotes.Clear(); }
    }

    // ─── Helpers ────────────────────────────────────────────────

    private List<int> BuildPattern(List<int> held)
    {
        held.Sort();
        var list = new List<int>();
        for (int oct = 0; oct < Math.Max(1, OctaveRange); oct++)
            foreach (int n in held)
            {
                int shifted = n + oct * 12;
                if (shifted <= 127) list.Add(shifted);
            }
        if (Pattern == ArpPattern.Down)
            list.Reverse();
        return list;
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

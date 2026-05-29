using System;

namespace OttoSynth.Core.DSP;

public sealed class SequencerStep
{
    public bool Active   { get; set; } = false;
    public int  Note     { get; set; } = 60;   // Middle C
    public int  Velocity { get; set; } = 100;
    public bool Tied     { get; set; } = false;
}

/// <summary>
/// Step sequencer. Loops through a fixed step grid and generates NoteOn/NoteOff events
/// on the audio thread via Tick(). Steps are read-only on the audio thread (UI writes are rare
/// and single-field assignments on x64 are atomic — occasional stale reads are imperceptible).
/// </summary>
public sealed class SequencerEngine
{
    public const int MaxSteps = 32;

    public bool      Enabled   { get; set; }
    public int       StepCount { get; set; } = 16;
    public NoteRate  Rate      { get; set; } = NoteRate.Sixteenth;

    public SequencerStep[] Steps { get; } = new SequencerStep[MaxSteps];

    private int  _writeStep;   // tracks next-to-play index (audio thread)
    private long _nextStepSample;
    private bool _initialized;
    private int  _activeNote = -1;

    // Read by UI thread for playhead display (volatile = safe cross-thread read)
    private volatile int _currentStep;
    public int CurrentStep => _currentStep;

    public SequencerEngine()
    {
        for (int i = 0; i < MaxSteps; i++)
            Steps[i] = new SequencerStep();
    }

    // ─── Audio thread ───────────────────────────────────────────

    public void Tick(long samplePos, int sampleCount, int sampleRate, int bpm,
        Action<int, int> noteOn, Action<int> noteOff)
    {
        if (!Enabled)
        {
            if (_activeNote >= 0) { noteOff(_activeNote); _activeNote = -1; }
            _initialized = false;
            _writeStep   = 0;
            _currentStep = 0;
            return;
        }

        int count = Math.Clamp(StepCount, 1, MaxSteps);
        double samplesPerStep = sampleRate * 60.0 / Math.Max(1, bpm) * 4.0 / (int)Rate;

        if (!_initialized)
        {
            _nextStepSample = samplePos;
            _initialized    = true;
        }

        while (_nextStepSample < samplePos + sampleCount)
        {
            var step = Steps[_writeStep];

            bool tied = _activeNote >= 0 && step.Tied && step.Active && step.Note == _activeNote;
            if (_activeNote >= 0 && !tied)
            {
                noteOff(_activeNote);
                _activeNote = -1;
            }

            if (step.Active && !tied)
            {
                noteOn(step.Note, Math.Clamp(step.Velocity, 1, 127));
                _activeNote = step.Note;
            }

            _currentStep = _writeStep;
            _writeStep   = (_writeStep + 1) % count;
            _nextStepSample += (long)samplesPerStep;
        }
    }

    public void Reset(Action<int> noteOff)
    {
        if (_activeNote >= 0) { noteOff(_activeNote); _activeNote = -1; }
        _writeStep   = 0;
        _currentStep = 0;
        _initialized = false;
    }
}

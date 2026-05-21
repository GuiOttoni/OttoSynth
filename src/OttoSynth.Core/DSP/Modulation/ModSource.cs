namespace OttoSynth.Core.DSP.Modulation;

/// <summary>
/// All available modulation sources in the synthesizer.
/// Per-voice sources provide a value per note; global sources share one value.
/// </summary>
public enum ModSource : byte
{
    None = 0,

    // ── Per-voice envelopes (unipolar 0..1) ─────────────────
    Envelope1 = 1,   // ENV1 (Amp)
    Envelope2 = 2,   // ENV2 (Filter)
    Envelope3 = 3,   // ENV3 (Free)

    // ── Per-voice LFOs (bipolar -1..+1 or unipolar 0..+1) ──
    Lfo1 = 10,
    Lfo2 = 11,
    Lfo3 = 12,

    // ── Per-note (set at NoteOn, constant during note) ──────
    Velocity = 20,       // 0..1 from MIDI velocity
    KeyTracking = 21,    // -1..+1 centered on C4 (note 60)
    NoteRandom = 22,     // 0..1 random value per note

    // ── Global (shared across all voices) ───────────────────
    ModWheel = 30,       // CC#1, 0..1
    PitchBend = 31,      // -1..+1
    Aftertouch = 32,     // 0..1 (channel pressure)

    // ── Macro controls ──────────────────────────────────────
    Macro1 = 40,
    Macro2 = 41,
    Macro3 = 42,
    Macro4 = 43,
}

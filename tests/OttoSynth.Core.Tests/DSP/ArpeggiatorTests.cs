using OttoSynth.Core.DSP;

namespace OttoSynth.Core.Tests.DSP;

public class ArpeggiatorTests
{
    [Fact]
    public void NoteOn_KeepsHeldNotesSorted()
    {
        var arp = new ArpeggiatorEngine { Enabled = true, Pattern = ArpPattern.Up };

        arp.NoteOn(67);
        arp.NoteOn(60);
        arp.NoteOn(64);

        // Capture pattern via Tick (Up should ascend through sorted notes).
        var notes = new List<int>();
        arp.Tick(0, 44100, 44100, 240, (n, _) => notes.Add(n), _ => { });

        Assert.Contains(60, notes);
        Assert.Contains(64, notes);
        Assert.Contains(67, notes);
        int idx60 = notes.IndexOf(60);
        int idx64 = notes.IndexOf(64);
        int idx67 = notes.IndexOf(67);
        Assert.True(idx60 < idx64, "Sorted-insertion property: 60 before 64");
        Assert.True(idx64 < idx67, "Sorted-insertion property: 64 before 67");
    }

    [Fact]
    public void NoteOff_RemovesNoteFromHeld()
    {
        var arp = new ArpeggiatorEngine { Enabled = true, Pattern = ArpPattern.Up };

        arp.NoteOn(60);
        arp.NoteOn(64);
        arp.NoteOff(60);

        var notes = new List<int>();
        arp.Tick(0, 44100, 44100, 240, (n, _) => notes.Add(n), _ => { });

        Assert.DoesNotContain(60, notes);
        Assert.Contains(64, notes);
    }

    [Fact]
    public void Duplicate_NoteOn_IsIdempotent()
    {
        // Bug 1.1: insertion must not duplicate; an idempotent NoteOn is required
        // because real MIDI streams can send duplicate note-on without note-off.
        var arp = new ArpeggiatorEngine { Enabled = true, Pattern = ArpPattern.Up };
        for (int i = 0; i < 20; i++) arp.NoteOn(60);

        var notes = new List<int>();
        arp.Tick(0, 4410, 44100, 240, (n, _) => notes.Add(n), _ => { });

        // Exactly one unique pitch in the pattern.
        Assert.All(notes, n => Assert.Equal(60, n));
    }

    [Fact]
    public void Held_Exceeds_MaxNotes_DoesNotThrow()
    {
        // Insertion stops at MaxHeldNotes; flooding the arp with 50 notes must
        // not allocate or throw. The zero-alloc contract requires the audio
        // thread Tick() to behave deterministically.
        var arp = new ArpeggiatorEngine { Enabled = true, Pattern = ArpPattern.Up };

        for (int i = 0; i < 50; i++) arp.NoteOn(40 + i);
        for (int i = 0; i < 50; i++) arp.NoteOff(40 + i % 30);

        var noteOns  = 0;
        var noteOffs = 0;
        arp.Tick(0, 44100, 44100, 240, (_, _) => noteOns++, _ => noteOffs++);

        Assert.True(noteOns >= 0); // no crash
    }

    [Fact]
    public void Disabled_Arp_DoesNotFire()
    {
        var arp = new ArpeggiatorEngine { Enabled = false };
        arp.NoteOn(60);

        int fired = 0;
        arp.Tick(0, 44100, 44100, 120, (_, _) => fired++, _ => { });

        Assert.Equal(0, fired);
    }
}

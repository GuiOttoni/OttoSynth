using System;

namespace OttoSynth.Core.Midi;

/// <summary>
/// Represents a parsed MIDI event for the synth engine.
/// </summary>
public readonly struct MidiEvent
{
    public enum EventType : byte
    {
        NoteOn,
        NoteOff,
        PitchBend,
        ModWheel,
        Aftertouch,
        ControlChange
    }

    /// <summary>Type of MIDI event.</summary>
    public readonly EventType Type;

    /// <summary>MIDI channel (0-15).</summary>
    public readonly byte Channel;

    /// <summary>Note number (0-127) for NoteOn/NoteOff, CC number for ControlChange.</summary>
    public readonly byte Data1;

    /// <summary>Velocity (0-127) for NoteOn/NoteOff, CC value for ControlChange.</summary>
    public readonly byte Data2;

    /// <summary>14-bit pitch bend value (0-16383, center=8192).</summary>
    public readonly int PitchBendValue;

    /// <summary>Sample offset within the current buffer (for sample-accurate timing).</summary>
    public readonly int SampleOffset;

    private MidiEvent(EventType type, byte channel, byte data1, byte data2, int pitchBend, int sampleOffset)
    {
        Type = type;
        Channel = channel;
        Data1 = data1;
        Data2 = data2;
        PitchBendValue = pitchBend;
        SampleOffset = sampleOffset;
    }

    public static MidiEvent NoteOn(byte note, byte velocity, byte channel = 0, int sampleOffset = 0)
        => new(EventType.NoteOn, channel, note, velocity, 0, sampleOffset);

    public static MidiEvent NoteOff(byte note, byte velocity = 0, byte channel = 0, int sampleOffset = 0)
        => new(EventType.NoteOff, channel, note, velocity, 0, sampleOffset);

    public static MidiEvent PitchBend(int value, byte channel = 0, int sampleOffset = 0)
        => new(EventType.PitchBend, channel, 0, 0, value, sampleOffset);

    public static MidiEvent ModWheel(byte value, byte channel = 0, int sampleOffset = 0)
        => new(EventType.ModWheel, channel, 1, value, 0, sampleOffset);

    public static MidiEvent Aftertouch(byte pressure, byte channel = 0, int sampleOffset = 0)
        => new(EventType.Aftertouch, channel, pressure, 0, 0, sampleOffset);

    public static MidiEvent CC(byte ccNumber, byte value, byte channel = 0, int sampleOffset = 0)
        => new(EventType.ControlChange, channel, ccNumber, value, 0, sampleOffset);
}

/// <summary>
/// Parses raw MIDI bytes into MidiEvent structs.
/// Stateless and allocation-free.
/// </summary>
public static class MidiProcessor
{
    /// <summary>
    /// Parses a 3-byte MIDI message into a MidiEvent.
    /// Returns null if the message type is not supported.
    /// </summary>
    public static MidiEvent? Parse(byte status, byte data1, byte data2, int sampleOffset = 0)
    {
        byte channel = (byte)(status & 0x0F);
        byte messageType = (byte)(status & 0xF0);

        return messageType switch
        {
            0x90 when data2 > 0 => MidiEvent.NoteOn(data1, data2, channel, sampleOffset),
            0x90 when data2 == 0 => MidiEvent.NoteOff(data1, 0, channel, sampleOffset), // Note On with vel 0 = Note Off
            0x80 => MidiEvent.NoteOff(data1, data2, channel, sampleOffset),
            0xE0 => MidiEvent.PitchBend((data2 << 7) | data1, channel, sampleOffset),
            0xD0 => MidiEvent.Aftertouch(data1, channel, sampleOffset),
            0xB0 when data1 == 1 => MidiEvent.ModWheel(data2, channel, sampleOffset),
            0xB0 => MidiEvent.CC(data1, data2, channel, sampleOffset),
            _ => null
        };
    }
}

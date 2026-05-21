using System;
using System.Collections.Generic;
using NAudio.Midi;
using OttoSynth.Core;
using OttoSynth.Core.Diagnostics;
using OttoSynth.Core.Midi;
using MidiEvent = OttoSynth.Core.Midi.MidiEvent;

namespace OttoSynth.Standalone.Services;

/// <summary>
/// Manages NAudio MidiIn devices and routes MIDI messages to the SynthEngine.
/// </summary>
public sealed class MidiService : IDisposable
{
    private readonly SynthEngine _engine;
    private MidiIn? _midiIn;

    public event Action<int, bool>? NoteActiveChanged;

    public int ActiveDeviceIndex { get; private set; } = -1;

    public static int DeviceCount => MidiIn.NumberOfDevices;

    public MidiService(SynthEngine engine) => _engine = engine;

    public IReadOnlyList<string> GetDeviceNames()
    {
        var names = new List<string>();
        for (int i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            try { names.Add(MidiIn.DeviceInfo(i).ProductName); }
            catch { names.Add($"<device {i}>"); }
        }
        return names;
    }

    public void Connect(int deviceIndex)
    {
        Disconnect();
        if (deviceIndex < 0 || deviceIndex >= MidiIn.NumberOfDevices) return;

        _midiIn = new MidiIn(deviceIndex);
        _midiIn.MessageReceived += OnMessageReceived;
        _midiIn.ErrorReceived += (_, _) => { };
        _midiIn.Start();
        ActiveDeviceIndex = deviceIndex;
        Logger.Info("MidiService", $"Connected to: {MidiIn.DeviceInfo(deviceIndex).ProductName}");
    }

    public void Disconnect()
    {
        _midiIn?.Stop();
        _midiIn?.Dispose();
        _midiIn = null;
        ActiveDeviceIndex = -1;
    }

    private void OnMessageReceived(object? sender, MidiInMessageEventArgs e)
    {
        byte status = e.MidiEvent.CommandCode switch
        {
            MidiCommandCode.NoteOn          => 0x90,
            MidiCommandCode.NoteOff         => 0x80,
            MidiCommandCode.PitchWheelChange => 0xE0,
            MidiCommandCode.ControlChange   => 0xB0,
            _                               => 0
        };
        if (status == 0) return;

        status |= (byte)(e.MidiEvent.Channel - 1);
        byte d1 = 0, d2 = 0;

        switch (e.MidiEvent)
        {
            case NAudio.Midi.NoteEvent n:
                d1 = (byte)n.NoteNumber;
                d2 = (byte)n.Velocity;
                break;
            case NAudio.Midi.PitchWheelChangeEvent p:
                int pitch = p.Pitch + 8192;
                d1 = (byte)(pitch & 0x7F);
                d2 = (byte)((pitch >> 7) & 0x7F);
                break;
            case NAudio.Midi.ControlChangeEvent cc:
                d1 = (byte)cc.Controller;
                d2 = (byte)cc.ControllerValue;
                break;
        }

        var parsed = MidiProcessor.Parse(status, d1, d2);
        if (parsed.HasValue) _engine.ProcessMidiEvent(parsed.Value);

        if (e.MidiEvent is NAudio.Midi.NoteEvent ne)
        {
            bool on = e.MidiEvent.CommandCode == MidiCommandCode.NoteOn && ne.Velocity > 0;
            NoteActiveChanged?.Invoke(ne.NoteNumber, on);
        }
    }

    public void Dispose() => Disconnect();
}

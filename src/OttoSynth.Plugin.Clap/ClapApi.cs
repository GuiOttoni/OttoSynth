// CLAP API native struct definitions — translation of clap/include/clap/*.h to C#.
// Only the structs we actually implement are listed; full CLAP has dozens more.
//
// All structs use sequential layout matching C ABI exactly. Function-pointer fields
// use `delegate* unmanaged[Cdecl]<...>` which NativeAOT compiles to raw fn pointers.

using System;
using System.Runtime.InteropServices;

namespace OttoSynth.Plugin.Clap;

// ─── Versioning ────────────────────────────────────────────────

[StructLayout(LayoutKind.Sequential)]
public struct ClapVersion
{
    public uint Major;
    public uint Minor;
    public uint Revision;
}

// ─── Plugin descriptor (static metadata) ───────────────────────

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ClapPluginDescriptor
{
    public ClapVersion ClapVersion;
    public byte* Id;            // "io.ottosound.ottosynth"
    public byte* Name;          // "OttoSynth"
    public byte* Vendor;        // "OttoSound"
    public byte* Url;
    public byte* ManualUrl;
    public byte* SupportUrl;
    public byte* Version;       // "1.1.0"
    public byte* Description;
    public byte** Features;     // null-terminated array of feature strings (e.g. "instrument", "synthesizer")
}

// ─── Plugin instance (runtime) ─────────────────────────────────

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ClapPlugin
{
    public ClapPluginDescriptor* Descriptor;
    public void* PluginData;    // managed-allocated handle to per-instance state

    public delegate* unmanaged[Cdecl]<ClapPlugin*, bool>                Init;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, void>                Destroy;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, double, uint, uint, bool> Activate;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, void>                Deactivate;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, bool>                StartProcessing;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, void>                StopProcessing;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, void>                Reset;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, ClapProcess*, int>   Process;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte*, void*>        GetExtension;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, void>                OnMainThread;
}

// ─── Audio buffer ──────────────────────────────────────────────

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ClapAudioBuffer
{
    public float**  Data32;          // [channel_count][frame_count]
    public double** Data64;          // [channel_count][frame_count] (used when uses_double=true)
    public uint     ChannelCount;
    public uint     Latency;
    public ulong    ConstantMask;
}

// ─── Process context ───────────────────────────────────────────

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ClapProcess
{
    public long              SteadyTime;
    public uint              FramesCount;
    public ClapEventTransport* Transport;
    public ClapAudioBuffer*  AudioInputs;
    public ClapAudioBuffer*  AudioOutputs;
    public uint              AudioInputsCount;
    public uint              AudioOutputsCount;
    public ClapInputEvents*  InEvents;
    public ClapOutputEvents* OutEvents;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ClapEventTransport
{
    public ClapEventHeader Header;
    public uint  Flags;
    public long  SongPosBeats;
    public long  SongPosSeconds;
    public double Tempo;
    public double TempoInc;
    public long  LoopStartBeats;
    public long  LoopEndBeats;
    public long  LoopStartSeconds;
    public long  LoopEndSeconds;
    public long  BarStart;
    public int   BarNumber;
    public short TimeSigNumerator;
    public short TimeSigDenominator;
}

// ─── Events ────────────────────────────────────────────────────

[StructLayout(LayoutKind.Sequential)]
public struct ClapEventHeader
{
    public uint   Size;
    public uint   Time;
    public ushort SpaceId;
    public ushort Type;
    public uint   Flags;
}

[StructLayout(LayoutKind.Sequential)]
public struct ClapEventNote
{
    public ClapEventHeader Header;
    public int    NoteId;     // -1 = wildcard
    public short  PortIndex;
    public short  Channel;    // -1 = wildcard
    public short  Key;        // -1 = wildcard
    public double Velocity;   // 0..1
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ClapEventParamValue
{
    public ClapEventHeader Header;
    public uint   ParamId;
    public void*  Cookie;
    public int    NoteId;
    public short  PortIndex;
    public short  Channel;
    public short  Key;
    public double Value;
}

[StructLayout(LayoutKind.Sequential)]
public struct ClapEventMidi
{
    public ClapEventHeader Header;
    public ushort PortIndex;
    public byte Data0;
    public byte Data1;
    public byte Data2;
}

// CLAP event types we care about (from clap/events.h)
public static class ClapEventType
{
    public const ushort NoteOn     = 0;
    public const ushort NoteOff    = 1;
    public const ushort NoteChoke  = 2;
    public const ushort NoteEnd    = 3;
    public const ushort NoteExpr   = 4;
    public const ushort ParamValue = 5;
    public const ushort ParamMod   = 6;
    public const ushort ParamGestureBegin = 7;
    public const ushort ParamGestureEnd   = 8;
    public const ushort Transport  = 9;
    public const ushort Midi       = 10;
    public const ushort MidiSysex  = 11;
    public const ushort Midi2      = 12;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ClapInputEvents
{
    public void* Ctx;
    public delegate* unmanaged[Cdecl]<ClapInputEvents*, uint>                       Size;
    public delegate* unmanaged[Cdecl]<ClapInputEvents*, uint, ClapEventHeader*>     Get;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ClapOutputEvents
{
    public void* Ctx;
    public delegate* unmanaged[Cdecl]<ClapOutputEvents*, ClapEventHeader*, bool>    TryPush;
}

// ─── Extensions ────────────────────────────────────────────────

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ClapPluginAudioPorts
{
    public delegate* unmanaged[Cdecl]<ClapPlugin*, bool, uint>                          Count;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint, bool, ClapAudioPortInfo*, bool> Get;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ClapAudioPortInfo
{
    public uint Id;
    public fixed byte Name[256];
    public uint Flags;
    public uint ChannelCount;
    public byte* PortType;
    public uint InPlacePairId;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ClapPluginNotePorts
{
    public delegate* unmanaged[Cdecl]<ClapPlugin*, bool, uint>                          Count;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint, bool, ClapNotePortInfo*, bool> Get;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ClapNotePortInfo
{
    public uint Id;
    public uint SupportedDialects;
    public uint PreferredDialect;
    public fixed byte Name[256];
}

public static class ClapNoteDialect
{
    public const uint Clap       = 1u << 0;
    public const uint Midi       = 1u << 1;
    public const uint MidiMpe    = 1u << 2;
    public const uint Midi2      = 1u << 3;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ClapPluginParams
{
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint>                                    Count;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint, ClapParamInfo*, bool>              GetInfo;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint, double*, bool>                     GetValue;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint, double, byte*, uint, bool>         ValueToText;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint, byte*, double*, bool>              TextToValue;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, ClapInputEvents*, ClapOutputEvents*, void> Flush;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ClapParamInfo
{
    public uint    Id;
    public uint    Flags;
    public void*   Cookie;
    public fixed byte Name[256];
    public fixed byte Module[1024];
    public double  MinValue;
    public double  MaxValue;
    public double  DefaultValue;
}

public static class ClapParamFlag
{
    public const uint IsStepped         = 1u << 0;
    public const uint IsPeriodic        = 1u << 1;
    public const uint IsHidden          = 1u << 2;
    public const uint IsReadonly        = 1u << 3;
    public const uint IsBypass          = 1u << 4;
    public const uint IsAutomatable     = 1u << 5;
    public const uint RequiresProcess   = 1u << 11;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ClapPluginState
{
    public delegate* unmanaged[Cdecl]<ClapPlugin*, ClapOStream*, bool> Save;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, ClapIStream*, bool> Load;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ClapIStream
{
    public void* Ctx;
    public delegate* unmanaged[Cdecl]<ClapIStream*, void*, ulong, long> Read;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ClapOStream
{
    public void* Ctx;
    public delegate* unmanaged[Cdecl]<ClapOStream*, void*, ulong, long> Write;
}

// ─── Factory ───────────────────────────────────────────────────

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ClapPluginFactory
{
    public delegate* unmanaged[Cdecl]<ClapPluginFactory*, uint>                                                  GetPluginCount;
    public delegate* unmanaged[Cdecl]<ClapPluginFactory*, uint, ClapPluginDescriptor*>                            GetPluginDescriptor;
    public delegate* unmanaged[Cdecl]<ClapPluginFactory*, void*, byte*, ClapPlugin*>                              CreatePlugin;
}

// CLAP standard extension IDs
public static class ClapExtensionId
{
    public const string AudioPorts = "clap.audio-ports";
    public const string NotePorts  = "clap.note-ports";
    public const string Params     = "clap.params";
    public const string State      = "clap.state";
}

// CLAP audio port types
public static class ClapAudioPortType
{
    public const string Stereo = "stereo";
    public const string Mono   = "mono";
}

// Process result codes
public static class ClapProcessResult
{
    public const int Error                = 0;
    public const int Continue             = 1;
    public const int ContinueIfNotQuiet   = 2;
    public const int Tail                 = 3;
    public const int Sleep                = 4;
}

// Audio port flags
public static class ClapAudioPortFlag
{
    public const uint IsMain         = 1u << 0;
    public const uint Supports64bits  = 1u << 1;
    public const uint Prefers64bits  = 1u << 2;
    public const uint RequiresCommonSampleSize = 1u << 3;
}

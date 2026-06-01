// Per-plugin instance: holds the SynthEngine, the parameter store, and the
// allocated ClapPlugin struct exposed to the host. Implements all the lifecycle
// callbacks (init, activate, process, destroy) and the extensions.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OttoSynth.Core;
using OttoSynth.Core.Midi;
using OttoSynth.Core.Plugin;
using OttoSynth.Core.Preset;
using P = OttoSynth.Core.Plugin.ParameterIds;

namespace OttoSynth.Plugin.Clap;

internal sealed unsafe class ClapPluginInstance : IDisposable
{
    // ─── Plugin state ──────────────────────────────────────────────

    private readonly SynthEngine _engine;
    private readonly PresetManager _presetManager;
    private readonly ClapParameterStore _params;

    private ClapPlugin* _plugin;
    private double[] _scratchLeft = Array.Empty<double>();
    private double[] _scratchRight = Array.Empty<double>();
    private bool _active;

    // Cached extension structs (pinned for lifetime of this instance).
    private GCHandle _audioPortsHandle, _notePortsHandle, _paramsHandle, _stateHandle;

    public ClapPluginInstance()
    {
        _engine = new SynthEngine(maxVoices: 16, maxBufferSize: 2048);
        _presetManager = new PresetManager();
        _params = new ClapParameterStore();
    }

    // ─── Native struct allocation ──────────────────────────────────

    public ClapPlugin* AllocatePluginStruct()
    {
        _plugin = (ClapPlugin*)NativeMemory.AllocZeroed((nuint)sizeof(ClapPlugin));
        // Descriptor pointer is set by the factory after creation; for simplicity
        // we leave it null here and let the host use the factory descriptor.
        _plugin->Init             = &Trampoline_Init;
        _plugin->Destroy          = &Trampoline_Destroy;
        _plugin->Activate         = &Trampoline_Activate;
        _plugin->Deactivate       = &Trampoline_Deactivate;
        _plugin->StartProcessing  = &Trampoline_StartProcessing;
        _plugin->StopProcessing   = &Trampoline_StopProcessing;
        _plugin->Reset            = &Trampoline_Reset;
        _plugin->Process          = &Trampoline_Process;
        _plugin->GetExtension     = &Trampoline_GetExtension;
        _plugin->OnMainThread     = &Trampoline_OnMainThread;
        return _plugin;
    }

    public void Dispose()
    {
        if (_audioPortsHandle.IsAllocated) _audioPortsHandle.Free();
        if (_notePortsHandle.IsAllocated)  _notePortsHandle.Free();
        if (_paramsHandle.IsAllocated)     _paramsHandle.Free();
        if (_stateHandle.IsAllocated)      _stateHandle.Free();
        if (_plugin != null)
        {
            NativeMemory.Free(_plugin);
            _plugin = null;
        }
    }

    // ─── Lifecycle ─────────────────────────────────────────────────

    private bool DoInit() => true;

    private void DoDestroy()
    {
        ClapEntry.UnregisterInstance(_plugin);
        Dispose();
    }

    private bool DoActivate(double sampleRate, uint minFramesCount, uint maxFramesCount)
    {
        _engine.Initialize(sampleRate, (int)maxFramesCount);
        int sz = Math.Max(2048, (int)maxFramesCount);
        if (_scratchLeft.Length < sz)
        {
            _scratchLeft  = new double[sz];
            _scratchRight = new double[sz];
        }
        _active = true;
        return true;
    }

    private void DoDeactivate() => _active = false;

    private int DoProcess(ClapProcess* process)
    {
        if (!_active || process == null) return ClapProcessResult.Sleep;

        uint frames = process->FramesCount;
        if (frames == 0) return ClapProcessResult.Continue;

        if (process->AudioOutputsCount < 1) return ClapProcessResult.Error;
        var outBuf = &process->AudioOutputs[0];
        if (outBuf->ChannelCount < 2) return ClapProcessResult.Error;

        // BPM
        if (process->Transport != null && process->Transport->Tempo > 0)
            _engine.Bpm = (int)Math.Round(process->Transport->Tempo);

        // Drain input events: split the buffer at each event's frame.
        uint eventCount = 0;
        if (process->InEvents != null)
            eventCount = process->InEvents->Size(process->InEvents);

        uint cursor = 0;
        uint eventIdx = 0;
        while (cursor < frames)
        {
            // Find next event at or after cursor; process up to that point.
            uint chunkEnd = frames;
            while (eventIdx < eventCount)
            {
                ClapEventHeader* hdr = process->InEvents->Get(process->InEvents, eventIdx);
                if (hdr == null) { eventIdx++; continue; }
                if (hdr->Time > cursor) { chunkEnd = hdr->Time; break; }
                ProcessEvent(hdr);
                eventIdx++;
            }
            if (chunkEnd > frames) chunkEnd = frames;
            uint chunkLen = chunkEnd - cursor;

            if (chunkLen > 0)
            {
                RenderChunk(outBuf, cursor, chunkLen);
                cursor += chunkLen;
            }
            else if (eventIdx >= eventCount)
            {
                break;
            }
        }

        return ClapProcessResult.Continue;
    }

    private void RenderChunk(ClapAudioBuffer* outBuf, uint offset, uint length)
    {
        // SynthEngine works in double[]; produce into the scratch buffer and copy
        // to the host's float[] output channels.
        if (_scratchLeft.Length < length)
        {
            _scratchLeft  = new double[length];
            _scratchRight = new double[length];
        }
        _engine.ProcessAudio(_scratchLeft, _scratchRight, (int)length);

        float* outL = outBuf->Data32[0];
        float* outR = outBuf->Data32[1];
        for (uint i = 0; i < length; i++)
        {
            outL[offset + i] = (float)_scratchLeft[i];
            outR[offset + i] = (float)_scratchRight[i];
        }
    }

    private void ProcessEvent(ClapEventHeader* hdr)
    {
        switch (hdr->Type)
        {
            case ClapEventType.NoteOn:
            {
                var n = (ClapEventNote*)hdr;
                int note = n->Key < 0 ? 60 : n->Key;
                int vel  = (int)Math.Round(Math.Clamp(n->Velocity, 0.0, 1.0) * 127);
                _engine.ProcessMidiEvent(MidiEvent.NoteOn((byte)note, (byte)vel));
                break;
            }
            case ClapEventType.NoteOff:
            {
                var n = (ClapEventNote*)hdr;
                int note = n->Key < 0 ? 60 : n->Key;
                _engine.ProcessMidiEvent(MidiEvent.NoteOff((byte)note, 0));
                break;
            }
            case ClapEventType.ParamValue:
            {
                var pe = (ClapEventParamValue*)hdr;
                int id = (int)pe->ParamId;
                _params.Set(id, pe->Value);
                ParameterDispatcher.Apply(id, pe->Value, _engine, _params);
                break;
            }
            case ClapEventType.Midi:
            {
                var mi = (ClapEventMidi*)hdr;
                byte status = mi->Data0;
                byte d1     = mi->Data1;
                byte d2     = mi->Data2;
                byte msg    = (byte)(status & 0xF0);
                switch (msg)
                {
                    case 0x90: // note on
                        if (d2 > 0) _engine.ProcessMidiEvent(MidiEvent.NoteOn(d1, d2));
                        else        _engine.ProcessMidiEvent(MidiEvent.NoteOff(d1, 0));
                        break;
                    case 0x80: // note off
                        _engine.ProcessMidiEvent(MidiEvent.NoteOff(d1, d2));
                        break;
                    case 0xB0: // CC
                        _engine.ProcessMidiEvent(MidiEvent.CC(d1, d2));
                        break;
                    case 0xE0: // pitch bend (14-bit: data2 = MSB, data1 = LSB)
                        _engine.ProcessMidiEvent(MidiEvent.PitchBend((d2 << 7) | d1));
                        break;
                }
                break;
            }
        }
    }

    // ─── Extension lookup ──────────────────────────────────────────

    private void* DoGetExtension(byte* extId)
    {
        if (ClapEntry.Utf8Equals(extId, ClapExtensionId.AudioPorts))
            return GetOrCreateAudioPortsExt();
        if (ClapEntry.Utf8Equals(extId, ClapExtensionId.NotePorts))
            return GetOrCreateNotePortsExt();
        if (ClapEntry.Utf8Equals(extId, ClapExtensionId.Params))
            return GetOrCreateParamsExt();
        if (ClapEntry.Utf8Equals(extId, ClapExtensionId.State))
            return GetOrCreateStateExt();
        return null;
    }

    // ─── audio_ports ───────────────────────────────────────────────

    private ClapPluginAudioPorts _audioPortsExt;
    private void* GetOrCreateAudioPortsExt()
    {
        if (_audioPortsHandle.IsAllocated)
            return Unsafe.AsPointer(ref _audioPortsExt);

        _audioPortsExt = new ClapPluginAudioPorts
        {
            Count = &Trampoline_AudioPorts_Count,
            Get   = &Trampoline_AudioPorts_Get
        };
        _audioPortsHandle = GCHandle.Alloc(this); // keep instance alive while host references the ext
        return Unsafe.AsPointer(ref _audioPortsExt);
    }

    private uint AudioPorts_Count(bool isInput) => isInput ? 0u : 1u;

    private bool AudioPorts_Get(uint index, bool isInput, ClapAudioPortInfo* info)
    {
        if (isInput || index != 0) return false;
        info->Id = 0;
        info->Flags = ClapAudioPortFlag.IsMain;
        info->ChannelCount = 2;
        info->PortType = (byte*)_stereoPortType;
        info->InPlacePairId = uint.MaxValue;
        WriteUtf8(info->Name, 256, "Stereo Out");
        return true;
    }

    // ─── note_ports ────────────────────────────────────────────────

    private ClapPluginNotePorts _notePortsExt;
    private void* GetOrCreateNotePortsExt()
    {
        if (_notePortsHandle.IsAllocated)
            return Unsafe.AsPointer(ref _notePortsExt);

        _notePortsExt = new ClapPluginNotePorts
        {
            Count = &Trampoline_NotePorts_Count,
            Get   = &Trampoline_NotePorts_Get
        };
        _notePortsHandle = GCHandle.Alloc(this);
        return Unsafe.AsPointer(ref _notePortsExt);
    }

    private uint NotePorts_Count(bool isInput) => isInput ? 1u : 0u;

    private bool NotePorts_Get(uint index, bool isInput, ClapNotePortInfo* info)
    {
        if (!isInput || index != 0) return false;
        info->Id = 0;
        info->SupportedDialects = ClapNoteDialect.Clap | ClapNoteDialect.Midi;
        info->PreferredDialect  = ClapNoteDialect.Clap;
        WriteUtf8(info->Name, 256, "Notes");
        return true;
    }

    // ─── params ────────────────────────────────────────────────────

    private ClapPluginParams _paramsExt;
    private void* GetOrCreateParamsExt()
    {
        if (_paramsHandle.IsAllocated)
            return Unsafe.AsPointer(ref _paramsExt);

        _paramsExt = new ClapPluginParams
        {
            Count       = &Trampoline_Params_Count,
            GetInfo     = &Trampoline_Params_GetInfo,
            GetValue    = &Trampoline_Params_GetValue,
            ValueToText = &Trampoline_Params_ValueToText,
            TextToValue = &Trampoline_Params_TextToValue,
            Flush       = &Trampoline_Params_Flush
        };
        _paramsHandle = GCHandle.Alloc(this);
        return Unsafe.AsPointer(ref _paramsExt);
    }

    // ─── state ─────────────────────────────────────────────────────

    private ClapPluginState _stateExt;
    private void* GetOrCreateStateExt()
    {
        if (_stateHandle.IsAllocated)
            return Unsafe.AsPointer(ref _stateExt);

        _stateExt = new ClapPluginState
        {
            Save = &Trampoline_State_Save,
            Load = &Trampoline_State_Load
        };
        _stateHandle = GCHandle.Alloc(this);
        return Unsafe.AsPointer(ref _stateExt);
    }

    private bool State_Save(ClapOStream* ostream)
    {
        try
        {
            var preset = _presetManager.Capture(_engine, "Plugin State");
            string json = _presetManager.ToJson(preset);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            fixed (byte* p = bytes)
            {
                long remaining = bytes.Length;
                byte* cur = p;
                while (remaining > 0)
                {
                    long written = ostream->Write(ostream, cur, (ulong)remaining);
                    if (written <= 0) return false;
                    cur += written;
                    remaining -= written;
                }
            }
            return true;
        }
        catch { return false; }
    }

    private bool State_Load(ClapIStream* istream)
    {
        try
        {
            using var ms = new MemoryStream();
            byte[] buf = new byte[4096];
            fixed (byte* p = buf)
            {
                while (true)
                {
                    long read = istream->Read(istream, p, (ulong)buf.Length);
                    if (read <= 0) break;
                    ms.Write(buf, 0, (int)read);
                }
            }
            string json = Encoding.UTF8.GetString(ms.ToArray());
            var preset = _presetManager.LoadFromJson(json);
            _presetManager.Apply(preset, _engine);
            ParameterDispatcher.CaptureAll(_engine, _params);
            return true;
        }
        catch { return false; }
    }

    // ─── Utilities ─────────────────────────────────────────────────

    // Pre-allocated stereo port type string ("stereo\0"), pinned for the DLL.
    private static byte* _stereoPortType = AllocAnsiStatic("stereo");

    private static byte* AllocAnsiStatic(string s)
    {
        byte[] bytes = new byte[Encoding.UTF8.GetByteCount(s) + 1];
        Encoding.UTF8.GetBytes(s, bytes);
        bytes[^1] = 0;
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        // Intentionally never freed — string lives for DLL lifetime.
        return (byte*)handle.AddrOfPinnedObject();
    }

    private static void WriteUtf8(byte* dst, int max, string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        int n = Math.Min(bytes.Length, max - 1);
        for (int i = 0; i < n; i++) dst[i] = bytes[i];
        dst[n] = 0;
    }

    // ─── Trampolines (unmanaged → managed dispatch via the registry) ──────

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static bool Trampoline_Init(ClapPlugin* p)
        => ClapEntry.GetInstance(p)?.DoInit() ?? false;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void Trampoline_Destroy(ClapPlugin* p)
        => ClapEntry.GetInstance(p)?.DoDestroy();

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static bool Trampoline_Activate(ClapPlugin* p, double sr, uint min, uint max)
        => ClapEntry.GetInstance(p)?.DoActivate(sr, min, max) ?? false;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void Trampoline_Deactivate(ClapPlugin* p)
        => ClapEntry.GetInstance(p)?.DoDeactivate();

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static bool Trampoline_StartProcessing(ClapPlugin* p) => true;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void Trampoline_StopProcessing(ClapPlugin* p) { }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void Trampoline_Reset(ClapPlugin* p)
    {
        var inst = ClapEntry.GetInstance(p);
        if (inst != null) inst._engine.AllNotesOff();
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Trampoline_Process(ClapPlugin* p, ClapProcess* process)
        => ClapEntry.GetInstance(p)?.DoProcess(process) ?? ClapProcessResult.Error;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void* Trampoline_GetExtension(ClapPlugin* p, byte* extId)
    {
        var inst = ClapEntry.GetInstance(p);
        return inst == null ? null : inst.DoGetExtension(extId);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void Trampoline_OnMainThread(ClapPlugin* p) { }

    // Audio ports trampolines
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static uint Trampoline_AudioPorts_Count(ClapPlugin* p, bool isInput)
        => ClapEntry.GetInstance(p)?.AudioPorts_Count(isInput) ?? 0;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static bool Trampoline_AudioPorts_Get(ClapPlugin* p, uint i, bool isIn, ClapAudioPortInfo* info)
        => ClapEntry.GetInstance(p)?.AudioPorts_Get(i, isIn, info) ?? false;

    // Note ports trampolines
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static uint Trampoline_NotePorts_Count(ClapPlugin* p, bool isInput)
        => ClapEntry.GetInstance(p)?.NotePorts_Count(isInput) ?? 0;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static bool Trampoline_NotePorts_Get(ClapPlugin* p, uint i, bool isIn, ClapNotePortInfo* info)
        => ClapEntry.GetInstance(p)?.NotePorts_Get(i, isIn, info) ?? false;

    // Params trampolines
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static uint Trampoline_Params_Count(ClapPlugin* p)
        => (uint)(ClapEntry.GetInstance(p)?._params.Count ?? 0);

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static bool Trampoline_Params_GetInfo(ClapPlugin* p, uint index, ClapParamInfo* info)
        => ClapEntry.GetInstance(p)?._params.GetInfo(index, info) ?? false;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static bool Trampoline_Params_GetValue(ClapPlugin* p, uint paramId, double* outValue)
    {
        var inst = ClapEntry.GetInstance(p);
        if (inst == null) return false;
        *outValue = inst._params.GetParameterValue((int)paramId);
        return true;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static bool Trampoline_Params_ValueToText(ClapPlugin* p, uint paramId, double value, byte* buf, uint cap)
    {
        if (buf == null || cap == 0) return false;
        string s = value.ToString("G6", System.Globalization.CultureInfo.InvariantCulture);
        WriteUtf8(buf, (int)cap, s);
        return true;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static bool Trampoline_Params_TextToValue(ClapPlugin* p, uint paramId, byte* text, double* outValue)
    {
        string s = ClapEntry.Utf8String(text);
        if (double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double v))
        {
            *outValue = v;
            return true;
        }
        return false;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void Trampoline_Params_Flush(ClapPlugin* p, ClapInputEvents* inEvents, ClapOutputEvents* outEvents)
    {
        var inst = ClapEntry.GetInstance(p);
        if (inst == null || inEvents == null) return;

        uint count = inEvents->Size(inEvents);
        for (uint i = 0; i < count; i++)
        {
            ClapEventHeader* h = inEvents->Get(inEvents, i);
            if (h != null && h->Type == ClapEventType.ParamValue)
            {
                var pe = (ClapEventParamValue*)h;
                int id = (int)pe->ParamId;
                inst._params.Set(id, pe->Value);
                ParameterDispatcher.Apply(id, pe->Value, inst._engine, inst._params);
            }
        }
    }

    // State trampolines
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static bool Trampoline_State_Save(ClapPlugin* p, ClapOStream* ostream)
        => ClapEntry.GetInstance(p)?.State_Save(ostream) ?? false;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static bool Trampoline_State_Load(ClapPlugin* p, ClapIStream* istream)
        => ClapEntry.GetInstance(p)?.State_Load(istream) ?? false;
}

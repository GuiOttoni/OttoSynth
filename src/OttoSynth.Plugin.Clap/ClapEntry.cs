// Managed CLAP entry points. The native shim (clap_shim.c) defines the global
// `clap_entry` symbol and forwards init/deinit/get_factory to these methods.
//
// All exports use [UnmanagedCallersOnly] with explicit EntryPoint names that
// match the extern declarations in clap_shim.c.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace OttoSynth.Plugin.Clap;

public static unsafe class ClapEntry
{
    // ─── Plugin metadata (UTF-8 byte arrays, pinned for lifetime of the DLL) ──

    private static byte[]? _pluginId;
    private static byte[]? _pluginName;
    private static byte[]? _vendor;
    private static byte[]? _url;
    private static byte[]? _version;
    private static byte[]? _description;
    private static byte[]? _feature1;
    private static byte[]? _feature2;
    private static byte[]? _feature3;

    private static GCHandle _idHandle, _nameHandle, _vendorHandle, _urlHandle,
                            _versionHandle, _descriptionHandle,
                            _feature1Handle, _feature2Handle, _feature3Handle;

    // Pre-allocated descriptor (single plugin per shared lib).
    private static ClapPluginDescriptor _descriptor;
    private static byte** _featuresArray; // pointer to array of byte* (null-terminated)

    private static ClapPluginFactory _factory;

    // Each ClapPlugin* we hand out is allocated separately; we keep a registry
    // so we can find the managed ClapPluginInstance from its native pointer.
    private static readonly object _instancesLock = new();
    private static readonly Dictionary<IntPtr, ClapPluginInstance> _instances = new();

    // ─── DLL Lifecycle ─────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "OttoClap_Init", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static int Init(byte* pluginPath)
    {
        try
        {
            InitializeDescriptor();
            InitializeFactory();
            return 1; // true
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "OttoClap_Deinit", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static void Deinit()
    {
        lock (_instancesLock)
        {
            foreach (var inst in _instances.Values) inst.Dispose();
            _instances.Clear();
        }

        FreePinned(ref _idHandle);
        FreePinned(ref _nameHandle);
        FreePinned(ref _vendorHandle);
        FreePinned(ref _urlHandle);
        FreePinned(ref _versionHandle);
        FreePinned(ref _descriptionHandle);
        FreePinned(ref _feature1Handle);
        FreePinned(ref _feature2Handle);
        FreePinned(ref _feature3Handle);

        if (_featuresArray != null)
        {
            NativeMemory.Free(_featuresArray);
            _featuresArray = null;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "OttoClap_GetFactory", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static void* GetFactory(byte* factoryId)
    {
        if (factoryId == null) return null;
        // Compare against "clap.plugin-factory"
        if (Utf8Equals(factoryId, "clap.plugin-factory"))
        {
            fixed (ClapPluginFactory* p = &_factory) return p;
        }
        return null;
    }

    // ─── Descriptor setup ──────────────────────────────────────────

    private static void InitializeDescriptor()
    {
        _pluginId    = Utf8Bytes("io.ottosound.ottosynth");
        _pluginName  = Utf8Bytes("OttoSynth");
        _vendor      = Utf8Bytes("OttoSound");
        _url         = Utf8Bytes("https://ottosound.io");
        _version     = Utf8Bytes("1.1.0");
        _description = Utf8Bytes("Wavetable synthesizer with modulation matrix and effects");
        _feature1    = Utf8Bytes("instrument");
        _feature2    = Utf8Bytes("synthesizer");
        _feature3    = Utf8Bytes("stereo");

        _idHandle          = GCHandle.Alloc(_pluginId,    GCHandleType.Pinned);
        _nameHandle        = GCHandle.Alloc(_pluginName,  GCHandleType.Pinned);
        _vendorHandle      = GCHandle.Alloc(_vendor,      GCHandleType.Pinned);
        _urlHandle         = GCHandle.Alloc(_url,         GCHandleType.Pinned);
        _versionHandle     = GCHandle.Alloc(_version,     GCHandleType.Pinned);
        _descriptionHandle = GCHandle.Alloc(_description, GCHandleType.Pinned);
        _feature1Handle    = GCHandle.Alloc(_feature1,    GCHandleType.Pinned);
        _feature2Handle    = GCHandle.Alloc(_feature2,    GCHandleType.Pinned);
        _feature3Handle    = GCHandle.Alloc(_feature3,    GCHandleType.Pinned);

        // Features: null-terminated array of 4 pointers.
        _featuresArray = (byte**)NativeMemory.Alloc((nuint)(IntPtr.Size * 4));
        _featuresArray[0] = (byte*)_feature1Handle.AddrOfPinnedObject();
        _featuresArray[1] = (byte*)_feature2Handle.AddrOfPinnedObject();
        _featuresArray[2] = (byte*)_feature3Handle.AddrOfPinnedObject();
        _featuresArray[3] = null;

        _descriptor = new ClapPluginDescriptor
        {
            ClapVersion = new ClapVersion { Major = 1, Minor = 2, Revision = 0 },
            Id          = (byte*)_idHandle.AddrOfPinnedObject(),
            Name        = (byte*)_nameHandle.AddrOfPinnedObject(),
            Vendor      = (byte*)_vendorHandle.AddrOfPinnedObject(),
            Url         = (byte*)_urlHandle.AddrOfPinnedObject(),
            ManualUrl   = (byte*)_urlHandle.AddrOfPinnedObject(),
            SupportUrl  = (byte*)_urlHandle.AddrOfPinnedObject(),
            Version     = (byte*)_versionHandle.AddrOfPinnedObject(),
            Description = (byte*)_descriptionHandle.AddrOfPinnedObject(),
            Features    = _featuresArray
        };
    }

    private static void InitializeFactory()
    {
        _factory = new ClapPluginFactory
        {
            GetPluginCount      = &Factory_GetPluginCount,
            GetPluginDescriptor = &Factory_GetPluginDescriptor,
            CreatePlugin        = &Factory_CreatePlugin
        };
    }

    // ─── Factory callbacks ─────────────────────────────────────────

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static uint Factory_GetPluginCount(ClapPluginFactory* factory) => 1;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static ClapPluginDescriptor* Factory_GetPluginDescriptor(ClapPluginFactory* factory, uint index)
    {
        if (index != 0) return null;
        fixed (ClapPluginDescriptor* d = &_descriptor) return d;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static ClapPlugin* Factory_CreatePlugin(ClapPluginFactory* factory, void* host, byte* pluginId)
    {
        // Validate ID.
        if (!Utf8Equals(pluginId, "io.ottosound.ottosynth")) return null;

        try
        {
            var instance = new ClapPluginInstance();
            ClapPlugin* plugin = instance.AllocatePluginStruct();
            lock (_instancesLock) _instances[(IntPtr)plugin] = instance;
            return plugin;
        }
        catch
        {
            return null;
        }
    }

    // ─── Instance registry — called from ClapPluginInstance.Destroy ──

    internal static void UnregisterInstance(ClapPlugin* plugin)
    {
        lock (_instancesLock) _instances.Remove((IntPtr)plugin);
    }

    internal static ClapPluginInstance? GetInstance(ClapPlugin* plugin)
    {
        lock (_instancesLock)
        {
            _instances.TryGetValue((IntPtr)plugin, out var inst);
            return inst;
        }
    }

    // ─── Utilities ─────────────────────────────────────────────────

    private static byte[] Utf8Bytes(string s)
    {
        int len = Encoding.UTF8.GetByteCount(s);
        var bytes = new byte[len + 1];
        Encoding.UTF8.GetBytes(s, bytes);
        bytes[len] = 0;
        return bytes;
    }

    private static void FreePinned(ref GCHandle handle)
    {
        if (handle.IsAllocated) handle.Free();
    }

    internal static bool Utf8Equals(byte* utf8, string s)
    {
        if (utf8 == null) return false;
        var bytes = Encoding.UTF8.GetBytes(s);
        for (int i = 0; i < bytes.Length; i++)
        {
            if (utf8[i] != bytes[i]) return false;
        }
        return utf8[bytes.Length] == 0;
    }

    internal static int Utf8Length(byte* s)
    {
        if (s == null) return 0;
        int n = 0;
        while (s[n] != 0) n++;
        return n;
    }

    internal static string Utf8String(byte* s)
    {
        int len = Utf8Length(s);
        return Encoding.UTF8.GetString(s, len);
    }
}

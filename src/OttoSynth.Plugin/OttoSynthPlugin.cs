using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using AudioPlugSharp;
using AudioPlugSharpWPF;
using OttoSynth.Core;
using OttoSynth.Core.Midi;
using OttoSynth.Core.Plugin;
using OttoSynth.Core.Preset;

namespace OttoSynth.Plugin;

/// <summary>
/// AudioPlugSharp VST3 entry point for OttoSynth.
/// Exposes all synth parameters via ParameterMap, supports state chunks, and syncs host BPM.
/// </summary>
public class OttoSynthPlugin : AudioPluginWPF
{
    private readonly SynthEngine _engine;
    private readonly PresetManager _presetManager;
    private Dictionary<int, AudioPluginParameter> _paramsById = new();
    private Vst3PluginHost? _hostAdapter;

    public SynthEngine Engine => _engine;

    public OttoSynthPlugin()
    {
        _engine        = new SynthEngine(maxVoices: 16, maxBufferSize: 2048);
        _presetManager = new PresetManager();

        Company          = "OttoSound";
        Website          = "https://ottosound.io";
        Contact          = "contact@ottosound.io";
        PluginName       = "OttoSynth";
        PluginCategory   = "Instrument|Synth";
        PluginVersion    = "1.2.1";
        PluginID         = 0x0A7C8ED1C2464B7AUL;
        HasUserInterface = true;
        EditorWidth      = 1280;
        EditorHeight     = 820;

        // Declare supported sample formats. Without this, some hosts (Ableton in 32-bit
        // float mode in particular) do not negotiate audio I/O and the plugin is silent.
        SampleFormatsSupported = EAudioBitsPerSample.Bits32 | EAudioBitsPerSample.Bits64;

        PluginLogger.Log("Constructor",
            $"Company={Company} Name={PluginName} Version={PluginVersion} PluginID=0x{PluginID:X16}");
    }

    // ─── WPF Application bootstrap ────────────────────────────────
    // DAW host processes have no WPF Application — Window.Show() crashes without one.
    // We spin a dedicated STA background thread and wait for its dispatcher loop to
    // start before returning, then marshal all WPF operations to that dispatcher.
    //
    // Critical invariant: managed exceptions must NEVER propagate out of any plugin
    // callback into the native C++/CLI bridge. That crossing causes KERNELBASE.dll
    // crashes in Cubase, Pro Tools and other hosts. Every public override must catch
    // all exceptions and log them instead of rethrowing.

    private static Thread? _wpfThread;
    private static readonly ManualResetEventSlim _wpfAppReady = new(false);
    private static readonly object _wpfLock = new();

    // Returns true if a healthy WPF dispatcher is available after the call.
    private static bool EnsureWpfApplication()
    {
        try
        {
            int callerTid = Thread.CurrentThread.ManagedThreadId;

            // Fast path: dispatcher is alive.
            var current = Application.Current;
            if (current != null && !current.Dispatcher.HasShutdownStarted)
                return true;

            lock (_wpfLock)
            {
                current = Application.Current;
                if (current != null && !current.Dispatcher.HasShutdownStarted)
                    return true;

                if (current == null)
                    PluginLogger.Log("EnsureWpfApplication",
                        $"Application.Current is null — spinning new STA thread (callerTID={callerTid})");
                else
                    PluginLogger.LogWarn("EnsureWpfApplication",
                        $"Dispatcher HasShutdownStarted=true — respawning (callerTID={callerTid})");

                // Reset the event so the Wait below blocks until the new thread signals.
                _wpfAppReady.Reset();

                _wpfThread = new Thread(() =>
                {
                    int wpfTid = Thread.CurrentThread.ManagedThreadId;
                    PluginLogger.Log("EnsureWpfApplication", $"WPF STA thread started (TID={wpfTid})");

                    var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

                    // Defer theme load until the dispatcher is pumping. Pack URIs
                    // (/Assembly;component/path) need WPF's pack scheme fully wired up,
                    // which only happens reliably once Application.Run has begun.
                    app.Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            PluginLogger.Log("EnsureWpfApplication",
                                $"Dispatcher pumping — loading theme (TID={Thread.CurrentThread.ManagedThreadId})");
                            app.Resources.MergedDictionaries.Add(new ResourceDictionary
                            {
                                Source = new Uri("pack://application:,,,/OttoSynth.UI;component/Themes/ThemeMatrix.xaml",
                                                 UriKind.Absolute)
                            });
                            PluginLogger.Log("EnsureWpfApplication", "Theme loaded successfully");
                        }
                        catch (Exception ex)
                        {
                            PluginLogger.LogException("LoadAppTheme", ex);
                        }
                        _wpfAppReady.Set();
                        PluginLogger.Log("EnsureWpfApplication",
                            $"_wpfAppReady signalled (TID={Thread.CurrentThread.ManagedThreadId})");
                    });
                    app.Run();
                    PluginLogger.LogWarn("EnsureWpfApplication",
                        $"WPF STA thread exiting app.Run() (TID={wpfTid})");
                });
                _wpfThread.SetApartmentState(ApartmentState.STA);
                _wpfThread.IsBackground = true;
                _wpfThread.Name = "OttoSynth WPF";
                _wpfThread.Start();

                PluginLogger.Log("EnsureWpfApplication",
                    $"Waiting for WPF dispatcher (timeout=15 s, callerTID={callerTid})");
                bool signalled = _wpfAppReady.Wait(15_000);
                if (!signalled)
                {
                    PluginLogger.LogWarn("EnsureWpfApplication",
                        "WPF thread did not signal ready within 15 s — aborting UI init");
                    return false;
                }

                current = Application.Current;
                if (current == null)
                {
                    PluginLogger.LogWarn("EnsureWpfApplication",
                        "Application.Current is null after wait — aborting");
                    return false;
                }

                bool ok = !current.Dispatcher.HasShutdownStarted;
                if (ok)
                    PluginLogger.Log("EnsureWpfApplication",
                        $"WPF Application ready (callerTID={callerTid}) — success");
                else
                    PluginLogger.LogWarn("EnsureWpfApplication",
                        "Dispatcher HasShutdownStarted=true after wait — returning false");
                return ok;
            }
        }
        catch (Exception ex)
        {
            PluginLogger.LogException("EnsureWpfApplication", ex);
            return false;
        }
    }

    public override void ShowEditor(IntPtr parentWindow)
    {
        PluginLogger.Log("ShowEditor", $"Entry — parentWindow=0x{parentWindow:X}");
        try
        {
            if (!EnsureWpfApplication())
            {
                PluginLogger.LogWarn("ShowEditor", "EnsureWpfApplication returned false — aborting");
                return;
            }
            Application.Current!.Dispatcher.Invoke(() => base.ShowEditor(parentWindow));
            PluginLogger.Log("ShowEditor", "Exit — OK");
        }
        catch (Exception ex)
        {
            PluginLogger.LogException("ShowEditor", ex);
            // Never rethrow — managed exception crossing native boundary = host crash.
        }
    }

    public override void HideEditor()
    {
        PluginLogger.Log("HideEditor", "Entry");
        try
        {
            var app = Application.Current;
            if (app != null && !app.Dispatcher.HasShutdownStarted)
                app.Dispatcher.Invoke(() => { EditorWindow?.Close(); base.HideEditor(); });
            else
                base.HideEditor();
            PluginLogger.Log("HideEditor", "Exit — OK");
        }
        catch (Exception ex)
        {
            PluginLogger.LogException("HideEditor", ex);
            try { base.HideEditor(); } catch { }
        }
    }

    // ─── AudioPluginWPF ───────────────────────────────────────────

    // AudioPlugSharp calls GetEditorView() on the host thread before ShowEditor().
    // We must ensure the UserControl is created on the WPF STA thread so that its
    // Dispatcher matches the thread that will later show and update it.
    public override System.Windows.Controls.UserControl GetEditorView()
    {
        PluginLogger.Log("GetEditorView", "Entry");
        try
        {
            if (!EnsureWpfApplication())
            {
                PluginLogger.LogWarn("GetEditorView", "EnsureWpfApplication returned false — returning empty UserControl");
                return new System.Windows.Controls.UserControl();
            }
            var view = Application.Current!.Dispatcher.Invoke(() => new PluginEditorView(_engine));
            PluginLogger.Log("GetEditorView", $"Exit — returning {view.GetType().FullName}");
            return view;
        }
        catch (Exception ex)
        {
            PluginLogger.LogException("GetEditorView", ex);
            return new System.Windows.Controls.UserControl();
        }
    }

    // ─── Initialization ───────────────────────────────────────────

    public override void Initialize()
    {
        PluginLogger.Log("Initialize", "Entry");
        try
        {
            // Log host process so we know which DAW loaded us.
            try
            {
                var proc = System.Diagnostics.Process.GetCurrentProcess();
                PluginLogger.Log("Initialize", $"Host process: {proc.ProcessName} (PID {proc.Id})");
            }
            catch (Exception ex)
            {
                PluginLogger.LogWarn("Initialize", $"Could not read host process info: {ex.Message}");
            }

            base.Initialize();

            InputPorts  = Array.Empty<AudioIOPort>();
            OutputPorts = new AudioIOPort[]
            {
                new AudioIOPortManaged("Stereo Output", EAudioChannelConfiguration.Stereo)
            };

            ParameterMap.RegisterAll(this);

            _paramsById = Parameters
                .Where(p => int.TryParse(p.ID, out _))
                .ToDictionary(p => int.Parse(p.ID));
            _hostAdapter = new Vst3PluginHost(_paramsById);

            PluginLogger.Log("Initialize", $"Exit — parameters registered: {_paramsById.Count}");
        }
        catch (Exception ex)
        {
            PluginLogger.LogException("Initialize", ex);
        }
    }

    public override void InitializeProcessing()
    {
        try
        {
            base.InitializeProcessing();
            double sr      = Host?.SampleRate ?? 0;
            uint   bufSize = Host?.MaxAudioBufferSize ?? 0;
            PluginLogger.Log("InitializeProcessing", $"SampleRate={sr} Hz  BufferSize={bufSize}");
            if (sr <= 0 || bufSize == 0)
            {
                PluginLogger.LogWarn("InitializeProcessing",
                    $"Invalid host values (SampleRate={sr}, MaxAudioBufferSize={bufSize}) — skipping engine init");
                return;
            }
            _engine.Initialize(sr, (int)bufSize);
        }
        catch (Exception ex)
        {
            PluginLogger.LogException("InitializeProcessing", ex);
        }
    }

    public override void SetMaxAudioBufferSize(uint maxSamples, EAudioBitsPerSample bitsPerSample)
    {
        try
        {
            base.SetMaxAudioBufferSize(maxSamples, bitsPerSample);
            double sr = Host?.SampleRate ?? 0;
            PluginLogger.Log("SetMaxAudioBufferSize", $"maxSamples={maxSamples}  bitsPerSample={bitsPerSample}  sr={sr}");
            if (sr <= 0 || maxSamples == 0)
            {
                PluginLogger.LogWarn("SetMaxAudioBufferSize",
                    $"Invalid values (SampleRate={sr}, maxSamples={maxSamples}) — skipping engine init");
                return;
            }
            _engine.Initialize(sr, (int)maxSamples);
        }
        catch (Exception ex)
        {
            PluginLogger.LogException("SetMaxAudioBufferSize", ex);
        }
    }

    // ─── State chunk ──────────────────────────────────────────────

    public override byte[] SaveState()
    {
        try
        {
            var preset = _presetManager.Capture(_engine, "Plugin State");
            var bytes  = Encoding.UTF8.GetBytes(_presetManager.ToJson(preset));
            PluginLogger.Log("SaveState", $"Saved {bytes.Length} bytes");
            return bytes;
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    public override void RestoreState(byte[] stateData)
    {
        PluginLogger.Log("RestoreState", $"Received {stateData?.Length ?? 0} bytes");
        try
        {
            if (_hostAdapter == null) return;
            if (stateData == null || stateData.Length == 0) return;
            var json   = Encoding.UTF8.GetString(stateData);
            var preset = _presetManager.LoadFromJson(json);
            _presetManager.Apply(preset, _engine);
            ParameterDispatcher.CaptureAll(_engine, _hostAdapter);
        }
        catch { /* corrupt state — leave defaults */ }
    }

    // ─── MIDI ─────────────────────────────────────────────────────

    public override void HandleNoteOn(int channel, int noteNumber, float velocity, int sampleOffset)
    {
        try { _engine.ProcessMidiEvent(MidiEvent.NoteOn((byte)noteNumber, (byte)(velocity * 127f))); }
        catch (Exception ex) { PluginLogger.LogException("HandleNoteOn", ex); }
    }

    public override void HandleNoteOff(int channel, int noteNumber, float velocity, int sampleOffset)
    {
        try { _engine.ProcessMidiEvent(MidiEvent.NoteOff((byte)noteNumber, (byte)(velocity * 127f))); }
        catch (Exception ex) { PluginLogger.LogException("HandleNoteOff", ex); }
    }

    // ─── Automation ───────────────────────────────────────────────

    public override void HandleParameterChange(AudioPluginParameter parameter, double newValue, int sampleOffset)
    {
        // Do NOT log every parameter change — called per-sample by some hosts.
        try
        {
            if (_hostAdapter == null) return;
            base.HandleParameterChange(parameter, newValue, sampleOffset);
            if (int.TryParse(parameter.ID, out int id))
                ParameterDispatcher.Apply(id, newValue, _engine, _hostAdapter);
        }
        catch (Exception ex)
        {
            PluginLogger.LogException("HandleParameterChange", ex);
        }
    }

    // ─── Audio processing ─────────────────────────────────────────

    private static int _processCallCount;

    public override void Process()
    {
        try
        {
            // _hostAdapter is null if Initialize() failed — nothing we can do.
            if (_hostAdapter == null) return;

            base.Process();

            // Drain all pending VST3 events (parameter changes + MIDI). Without this,
            // events queued by the host between Process calls never reach our handlers
            // and the engine stays at defaults — silent. Guard against hosts that do not
            // implement ProcessAllEvents (null reference).
            Host?.ProcessAllEvents();

            // Guard against OutputPorts being null or empty if Initialize() failed silently.
            if (OutputPorts == null || OutputPorts.Length == 0) return;

            // Sync host BPM
            if (Host is { IsPlaying: true, BPM: > 0 } host)
                _engine.Bpm = (int)Math.Round(host.BPM);

            var output = OutputPorts[0] as AudioIOPortManaged;
            if (output == null) return;

            var buffers = output.GetAudioBuffers();
            if (buffers == null || buffers.Length < 2) return;
            if (buffers[0] == null || buffers[1] == null) return;

            // Use the buffer's actual length as the sample count. This is the canonical
            // AudioPlugSharp pattern — Host.CurrentAudioBufferSize is unreliable
            // (sometimes 0 during start-up) but the Span/array Length is always correct
            // and matches what PostProcess → WriteData will copy back to the host.
            int sampleCount = buffers[0].Length;
            if (buffers[1].Length < sampleCount) sampleCount = buffers[1].Length;
            if (sampleCount <= 0) return;

            _engine.ProcessAudio(buffers[0], buffers[1], sampleCount);

            // Periodic heartbeat log (every 1000 calls ≈ every 5–20 s depending on buffer size).
            int callCount = Interlocked.Increment(ref _processCallCount);
            if (callCount % 1000 == 0)
                PluginLogger.Log("Process", $"Call #{callCount}  IsPlaying={Host?.IsPlaying}  BPM={Host?.BPM:F1}");
        }
        catch (Exception ex)
        {
            PluginLogger.LogException("Process", ex);
        }
    }
}

/// <summary>Convenience subclass with sane default ValueFormat.</summary>
internal sealed class OttoParameter : AudioPluginParameter
{
    public OttoParameter()
    {
        ValueFormat = "F2";
    }
}

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
    private Vst3PluginHost _hostAdapter = null!;

    public SynthEngine Engine => _engine;

    public OttoSynthPlugin()
    {
        _engine        = new SynthEngine(maxVoices: 16, maxBufferSize: 2048);
        _presetManager = new PresetManager();

        Company         = "OttoSound";
        Website         = "https://ottosound.io";
        Contact         = "contact@ottosound.io";
        PluginName      = "OttoSynth";
        PluginCategory  = "Instrument|Synth";
        PluginVersion   = "1.2.1";
        PluginID        = 0x0A7C8ED1C2464B7AUL;
        HasUserInterface = true;
        EditorWidth     = 1280;
        EditorHeight    = 820;

        // Declare supported sample formats. Without this, some hosts (Ableton in 32-bit
        // float mode in particular) do not negotiate audio I/O and the plugin is silent.
        SampleFormatsSupported = EAudioBitsPerSample.Bits32 | EAudioBitsPerSample.Bits64;
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
            // Fast path: dispatcher is alive.
            var current = Application.Current;
            if (current != null && !current.Dispatcher.HasShutdownStarted)
                return true;

            lock (_wpfLock)
            {
                current = Application.Current;
                if (current != null && !current.Dispatcher.HasShutdownStarted)
                    return true;

                // Reset the event so the Wait below blocks until the new thread signals.
                _wpfAppReady.Reset();

                _wpfThread = new Thread(() =>
                {
                    var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

                    // Defer theme load until the dispatcher is pumping. Pack URIs
                    // (/Assembly;component/path) need WPF's pack scheme fully wired up,
                    // which only happens reliably once Application.Run has begun.
                    app.Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            app.Resources.MergedDictionaries.Add(new ResourceDictionary
                            {
                                Source = new Uri("pack://application:,,,/OttoSynth.UI;component/Themes/ThemeMatrix.xaml",
                                                 UriKind.Absolute)
                            });
                        }
                        catch (Exception ex)
                        {
                            LogException("LoadAppTheme", ex);
                        }
                        _wpfAppReady.Set();
                    });
                    app.Run();
                });
                _wpfThread.SetApartmentState(ApartmentState.STA);
                _wpfThread.IsBackground = true;
                _wpfThread.Name = "OttoSynth WPF";
                _wpfThread.Start();
                _wpfAppReady.Wait(10_000);

                current = Application.Current;
                return current != null && !current.Dispatcher.HasShutdownStarted;
            }
        }
        catch (Exception ex)
        {
            LogException("EnsureWpfApplication", ex);
            return false;
        }
    }

    public override void ShowEditor(IntPtr parentWindow)
    {
        try
        {
            if (!EnsureWpfApplication()) return;
            Application.Current!.Dispatcher.Invoke(() => base.ShowEditor(parentWindow));
        }
        catch (Exception ex)
        {
            LogException("ShowEditor", ex);
            // Never rethrow — managed exception crossing native boundary = host crash.
        }
    }

    public override void HideEditor()
    {
        try
        {
            var app = Application.Current;
            if (app != null && !app.Dispatcher.HasShutdownStarted)
                app.Dispatcher.Invoke(() => { EditorWindow?.Close(); base.HideEditor(); });
            else
                base.HideEditor();
        }
        catch (Exception ex)
        {
            LogException("HideEditor", ex);
            try { base.HideEditor(); } catch { }
        }
    }

    // ─── AudioPluginWPF ───────────────────────────────────────────

    // AudioPlugSharp calls GetEditorView() on the host thread before ShowEditor().
    // We must ensure the UserControl is created on the WPF STA thread so that its
    // Dispatcher matches the thread that will later show and update it.
    public override System.Windows.Controls.UserControl GetEditorView()
    {
        try
        {
            if (!EnsureWpfApplication())
                return new System.Windows.Controls.UserControl();
            return Application.Current!.Dispatcher.Invoke(() => new PluginEditorView(_engine));
        }
        catch (Exception ex)
        {
            LogException("GetEditorView", ex);
            return new System.Windows.Controls.UserControl();
        }
    }

    private static void LogException(string context, Exception ex)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AudioPlugSharp");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"OttoSynth-crash-{DateTime.Now:yyyy-MM-dd}.log");
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] {context}:");
            for (var e = ex; e != null; e = e.InnerException)
                sb.AppendLine($"  {e.GetType().Name}: {e.Message}");
            sb.AppendLine(ex.StackTrace);
            sb.AppendLine();
            File.AppendAllText(path, sb.ToString());
        }
        catch { }
    }

    // ─── Initialization ───────────────────────────────────────────

    public override void Initialize()
    {
        try
        {
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
        }
        catch (Exception ex)
        {
            LogException("Initialize", ex);
        }
    }

    public override void InitializeProcessing()
    {
        try
        {
            base.InitializeProcessing();
            _engine.Initialize(Host.SampleRate, (int)Host.MaxAudioBufferSize);
        }
        catch (Exception ex)
        {
            LogException("InitializeProcessing", ex);
        }
    }

    public override void SetMaxAudioBufferSize(uint maxSamples, EAudioBitsPerSample bitsPerSample)
    {
        try
        {
            base.SetMaxAudioBufferSize(maxSamples, bitsPerSample);
            _engine.Initialize(Host.SampleRate, (int)maxSamples);
        }
        catch (Exception ex)
        {
            LogException("SetMaxAudioBufferSize", ex);
        }
    }

    // ─── State chunk ──────────────────────────────────────────────

    public override byte[] SaveState()
    {
        try
        {
            var preset = _presetManager.Capture(_engine, "Plugin State");
            return Encoding.UTF8.GetBytes(_presetManager.ToJson(preset));
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    public override void RestoreState(byte[] stateData)
    {
        try
        {
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
        catch (Exception ex) { LogException("HandleNoteOn", ex); }
    }

    public override void HandleNoteOff(int channel, int noteNumber, float velocity, int sampleOffset)
    {
        try { _engine.ProcessMidiEvent(MidiEvent.NoteOff((byte)noteNumber, (byte)(velocity * 127f))); }
        catch (Exception ex) { LogException("HandleNoteOff", ex); }
    }

    // ─── Automation ───────────────────────────────────────────────

    public override void HandleParameterChange(AudioPluginParameter parameter, double newValue, int sampleOffset)
    {
        try
        {
            base.HandleParameterChange(parameter, newValue, sampleOffset);

            if (int.TryParse(parameter.ID, out int id))
                ParameterDispatcher.Apply(id, newValue, _engine, _hostAdapter);
        }
        catch (Exception ex)
        {
            LogException("HandleParameterChange", ex);
        }
    }

    // ─── Audio processing ─────────────────────────────────────────

    public override void Process()
    {
        try
        {
            base.Process();

            // Drain all pending VST3 events (parameter changes + MIDI). Without this,
            // events queued by the host between Process calls never reach our handlers
            // and the engine stays at defaults — silent. This matches the canonical
            // AudioPlugSharp example pattern (SimpleExamplePlugin / MidiExamplePlugin).
            Host.ProcessAllEvents();

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
        }
        catch (Exception ex)
        {
            LogException("Process", ex);
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

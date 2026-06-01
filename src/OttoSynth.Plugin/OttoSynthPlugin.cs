using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using AudioPlugSharp;
using AudioPlugSharpWPF;
using OttoSynth.Core;
using OttoSynth.Core.Midi;
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
        PluginVersion   = "1.0.0";
        PluginID        = 0x0A7C8ED1C2464B7AUL;
        HasUserInterface = true;
        EditorWidth     = 1280;
        EditorHeight    = 820;
    }

    // ─── WPF Application bootstrap ────────────────────────────────
    // DAW host processes have no WPF Application — Window.Show() crashes without one.
    // We spin a dedicated STA background thread and wait for its dispatcher loop to
    // start before returning, then marshal all WPF operations to that dispatcher.

    private static Thread? _wpfThread;
    private static readonly ManualResetEventSlim _wpfAppReady = new(false);
    private static readonly object _wpfLock = new();

    private static void EnsureWpfApplication()
    {
        if (Application.Current != null)
            return;

        lock (_wpfLock)
        {
            if (Application.Current != null)
                return;

            _wpfThread = new Thread(() =>
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                // Set after the dispatcher loop is actually pumping
                app.Dispatcher.BeginInvoke(() => _wpfAppReady.Set());
                app.Run();
            });
            _wpfThread.SetApartmentState(ApartmentState.STA);
            _wpfThread.IsBackground = true;
            _wpfThread.Name = "OttoSynth WPF";
            _wpfThread.Start();
            _wpfAppReady.Wait(10_000);
        }
    }

    public override void ShowEditor(IntPtr parentWindow)
    {
        EnsureWpfApplication();
        try
        {
            Application.Current!.Dispatcher.Invoke(() => base.ShowEditor(parentWindow));
        }
        catch (Exception ex)
        {
            LogException("ShowEditor", ex);
            throw;
        }
    }

    public override void HideEditor()
    {
        var app = Application.Current;
        if (app != null)
            app.Dispatcher.Invoke(() => { EditorWindow?.Close(); base.HideEditor(); });
        else
            base.HideEditor();
    }

    // ─── AudioPluginWPF ───────────────────────────────────────────

    // AudioPlugSharp calls GetEditorView() on the host thread before ShowEditor().
    // We must ensure the UserControl is created on the WPF STA thread so that its
    // Dispatcher matches the thread that will later show and update it.
    public override System.Windows.Controls.UserControl GetEditorView()
    {
        EnsureWpfApplication();
        try
        {
            return Application.Current!.Dispatcher.Invoke(() => new PluginEditorView(_engine));
        }
        catch (Exception ex)
        {
            LogException("GetEditorView", ex);
            throw;
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
            File.AppendAllText(path,
                $"[{DateTime.Now:HH:mm:ss.fff}] {context}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n");
        }
        catch { }
    }

    // ─── Initialization ───────────────────────────────────────────

    public override void Initialize()
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
    }

    public override void InitializeProcessing()
    {
        base.InitializeProcessing();
        _engine.Initialize(Host.SampleRate, (int)Host.MaxAudioBufferSize);
    }

    public override void SetMaxAudioBufferSize(uint maxSamples, EAudioBitsPerSample bitsPerSample)
    {
        base.SetMaxAudioBufferSize(maxSamples, bitsPerSample);
        _engine.Initialize(Host.SampleRate, (int)maxSamples);
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
            ParameterMap.CaptureAll(_engine, _paramsById);
        }
        catch { /* corrupt state — leave defaults */ }
    }

    // ─── MIDI ─────────────────────────────────────────────────────

    public override void HandleNoteOn(int channel, int noteNumber, float velocity, int sampleOffset)
        => _engine.ProcessMidiEvent(MidiEvent.NoteOn((byte)noteNumber, (byte)(velocity * 127f)));

    public override void HandleNoteOff(int channel, int noteNumber, float velocity, int sampleOffset)
        => _engine.ProcessMidiEvent(MidiEvent.NoteOff((byte)noteNumber, (byte)(velocity * 127f)));

    // ─── Automation ───────────────────────────────────────────────

    public override void HandleParameterChange(AudioPluginParameter parameter, double newValue, int sampleOffset)
    {
        base.HandleParameterChange(parameter, newValue, sampleOffset);

        if (int.TryParse(parameter.ID, out int id))
            ParameterMap.Apply(id, newValue, _engine,
                pid => _paramsById.TryGetValue(pid, out var p) ? p.ProcessValue : 0.0);
    }

    // ─── Audio processing ─────────────────────────────────────────

    public override void Process()
    {
        base.Process();

        // Sync host BPM
        if (Host is { IsPlaying: true, BPM: > 0 } host)
            _engine.Bpm = (int)Math.Round(host.BPM);

        var output = OutputPorts[0] as AudioIOPortManaged;
        if (output == null) return;

        var buffers = output.GetAudioBuffers();
        if (buffers == null || buffers.Length < 2) return;

        _engine.ProcessAudio(buffers[0], buffers[1], (int)Host.CurrentAudioBufferSize);
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

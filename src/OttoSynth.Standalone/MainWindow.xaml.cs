using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AvalonDock.Layout.Serialization;
using HandyControl.Controls;
using Microsoft.Win32;
using Window = System.Windows.Window;
using OttoSynth.Core;
using OttoSynth.Core.Diagnostics;
using OttoSynth.Core.DSP.Effects;
using OttoSynth.Core.Midi;
using OttoSynth.Core.Preset;
using OttoSynth.UI.Commands;
using OttoSynth.UI.ViewModels;
using OttoSynth.Standalone.Models;
using OttoSynth.Standalone.Services;
using OttoSynth.Standalone.Views;
using MidiEvent = OttoSynth.Core.Midi.MidiEvent;

namespace OttoSynth.Standalone;

public partial class MainWindow : Window
{
    private readonly SynthEngine _engine;
    private readonly PresetManager _presetManager;
    private readonly AudioService _audioService;
    private readonly MidiService _midiService;
    private DispatcherTimer? _uiTimer;
    private DispatcherTimer? _midiScanTimer;
    private int _lastMidiDeviceCount = -1;

    private readonly double[] _waveformSnapshot = new double[1024];
    private readonly List<PresetData> _userPresets = new();
    private readonly CommandHistory _history = new();
    private AudioSettings _audioSettings = AudioSettings.Load();
    private PresetBrowserViewModel _browserVm = null!;

    private OscillatorsViewModel _oscsVm = null!;
    private FilterViewModel _filterVm = null!;
    private EnvelopeViewModel _envelopeVm = null!;
    private LfosViewModel _lfosVm = null!;
    private ModMatrixPanelViewModel _modMatrixVm = null!;
    private MasterViewModel _masterVm = null!;

    private static readonly string LayoutPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OttoSynth", "layout.xml");

    public MainWindow()
    {
        InitializeComponent();
        Logger.Initialize();
        Logger.MinimumLevel = Logger.Level.Debug;
        try
        {
            _engine = new SynthEngine(maxVoices: 16, maxBufferSize: 2048);
            _presetManager = new PresetManager();
            _audioService = new AudioService();
            _midiService = new MidiService(_engine);
            _midiService.NoteActiveChanged += (note, on) =>
                Dispatcher.BeginInvoke(() => Keyboard.SetNoteActive(note, on));
        }
        catch (Exception ex)
        {
            Logger.Error("MainWindow.ctor", ex);
            throw;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _oscsVm      = new OscillatorsViewModel(_engine, _history);
        _filterVm    = new FilterViewModel(_engine, _history);
        _envelopeVm  = new EnvelopeViewModel(_engine, _history);
        _lfosVm      = new LfosViewModel(_engine, _history);
        _modMatrixVm = new ModMatrixPanelViewModel(_engine);
        _masterVm    = new MasterViewModel(_engine);

        // ── Keyboard shortcuts ───────────────────────────────────
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo,
            (_, _) => { _history.Undo(); StatusText.Text = $"> UNDO: {_history.RedoDescription ?? "—"}"; }));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo,
            (_, _) => { _history.Redo(); StatusText.Text = $"> REDO: {_history.UndoDescription ?? "—"}"; }));

        OscillatorsPanelView.DataContext = _oscsVm;
        FilterPanelView.DataContext      = _filterVm;
        EnvelopePanelView.DataContext    = _envelopeVm;
        LfosPanelView.DataContext        = _lfosVm;
        ModMatrixPanelView.DataContext   = _modMatrixVm;
        MasterPanelView.DataContext      = _masterVm;

        _browserVm = new PresetBrowserViewModel();
        _browserVm.PresetLoadRequested += p =>
        {
            var oldState = _presetManager.Capture(_engine, "Previous State");
            _history.Execute(new PresetLoadCommand(p.Name, ApplyPreset, oldState, p));
            StatusText.Text = $"> LOADED: {p.Name}";
            Growl.SuccessGlobal($"Preset loaded: {p.Name}");
        };
        PresetBrowserPanelView.DataContext = _browserVm;

        DockManager.Theme = new AvalonDock.Themes.Vs2013DarkTheme();

        _engine.Effects.Add(new Reverb { Mix = 0.25, Bypass = true });
        _engine.Effects.Add(new Delay  { Mix = 0.2,  Bypass = true });
        EffectsRackView.Initialize(_engine);
        ArpSequencerPanelView.Initialize(_engine);

        RestoreLayout();
        PopulatePresets();
        PopulateMidiDevices();
        InitializeAudio();

        Keyboard.NoteOn  += (_, note) => _engine.ProcessMidiEvent(MidiEvent.NoteOn((byte)note, 100));
        Keyboard.NoteOff += (_, note) => _engine.ProcessMidiEvent(MidiEvent.NoteOff((byte)note));

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _uiTimer.Tick += UiTimer_Tick;
        _uiTimer.Start();

        _midiScanTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _midiScanTimer.Tick += MidiScanTimer_Tick;
        _midiScanTimer.Start();

        foreach (var t in ThemeManager.ThemeNames)
            ThemeSelector.Items.Add(t);
        ThemeSelector.SelectedItem = ThemeManager.Current;

        StatusText.Text = "> AUDIO ENGINE ONLINE";
    }

    private void MidiScanTimer_Tick(object? sender, EventArgs e)
    {
        int count = MidiService.DeviceCount;
        if (count != _lastMidiDeviceCount)
        {
            _lastMidiDeviceCount = count;
            PopulateMidiDevices();
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _uiTimer?.Stop();
        _midiScanTimer?.Stop();
        _midiService.Dispose();
        _audioService.Dispose();
        SaveLayout();
    }

    private void SaveLayout()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath)!);
            var serializer = new XmlLayoutSerializer(DockManager);
            using var stream = File.Create(LayoutPath);
            serializer.Serialize(stream);
        }
        catch (Exception ex) { Logger.Error("SaveLayout", ex); }
    }

    private void RestoreLayout()
    {
        if (!File.Exists(LayoutPath)) return;
        try
        {
            var serializer = new XmlLayoutSerializer(DockManager);
            serializer.LayoutSerializationCallback += (_, args) =>
            {
                args.Content = args.Model.ContentId switch
                {
                    "Oscillators"   => OscillatorsPanelView,
                    "Filter"        => FilterPanelView,
                    "Envelope"      => EnvelopePanelView,
                    "Lfos"          => LfosPanelView,
                    "ModMatrix"     => ModMatrixPanelView,
                    "Analyzer"      => AnalyzerPanelView,
                    "Master"        => MasterPanelView,
                    "ArpSeq"        => ArpSequencerPanelView,
                    "PresetBrowser" => PresetBrowserPanelView,
                    _               => null
                };
            };
            using var stream = File.OpenRead(LayoutPath);
            serializer.Deserialize(stream);
        }
        catch (Exception ex) { Logger.Error("RestoreLayout", ex); }
    }

    // ─── AUDIO ──────────────────────────────────────────────────

    private void InitializeAudio()
    {
        _audioService.Initialize(_engine, _audioSettings.SampleRate, _audioSettings.BufferSize);
        SrText.Text = $"{_audioService.SampleRate} Hz";
        AnalyzerPanelView.SampleRate = _audioService.SampleRate;
    }

    private void AudioSettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AudioSettingsDialog(_audioService.SampleRate, _audioService.BufferSize) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        _audioSettings.SampleRate = dlg.SelectedSampleRate;
        _audioSettings.BufferSize = dlg.SelectedBufferSize;
        _audioSettings.Save();

        _audioService.Reinitialize(_audioSettings.SampleRate, _audioSettings.BufferSize);
        SrText.Text = $"{_audioService.SampleRate} Hz";
        AnalyzerPanelView.SampleRate = _audioService.SampleRate;

        StatusText.Text = $"> AUDIO: {_audioService.SampleRate} Hz, buffer={_audioService.BufferSize}, latency≈{_audioService.LatencyMs}ms";
        Growl.InfoGlobal($"Audio restarted: {_audioService.SampleRate} Hz, ~{_audioService.LatencyMs}ms latency");
    }

    // ─── PRESETS ────────────────────────────────────────────────

    private void PopulatePresets()
    {
        PresetSelector.Items.Clear();
        foreach (var p in FactoryPresets.All())
            PresetSelector.Items.Add(p.Name);

        _userPresets.Clear();
        foreach (var (_, preset) in _presetManager.ScanUserPresets())
        {
            _userPresets.Add(preset);
            PresetSelector.Items.Add($"[User] {preset.Name}");
        }

        PresetSelector.SelectedIndex = 0;

        _browserVm?.Refresh(FactoryPresets.All(), _userPresets);
    }

    private PresetData? FindPreset(string name)
    {
        if (name.StartsWith("[User] "))
        {
            string userName = name["[User] ".Length..];
            return _userPresets.FirstOrDefault(p => p.Name == userName);
        }
        foreach (var p in FactoryPresets.All())
            if (p.Name == name) return p;
        return null;
    }

    private void ApplyPreset(PresetData preset)
    {
        _presetManager.Apply(preset, _engine);
        _oscsVm.ApplyPreset(preset);
        _filterVm.ApplyPreset(preset);
        _envelopeVm.ApplyPreset(preset);
        _lfosVm.ApplyPreset(preset);
        _modMatrixVm.ApplyPreset(preset);
        EffectsRackView.Refresh();
    }

    private void PresetSelector_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeSelector.SelectedItem is string theme)
            ThemeManager.Apply(theme);
    }

    private void LoadPresetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (PresetSelector.SelectedItem is not string name) return;
        var preset = FindPreset(name);
        if (preset == null) return;

        var oldState = _presetManager.Capture(_engine, "Previous State");
        _history.Execute(new PresetLoadCommand(preset.Name, ApplyPreset, oldState, preset));

        StatusText.Text = $"> LOADED: {preset.Name}";
        Growl.SuccessGlobal($"Preset loaded: {preset.Name}");
    }

    private void SavePresetBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title    = "Save OttoSynth Preset",
            Filter   = "OttoSynth Preset (*.otto)|*.otto",
            DefaultExt = ".otto",
            FileName = "MyPreset"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            string name = Path.GetFileNameWithoutExtension(dlg.FileName);
            _presetManager.ExportToFile(_engine, dlg.FileName, name);
            PopulatePresets();
            StatusText.Text = $"> SAVED: {dlg.FileName}";
            Growl.SuccessGlobal($"Preset saved: {name}");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"> SAVE ERROR: {ex.Message}";
            Growl.ErrorGlobal($"Save failed: {ex.Message}");
            Logger.Error("SavePreset", ex);
        }
    }

    private void ImportPresetBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Import OttoSynth Preset",
            Filter = "OttoSynth Preset (*.otto;*.ottopreset)|*.otto;*.ottopreset|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var preset = _presetManager.ImportFromFile(dlg.FileName, _engine);
            ApplyPreset(preset);
            StatusText.Text = $"> IMPORTED: {preset.Name}";
            Growl.InfoGlobal($"Preset imported: {preset.Name}");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"> IMPORT ERROR: {ex.Message}";
            Growl.ErrorGlobal($"Import failed: {ex.Message}");
            Logger.Error("ImportPreset", ex);
        }
    }

    private void ExportPresetBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title    = "Export OttoSynth Preset",
            Filter   = "OttoSynth Preset (*.otto)|*.otto",
            DefaultExt = ".otto",
            FileName = "Export"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            string name = Path.GetFileNameWithoutExtension(dlg.FileName);
            _presetManager.ExportToFile(_engine, dlg.FileName, name);
            StatusText.Text = $"> EXPORTED: {dlg.FileName}";
            Growl.SuccessGlobal($"Preset exported: {name}");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"> EXPORT ERROR: {ex.Message}";
            Growl.ErrorGlobal($"Export failed: {ex.Message}");
            Logger.Error("ExportPreset", ex);
        }
    }

    // ─── MIDI ───────────────────────────────────────────────────

    private void PopulateMidiDevices()
    {
        string? previousName = MidiDeviceSelector.SelectedItem as string;

        MidiDeviceSelector.Items.Clear();
        MidiDeviceSelector.Items.Add("(None)");
        var deviceNames = _midiService.GetDeviceNames();
        foreach (var name in deviceNames)
            MidiDeviceSelector.Items.Add(name);

        if (previousName != null && MidiDeviceSelector.Items.Contains(previousName))
            MidiDeviceSelector.SelectedItem = previousName;
        else
            MidiDeviceSelector.SelectedIndex = 0;

        StatusText.Text = deviceNames.Count > 0
            ? $"> {deviceNames.Count} MIDI device(s) detected"
            : "> NO MIDI DEVICES DETECTED — connect your controller and click [ ↻ ]";
    }

    private void RefreshMidiBtn_Click(object sender, RoutedEventArgs e) => PopulateMidiDevices();

    private void MidiDeviceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int idx = MidiDeviceSelector.SelectedIndex - 1;
        if (idx >= 0)
        {
            try
            {
                _midiService.Connect(idx);
                var names = _midiService.GetDeviceNames();
                string deviceName = idx < names.Count ? names[idx] : $"device {idx}";
                StatusText.Text = $"> CONNECTED: {deviceName}";
            }
            catch (Exception ex) { StatusText.Text = $"> MIDI ERROR: {ex.Message}"; }
        }
        else
        {
            _midiService.Disconnect();
            StatusText.Text = "> MIDI DISCONNECTED";
        }
    }

    // ─── MISC ───────────────────────────────────────────────────

    private void PanicBtn_Click(object sender, RoutedEventArgs e)
    {
        _engine.AllNotesOff();
        StatusText.Text = "All notes off";
    }

    // ─── UI TIMER ───────────────────────────────────────────────

    private void UiTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            VoicesText.Text = $"VOICES:{_engine.VoiceManager.ActiveVoiceCount}";

            ArpSequencerPanelView.RefreshPlayhead();

            _audioService.GetLastBuffer(_waveformSnapshot);
            var smallSlice = new double[512];
            Array.Copy(_waveformSnapshot, smallSlice, 512);
            AnalyzerPanelView.WaveformSamples = smallSlice;
            AnalyzerPanelView.SpectrumSamples = _waveformSnapshot;

        }
        catch (Exception ex)
        {
            try { Logger.Error("UiTimer_Tick", ex); } catch { }
        }
    }

}

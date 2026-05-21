using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using OttoSynth.Core;
using OttoSynth.Core.Diagnostics;
using OttoSynth.Core.DSP.Effects;
using OttoSynth.Core.DSP.Filters;
using OttoSynth.Core.DSP.Modulation;
using OttoSynth.Core.DSP.Oscillators;
using OttoSynth.Core.Midi;
using OttoSynth.Core.Preset;
using OttoSynth.Core.Voice;
using OttoSynth.Standalone.Services;
using OttoSynth.UI.Controls;
using MidiEvent = OttoSynth.Core.Midi.MidiEvent;

namespace OttoSynth.Standalone;

/// <summary>
/// Standalone host window for the OttoSynth engine.
/// Audio: NAudio WaveOut. MIDI input: NAudio MidiIn.
/// </summary>
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

    // User presets loaded from disk (in addition to factory presets)
    private readonly List<PresetData> _userPresets = new();

    // Friendly display names for filter modes (order matters — determines ComboBox order)
    private static readonly (StateVariableFilter.FilterMode Mode, string Label)[] FilterModeLabels =
    [
        (StateVariableFilter.FilterMode.LowPass,      "LP 12dB"),
        (StateVariableFilter.FilterMode.HighPass,     "HP 12dB"),
        (StateVariableFilter.FilterMode.BandPass,     "BP 12dB"),
        (StateVariableFilter.FilterMode.Notch,        "Notch"),
        (StateVariableFilter.FilterMode.AllPass,      "AllPass"),
        (StateVariableFilter.FilterMode.Peak,         "Peak"),
        (StateVariableFilter.FilterMode.MoogLadder,   "Moog 24dB"),
        (StateVariableFilter.FilterMode.K35LP,        "K35 LP"),
        (StateVariableFilter.FilterMode.K35HP,        "K35 HP"),
        (StateVariableFilter.FilterMode.CombPositive, "Comb +"),
        (StateVariableFilter.FilterMode.CombNegative, "Comb -"),
    ];

    private static readonly Dictionary<string, StateVariableFilter.FilterMode> FilterLabelToMode =
        FilterModeLabels.ToDictionary(x => x.Label, x => x.Mode);

    // Maps legacy preset mode strings to display labels
    private static readonly Dictionary<string, string> LegacyFilterModeMap = new()
    {
        ["LP"]    = "LP 12dB",
        ["HP"]    = "HP 12dB",
        ["BP"]    = "BP 12dB",
        ["Notch"] = "Notch",
    };

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
        PopulateCombos();
        PopulateMidiDevices();
        PopulatePresets();
        InitializeAudio();

        // Default Effects rack: Reverb + Delay (bypassed)
        _engine.Effects.Add(new Reverb { Mix = 0.25, Bypass = true });
        _engine.Effects.Add(new Delay { Mix = 0.2, Bypass = true });
        RefreshEffectsPanel();

        // Keyboard wiring
        Keyboard.NoteOn  += (_, note) => _engine.ProcessMidiEvent(MidiEvent.NoteOn((byte)note, 100));
        Keyboard.NoteOff += (_, note) => _engine.ProcessMidiEvent(MidiEvent.NoteOff((byte)note));

        // UI Timer (30fps for displays)
        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _uiTimer.Tick += UiTimer_Tick;
        _uiTimer.Start();

        // MIDI auto-scan timer (every 2 seconds)
        _midiScanTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _midiScanTimer.Tick += MidiScanTimer_Tick;
        _midiScanTimer.Start();

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
    }

    // ─── AUDIO ──────────────────────────────────────────────────

    private void InitializeAudio()
    {
        _audioService.Initialize(_engine, AudioService.DefaultSampleRate);
        SrText.Text = $"{_audioService.SampleRate} Hz";
        SpectrumView.SampleRate = _audioService.SampleRate;
    }

    private void PopulateCombos()
    {
        // Wavetables (per oscillator)
        foreach (var name in new[] { "Saw", "Sine", "Square", "Triangle" })
        {
            Osc1Wavetable.Items.Add(name);
            Osc2Wavetable.Items.Add(name);
            Osc3Wavetable.Items.Add(name);
        }
        Osc1Wavetable.SelectedItem = "Saw";
        Osc2Wavetable.SelectedItem = "Sine";
        Osc3Wavetable.SelectedItem = "Triangle";

        // Filter modes
        foreach (var (_, label) in FilterModeLabels)
            FilterMode.Items.Add(label);
        FilterMode.SelectedIndex = 0;

        // LFO shapes
        foreach (LfoGenerator.LfoShape s in Enum.GetValues<LfoGenerator.LfoShape>())
        {
            Lfo1Shape.Items.Add(s.ToString());
            Lfo2Shape.Items.Add(s.ToString());
            Lfo3Shape.Items.Add(s.ToString());
        }
        Lfo1Shape.SelectedIndex = 0;
        Lfo2Shape.SelectedIndex = 0;
        Lfo3Shape.SelectedIndex = 0;

        // Warp types (per oscillator)
        foreach (WavetableOscillator.WaveWarp w in Enum.GetValues<WavetableOscillator.WaveWarp>())
        {
            Osc1WarpType.Items.Add(w.ToString());
            Osc2WarpType.Items.Add(w.ToString());
            Osc3WarpType.Items.Add(w.ToString());
        }
        Osc1WarpType.SelectedIndex = 0;
        Osc2WarpType.SelectedIndex = 0;
        Osc3WarpType.SelectedIndex = 0;

        // Glide modes
        foreach (SynthVoice.GlideMode g in Enum.GetValues<SynthVoice.GlideMode>())
            GlideModeCombo.Items.Add(g.ToString());
        GlideModeCombo.SelectedIndex = 0;
    }

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

    // ─── HANDLERS ───────────────────────────────────────────────

    private void Osc1Wavetable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => _engine.SelectWavetable(1, (string)Osc1Wavetable.SelectedItem);
    private void Osc2Wavetable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => _engine.SelectWavetable(2, (string)Osc2Wavetable.SelectedItem);
    private void Osc3Wavetable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => _engine.SelectWavetable(3, (string)Osc3Wavetable.SelectedItem);

    private void OscEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_engine == null) return;
        _engine.SetOscillatorMix(1, Osc1LevelKnob.Value, Osc1Enabled.IsChecked == true);
        _engine.SetOscillatorMix(2, Osc2LevelKnob.Value, Osc2Enabled.IsChecked == true);
        _engine.SetOscillatorMix(3, Osc3LevelKnob.Value, Osc3Enabled.IsChecked == true);
    }

    private void Osc1Knob_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyOscParams(1);
    private void Osc2Knob_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyOscParams(2);
    private void Osc3Knob_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyOscParams(3);

    private void ApplyOscParams(int idx)
    {
        if (_engine == null) return;
        SynthKnob lvl = idx == 1 ? Osc1LevelKnob : idx == 2 ? Osc2LevelKnob : Osc3LevelKnob;
        SynthKnob pos = idx == 1 ? Osc1PositionKnob : idx == 2 ? Osc2PositionKnob : Osc3PositionKnob;
        SynthKnob warp = idx == 1 ? Osc1WarpAmt : idx == 2 ? Osc2WarpAmt : Osc3WarpAmt;
        SynthKnob coarse = idx == 1 ? Osc1CoarseKnob : idx == 2 ? Osc2CoarseKnob : Osc3CoarseKnob;
        SynthKnob fine = idx == 1 ? Osc1FineKnob : idx == 2 ? Osc2FineKnob : Osc3FineKnob;
        SynthKnob pan = idx == 1 ? Osc1PanKnob : idx == 2 ? Osc2PanKnob : Osc3PanKnob;
        CheckBox en = idx == 1 ? Osc1Enabled : idx == 2 ? Osc2Enabled : Osc3Enabled;

        _engine.SetOscillatorMix(idx, lvl.Value, en.IsChecked == true);
        foreach (var v in _engine.VoiceManager.Voices)
        {
            var o = idx == 1 ? v.Osc1 : idx == 2 ? v.Osc2 : v.Osc3;
            o.CoarseTune = (int)coarse.Value;
            o.FineTune = fine.Value;
            o.WavetablePosition = pos.Value;
            o.WarpAmount = warp.Value;
            o.Pan = pan.Value;
        }
    }

    private void Osc1WarpType_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyWarpType(1);
    private void Osc2WarpType_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyWarpType(2);
    private void Osc3WarpType_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyWarpType(3);

    private void ApplyWarpType(int idx)
    {
        if (_engine == null) return;
        var combo = idx == 1 ? Osc1WarpType : idx == 2 ? Osc2WarpType : Osc3WarpType;
        if (combo.SelectedItem == null) return;
        if (!Enum.TryParse<WavetableOscillator.WaveWarp>(combo.SelectedItem.ToString(), out var warp))
            return;
        foreach (var v in _engine.VoiceManager.Voices)
        {
            var o = idx == 1 ? v.Osc1 : idx == 2 ? v.Osc2 : v.Osc3;
            o.Warp = warp;
        }
    }

    private void FilterMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => ApplyFilter();

    private void Filter24dB_Changed(object sender, RoutedEventArgs e) => ApplyFilter();
    private void FilterKnob_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyFilter();
    private void FilterEnvAmt_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        => _engine?.SetFilterEnvAmount(FilterEnvAmtKnob.Value);

    private void GlideKnob_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyGlide();
    private void GlideModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyGlide();

    private void ApplyGlide()
    {
        if (_engine == null || GlideModeCombo.SelectedItem is not string modeStr) return;
        if (Enum.TryParse<SynthVoice.GlideMode>(modeStr, out var mode))
            _engine.SetPortamento(GlideTimeKnob.Value, mode);
    }

    private void ApplyFilter()
    {
        if (_engine == null || FilterMode.SelectedItem is not string label) return;
        if (FilterLabelToMode.TryGetValue(label, out var mode))
            _engine.SetFilter(1, mode, CutoffKnob.Value, ResonanceKnob.Value, DriveKnob.Value, Filter24dB.IsChecked == true);
    }

    private void EnvelopeKnob_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_engine == null) return;
        _engine.SetEnvelope(AttackKnob.Value, DecayKnob.Value, SustainKnob.Value, ReleaseKnob.Value);
    }

    private void Lfo1Shape_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyLfo(1);
        if (Enum.TryParse<LfoDisplay.LfoShape>(Lfo1Shape.SelectedItem?.ToString(), out var s)) Lfo1View.Shape = s;
    }
    private void Lfo2Shape_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyLfo(2);
        if (Enum.TryParse<LfoDisplay.LfoShape>(Lfo2Shape.SelectedItem?.ToString(), out var s)) Lfo2View.Shape = s;
    }
    private void Lfo3Shape_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyLfo(3);
        if (Enum.TryParse<LfoDisplay.LfoShape>(Lfo3Shape.SelectedItem?.ToString(), out var s)) Lfo3View.Shape = s;
    }
    private void Lfo1Knob_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyLfo(1);
    private void Lfo2Knob_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyLfo(2);
    private void Lfo3Knob_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyLfo(3);

    private void ApplyLfo(int idx)
    {
        if (_engine == null) return;
        var shapeCombo = idx == 1 ? Lfo1Shape : idx == 2 ? Lfo2Shape : Lfo3Shape;
        var rate = idx == 1 ? Lfo1Rate : idx == 2 ? Lfo2Rate : Lfo3Rate;
        var depth = idx == 1 ? Lfo1Depth : idx == 2 ? Lfo2Depth : Lfo3Depth;
        if (Enum.TryParse<LfoGenerator.LfoShape>(shapeCombo.SelectedItem?.ToString(), out var shape))
            _engine.SetLfo(idx, shape, rate.Value, depth.Value);
    }

    private void MacroKnob_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_engine == null) return;
        _engine.SetMacro(0, Macro1Knob.Value);
        _engine.SetMacro(1, Macro2Knob.Value);
        _engine.SetMacro(2, Macro3Knob.Value);
        _engine.SetMacro(3, Macro4Knob.Value);
    }

    private void MasterVolumeKnob_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_engine != null) _engine.MasterVolume = MasterVolumeKnob.Value;
    }

    private void PanicBtn_Click(object sender, RoutedEventArgs e)
    {
        _engine.AllNotesOff();
        StatusText.Text = "All notes off";
    }

    private void PresetSelector_SelectionChanged(object sender, SelectionChangedEventArgs e) { /* load on button click */ }

    private void LoadPresetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (PresetSelector.SelectedItem is not string name) return;
        var preset = FindPreset(name);
        if (preset == null) return;
        _presetManager.Apply(preset, _engine);
        ReflectPresetIntoUi(preset);
        RefreshEffectsPanel();
        RefreshModMatrix();
        StatusText.Text = $"> LOADED: {preset.Name}";
    }

    private void SavePresetBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Save OttoSynth Preset",
            Filter = "OttoSynth Preset (*.otto)|*.otto",
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
        }
        catch (Exception ex)
        {
            StatusText.Text = $"> SAVE ERROR: {ex.Message}";
            Logger.Error("SavePreset", ex);
        }
    }

    private void ImportPresetBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Import OttoSynth Preset",
            Filter = "OttoSynth Preset (*.otto;*.ottopreset)|*.otto;*.ottopreset|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var preset = _presetManager.ImportFromFile(dlg.FileName, _engine);
            ReflectPresetIntoUi(preset);
            RefreshEffectsPanel();
            RefreshModMatrix();
            StatusText.Text = $"> IMPORTED: {preset.Name}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"> IMPORT ERROR: {ex.Message}";
            Logger.Error("ImportPreset", ex);
        }
    }

    private void ExportPresetBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Export OttoSynth Preset",
            Filter = "OttoSynth Preset (*.otto)|*.otto",
            DefaultExt = ".otto",
            FileName = "Export"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            string name = Path.GetFileNameWithoutExtension(dlg.FileName);
            _presetManager.ExportToFile(_engine, dlg.FileName, name);
            StatusText.Text = $"> EXPORTED: {dlg.FileName}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"> EXPORT ERROR: {ex.Message}";
            Logger.Error("ExportPreset", ex);
        }
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

    private void ReflectPresetIntoUi(PresetData p)
    {
        Osc1Wavetable.SelectedItem = p.Osc1.Wavetable;
        Osc2Wavetable.SelectedItem = p.Osc2.Wavetable;
        Osc3Wavetable.SelectedItem = p.Osc3.Wavetable;
        Osc1Enabled.IsChecked = p.Osc1.Enabled;
        Osc2Enabled.IsChecked = p.Osc2.Enabled;
        Osc3Enabled.IsChecked = p.Osc3.Enabled;
        Osc1LevelKnob.Value = p.Osc1.Level;
        Osc2LevelKnob.Value = p.Osc2.Level;
        Osc3LevelKnob.Value = p.Osc3.Level;
        Osc1CoarseKnob.Value = p.Osc1.CoarseTune;
        Osc2CoarseKnob.Value = p.Osc2.CoarseTune;
        Osc3CoarseKnob.Value = p.Osc3.CoarseTune;
        Osc1FineKnob.Value = p.Osc1.FineTune;
        Osc2FineKnob.Value = p.Osc2.FineTune;
        Osc3FineKnob.Value = p.Osc3.FineTune;
        Osc1PositionKnob.Value = p.Osc1.WavetablePosition;
        Osc2PositionKnob.Value = p.Osc2.WavetablePosition;
        Osc3PositionKnob.Value = p.Osc3.WavetablePosition;
        Osc1PanKnob.Value = p.Osc1.Pan;
        Osc2PanKnob.Value = p.Osc2.Pan;
        Osc3PanKnob.Value = p.Osc3.Pan;

        string filterLabel = LegacyFilterModeMap.TryGetValue(p.Filter1.Mode, out var mapped)
            ? mapped : p.Filter1.Mode;
        FilterMode.SelectedItem = filterLabel;
        Filter24dB.IsChecked = p.Filter1.Is24dB;
        CutoffKnob.Value = p.Filter1.Cutoff;
        ResonanceKnob.Value = p.Filter1.Resonance;
        DriveKnob.Value = p.Filter1.Drive;
        FilterEnvAmtKnob.Value = p.FilterEnvAmount;

        AttackKnob.Value = p.EnvAmp.Attack;
        DecayKnob.Value = p.EnvAmp.Decay;
        SustainKnob.Value = p.EnvAmp.Sustain;
        ReleaseKnob.Value = p.EnvAmp.Release;

        MasterVolumeKnob.Value = p.MasterVolume;
        Macro1Knob.Value = p.Macros[0];
        Macro2Knob.Value = p.Macros[1];
        Macro3Knob.Value = p.Macros[2];
        Macro4Knob.Value = p.Macros[3];

        GlideTimeKnob.Value = p.GlideTime;
        GlideModeCombo.SelectedItem = p.GlideMode;
    }

    private void RefreshEffectsPanel()
    {
        EffectsPanel.Children.Clear();
        for (int i = 0; i < _engine.Effects.Count; i++)
        {
            var fx = _engine.Effects[i];
            var slot = new EffectSlot
            {
                EffectName = fx.Name,
                IsBypassed = fx.Bypass,
                Mix = fx.Mix,
                Margin = new Thickness(2)
            };
            int captureIdx = i;
            slot.MouseLeftButtonDown += (s, e) =>
            {
                var f = _engine.Effects[captureIdx];
                f.Bypass = !f.Bypass;
                slot.IsBypassed = f.Bypass;
            };
            EffectsPanel.Children.Add(slot);
        }
    }

    private void RefreshModMatrix()
    {
        var list = new List<ModMatrixGrid.Route>();
        foreach (var r in _engine.VoiceManager.Voices[0].ModMatrix.Routes)
        {
            list.Add(new ModMatrixGrid.Route
            {
                Source = r.Source.ToString(),
                Destination = r.Destination.ToString(),
                Amount = r.Amount
            });
        }
        ModMatrixView.Routes = list;
    }

    // ─── UI TIMER ───────────────────────────────────────────────

    private void UiTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            VoicesText.Text = $"VOICES:{_engine.VoiceManager.ActiveVoiceCount}";

            _audioService.GetLastBuffer(_waveformSnapshot);
            var smallSlice = new double[512];
            Array.Copy(_waveformSnapshot, smallSlice, 512);
            WaveformView.Samples = smallSlice;
            SpectrumView.Samples = _waveformSnapshot;

            // Drain logger into the log textbox
            bool any = false;
            Logger.DrainTo(entry =>
            {
                LogBox.AppendText(entry.ToString() + Environment.NewLine);
                any = true;
            });
            if (any)
            {
                // Trim log to last 200 lines
                if (LogBox.LineCount > 200)
                {
                    int charsToCut = LogBox.GetCharacterIndexFromLineIndex(LogBox.LineCount - 200);
                    if (charsToCut > 0)
                        LogBox.Text = LogBox.Text.Substring(charsToCut);
                }
                LogBox.ScrollToEnd();
            }
        }
        catch (Exception ex)
        {
            // UI timer must never crash the app
            try { Logger.Error("UiTimer_Tick", ex); } catch { }
        }
    }

    private void ClearLogBtn_Click(object sender, RoutedEventArgs e)
    {
        LogBox.Clear();
        Logger.Clear();
    }
}


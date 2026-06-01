using OttoSynth.Core;
using OttoSynth.Core.DSP.Oscillators;
using OttoSynth.Core.Preset;
using OttoSynth.UI.Commands;

namespace OttoSynth.UI.ViewModels;

public class OscillatorViewModel : ViewModelBase
{
    private readonly SynthEngine _engine;
    private readonly int _index;
    private CommandHistory? _history;
    private bool _loading;
    internal bool _suppressHistory;

    public int Index => _index;
    public string Title => $"OSC {_index}";

    private bool _isEnabled = true;
    public bool IsEnabled { get => _isEnabled; set => SetField(ref _isEnabled, value); }

    private string _selectedWavetable = "Saw";
    public string SelectedWavetable { get => _selectedWavetable; set => SetField(ref _selectedWavetable, value); }

    private string _selectedWarp = "None";
    public string SelectedWarp { get => _selectedWarp; set => SetField(ref _selectedWarp, value); }

    private double _level = 0.7;
    public double Level { get => _level; set => SetField(ref _level, value); }

    private double _position = 0.0;
    public double Position { get => _position; set => SetField(ref _position, value); }

    private double _warpAmount = 0.0;
    public double WarpAmount { get => _warpAmount; set => SetField(ref _warpAmount, value); }

    private double _coarseTune = 0.0;
    public double CoarseTune { get => _coarseTune; set => SetField(ref _coarseTune, value); }

    private double _fineTune = 0.0;
    public double FineTune { get => _fineTune; set => SetField(ref _fineTune, value); }

    private double _pan = 0.0;
    public double Pan { get => _pan; set => SetField(ref _pan, value); }

    private double _unisonVoices = 1.0;
    public double UnisonVoices { get => _unisonVoices; set => SetField(ref _unisonVoices, value); }

    private double _unisonDetune = 20.0;
    public double UnisonDetune { get => _unisonDetune; set => SetField(ref _unisonDetune, value); }

    private double _unisonSpread = 0.8;
    public double UnisonSpread { get => _unisonSpread; set => SetField(ref _unisonSpread, value); }

    public OscillatorViewModel(SynthEngine engine, int index, CommandHistory? history = null)
    {
        _engine  = engine;
        _index   = index;
        _history = history;

        PropertyChanged += (_, e) =>
        {
            if (_loading) return;
            switch (e.PropertyName)
            {
                case nameof(IsEnabled):
                case nameof(Level):
                    ApplyMix();
                    break;
                case nameof(SelectedWavetable):
                    _engine.SelectWavetable(_index, SelectedWavetable);
                    break;
                case nameof(SelectedWarp):
                    if (Enum.TryParse<WavetableOscillator.WaveWarp>(SelectedWarp, out var warp))
                        _engine.SetOscillatorWarp(_index, warp);
                    break;
                case nameof(Position):
                case nameof(WarpAmount):
                case nameof(CoarseTune):
                case nameof(FineTune):
                case nameof(Pan):
                    ApplyParams();
                    break;
                case nameof(UnisonVoices):
                case nameof(UnisonDetune):
                case nameof(UnisonSpread):
                    ApplyUnison();
                    break;
            }
        };

        string prefix = $"OSC{index}";
        TrackUndo(nameof(Level),      () => Level,      v => Level      = v, $"{prefix} Level");
        TrackUndo(nameof(Position),   () => Position,   v => Position   = v, $"{prefix} Position");
        TrackUndo(nameof(WarpAmount), () => WarpAmount, v => WarpAmount = v, $"{prefix} Warp Amt");
        TrackUndo(nameof(FineTune),   () => FineTune,   v => FineTune   = v, $"{prefix} Fine Tune");
        TrackUndo(nameof(Pan),        () => Pan,        v => Pan        = v, $"{prefix} Pan");
    }

    private void TrackUndo(string propName, Func<double> getter, Action<double> setter, string name)
    {
        double prev = getter();
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != propName) return;
            var newVal = getter();
            var old = prev;
            prev = newVal;
            if (_loading || _suppressHistory || _history == null) return;
            if (Math.Abs(old - newVal) < 1e-10) return;
            _history.Execute(new ParameterCommand(name,
                val => { _suppressHistory = true; setter(val); _suppressHistory = false; },
                old, newVal));
        };
    }

    private void ApplyMix() => _engine.SetOscillatorMix(_index, Level, IsEnabled);

    private void ApplyParams()
    {
        if (_loading) return;
        _engine.SetOscillatorParams(_index, (int)CoarseTune, FineTune, Position, WarpAmount, Pan);
    }

    private void ApplyUnison()
    {
        if (!_loading)
            _engine.SetOscillatorUnison(_index, (int)Math.Round(UnisonVoices), UnisonDetune, UnisonSpread);
    }

    public void ApplyPreset(OscillatorData data)
    {
        _loading = true;
        try
        {
            IsEnabled         = data.Enabled;
            SelectedWavetable = data.Wavetable;
            Level             = data.Level;
            CoarseTune        = data.CoarseTune;
            FineTune          = data.FineTune;
            Position          = data.WavetablePosition;
            WarpAmount        = data.WarpAmount;
            Pan               = data.Pan;
            SelectedWarp      = data.WarpType;
            UnisonVoices      = (double)data.UnisonVoices;
            UnisonDetune      = data.UnisonDetune;
            UnisonSpread      = data.UnisonSpread;
        }
        finally
        {
            _loading = false;
            ApplyMix();
            ApplyParams();
            ApplyUnison();
            _engine.SelectWavetable(_index, SelectedWavetable);
            if (Enum.TryParse<WavetableOscillator.WaveWarp>(SelectedWarp, out var warp))
                _engine.SetOscillatorWarp(_index, warp);
        }
    }
}

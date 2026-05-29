using System.Reactive.Linq;
using OttoSynth.Core;
using OttoSynth.Core.DSP.Oscillators;
using OttoSynth.Core.Preset;
using OttoSynth.UI.Commands;
using ReactiveUI;

namespace OttoSynth.UI.ViewModels;

public class OscillatorViewModel : ReactiveObject
{
    private readonly SynthEngine _engine;
    private readonly int _index;
    private CommandHistory? _history;
    private bool _loading;
    internal bool _suppressHistory;

    public int Index => _index;
    public string Title => $"OSC {_index}";

    private bool _isEnabled = true;
    public bool IsEnabled { get => _isEnabled; set => this.RaiseAndSetIfChanged(ref _isEnabled, value); }

    private string _selectedWavetable = "Saw";
    public string SelectedWavetable { get => _selectedWavetable; set => this.RaiseAndSetIfChanged(ref _selectedWavetable, value); }

    private string _selectedWarp = "None";
    public string SelectedWarp { get => _selectedWarp; set => this.RaiseAndSetIfChanged(ref _selectedWarp, value); }

    private double _level = 0.7;
    public double Level { get => _level; set => this.RaiseAndSetIfChanged(ref _level, value); }

    private double _position = 0.0;
    public double Position { get => _position; set => this.RaiseAndSetIfChanged(ref _position, value); }

    private double _warpAmount = 0.0;
    public double WarpAmount { get => _warpAmount; set => this.RaiseAndSetIfChanged(ref _warpAmount, value); }

    private double _coarseTune = 0.0;
    public double CoarseTune { get => _coarseTune; set => this.RaiseAndSetIfChanged(ref _coarseTune, value); }

    private double _fineTune = 0.0;
    public double FineTune { get => _fineTune; set => this.RaiseAndSetIfChanged(ref _fineTune, value); }

    private double _pan = 0.0;
    public double Pan { get => _pan; set => this.RaiseAndSetIfChanged(ref _pan, value); }

    private double _unisonVoices = 1.0;
    public double UnisonVoices { get => _unisonVoices; set => this.RaiseAndSetIfChanged(ref _unisonVoices, value); }

    private double _unisonDetune = 20.0;
    public double UnisonDetune { get => _unisonDetune; set => this.RaiseAndSetIfChanged(ref _unisonDetune, value); }

    private double _unisonSpread = 0.8;
    public double UnisonSpread { get => _unisonSpread; set => this.RaiseAndSetIfChanged(ref _unisonSpread, value); }

    public OscillatorViewModel(SynthEngine engine, int index, CommandHistory? history = null)
    {
        _engine  = engine;
        _index   = index;
        _history = history;

        this.WhenAnyValue(x => x.IsEnabled, x => x.Level).Skip(1)
            .Subscribe(_ => ApplyMix());

        this.WhenAnyValue(x => x.SelectedWavetable).Skip(1)
            .Subscribe(v => { if (!_loading) _engine.SelectWavetable(_index, v); });

        this.WhenAnyValue(x => x.SelectedWarp).Skip(1)
            .Subscribe(v => { if (_loading) return; if (Enum.TryParse<WavetableOscillator.WaveWarp>(v, out var warp)) _engine.SetOscillatorWarp(_index, warp); });

        this.WhenAnyValue(x => x.Position, x => x.WarpAmount, x => x.CoarseTune, x => x.FineTune, x => x.Pan).Skip(1)
            .Subscribe(_ => ApplyParams());

        this.WhenAnyValue(x => x.UnisonVoices, x => x.UnisonDetune, x => x.UnisonSpread).Skip(1)
            .Subscribe(_ => ApplyUnison());

        string prefix = $"OSC{index}";
        TrackUndo(x => x.Level,      v => Level      = v, $"{prefix} Level");
        TrackUndo(x => x.Position,   v => Position   = v, $"{prefix} Position");
        TrackUndo(x => x.WarpAmount, v => WarpAmount = v, $"{prefix} Warp Amt");
        TrackUndo(x => x.FineTune,   v => FineTune   = v, $"{prefix} Fine Tune");
        TrackUndo(x => x.Pan,        v => Pan        = v, $"{prefix} Pan");
    }

    private void TrackUndo(
        System.Linq.Expressions.Expression<Func<OscillatorViewModel, double>> prop,
        Action<double> setter, string name)
    {
        double prev = 0;
        this.WhenAnyValue(prop).Subscribe(v => prev = v);
        this.WhenAnyValue(prop).Skip(1).Subscribe(v =>
        {
            var old = prev;
            prev = v;
            if (_loading || _suppressHistory || _history == null) return;
            if (Math.Abs(old - v) < 1e-10) return;
            _history.Execute(new ParameterCommand(name,
                val => { _suppressHistory = true; setter(val); _suppressHistory = false; },
                old, v));
        });
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

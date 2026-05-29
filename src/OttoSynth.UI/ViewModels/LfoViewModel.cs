using System.Reactive.Linq;
using OttoSynth.Core;
using OttoSynth.Core.DSP.Modulation;
using OttoSynth.Core.Preset;
using OttoSynth.UI.Commands;
using ReactiveUI;

namespace OttoSynth.UI.ViewModels;

public class LfoViewModel : ReactiveObject
{
    private readonly SynthEngine _engine;
    private readonly int _index;
    private CommandHistory? _history;
    private bool _loading;
    internal bool _suppressHistory;

    public int Index => _index;
    public string Title => $"LFO {_index}";

    public static IReadOnlyList<string> ShapeLabels { get; } =
        Enum.GetNames<LfoGenerator.LfoShape>().ToList();

    private string _selectedShape = "Sine";
    public string SelectedShape { get => _selectedShape; set => this.RaiseAndSetIfChanged(ref _selectedShape, value); }

    private double _rate = 1.0;
    public double Rate { get => _rate; set => this.RaiseAndSetIfChanged(ref _rate, value); }

    private double _depth = 1.0;
    public double Depth { get => _depth; set => this.RaiseAndSetIfChanged(ref _depth, value); }

    public LfoViewModel(SynthEngine engine, int index, CommandHistory? history = null)
    {
        _engine  = engine;
        _index   = index;
        _history = history;

        this.WhenAnyValue(x => x.SelectedShape, x => x.Rate, x => x.Depth)
            .Skip(1).Subscribe(_ => { if (!_loading) Apply(); });

        string prefix = $"LFO{index}";
        TrackUndo(x => x.Rate,  v => Rate  = v, $"{prefix} Rate");
        TrackUndo(x => x.Depth, v => Depth = v, $"{prefix} Depth");
    }

    private void TrackUndo(
        System.Linq.Expressions.Expression<Func<LfoViewModel, double>> prop,
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

    private void Apply()
    {
        if (Enum.TryParse<LfoGenerator.LfoShape>(SelectedShape, out var shape))
            _engine.SetLfo(_index, shape, Rate, Depth);
    }

    public void ApplyPreset(LfoData data)
    {
        _loading = true;
        try
        {
            SelectedShape = ShapeLabels.Contains(data.Shape) ? data.Shape : "Sine";
            Rate          = data.Rate;
            Depth         = data.Depth;
        }
        finally
        {
            _loading = false;
            Apply();
        }
    }
}

using OttoSynth.Core;
using OttoSynth.Core.DSP.Modulation;
using OttoSynth.Core.Preset;
using OttoSynth.UI.Commands;

namespace OttoSynth.UI.ViewModels;

public class LfoViewModel : ViewModelBase
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
    public string SelectedShape { get => _selectedShape; set => SetField(ref _selectedShape, value); }

    private double _rate = 1.0;
    public double Rate { get => _rate; set => SetField(ref _rate, value); }

    private double _depth = 1.0;
    public double Depth { get => _depth; set => SetField(ref _depth, value); }

    public LfoViewModel(SynthEngine engine, int index, CommandHistory? history = null)
    {
        _engine  = engine;
        _index   = index;
        _history = history;

        PropertyChanged += (_, e) =>
        {
            if (_loading) return;
            switch (e.PropertyName)
            {
                case nameof(SelectedShape):
                case nameof(Rate):
                case nameof(Depth):
                    Apply();
                    break;
            }
        };

        string prefix = $"LFO{index}";
        TrackUndo(nameof(Rate),  () => Rate,  v => Rate  = v, $"{prefix} Rate");
        TrackUndo(nameof(Depth), () => Depth, v => Depth = v, $"{prefix} Depth");
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

using System.Windows;
using OttoSynth.Core;
using OttoSynth.Core.Preset;
using OttoSynth.Core.Voice;

namespace OttoSynth.UI.ViewModels;

public class OscRoutingCellViewModel : ViewModelBase
{
    private readonly SynthEngine _engine;
    private bool _loading;

    public int    Modulator   { get; }
    public int    Carrier     { get; }
    public string Label       { get; }
    public bool   IsDiagonal  => Modulator == Carrier;

    public Visibility ActiveVisibility   => IsDiagonal ? Visibility.Collapsed : Visibility.Visible;
    public Visibility DiagonalVisibility => IsDiagonal ? Visibility.Visible   : Visibility.Collapsed;
    public double     CellOpacity        => IsDiagonal ? 0.3 : 1.0;

    public IReadOnlyList<string> RoutingModes { get; } = Enum.GetNames<SynthVoice.OscRouting>().ToList();

    private string _selectedMode = "Mix";
    public string SelectedMode
    {
        get => _selectedMode;
        set => SetField(ref _selectedMode, value);
    }

    private double _fmDepth = 0.5;
    public double FmDepth
    {
        get => _fmDepth;
        set => SetField(ref _fmDepth, value);
    }

    public OscRoutingCellViewModel(SynthEngine engine, int modulator, int carrier)
    {
        _engine   = engine;
        Modulator = modulator;
        Carrier   = carrier;
        Label     = $"OSC{modulator} → OSC{carrier}";

        if (!IsDiagonal)
        {
            PropertyChanged += (_, e) =>
            {
                if (_loading) return;
                switch (e.PropertyName)
                {
                    case nameof(SelectedMode):
                    case nameof(FmDepth):
                        Apply();
                        break;
                }
            };
        }
    }

    private void Apply()
    {
        if (Enum.TryParse<SynthVoice.OscRouting>(SelectedMode, out var routing))
            _engine.SetOscillatorRouting(Modulator, Carrier, routing, FmDepth);
    }

    public void ApplyPreset(string modeName, double depth)
    {
        _loading = true;
        try
        {
            SelectedMode = modeName;
            FmDepth      = depth;
        }
        finally
        {
            _loading = false;
            Apply();
        }
    }
}

public class OscRoutingMatrixViewModel : ViewModelBase
{
    private const int Size = 3;

    public IReadOnlyList<OscRoutingCellViewModel> AllCells { get; }

    public OscRoutingMatrixViewModel(SynthEngine engine)
    {
        var cells = new List<OscRoutingCellViewModel>(Size * Size);
        for (int mod = 1; mod <= Size; mod++)
            for (int car = 1; car <= Size; car++)
                cells.Add(new OscRoutingCellViewModel(engine, mod, car));
        AllCells = cells;
    }

    public void ApplyPreset(PresetData p)
    {
        if (p.OscRoutingModes == null || p.OscRoutingDepths == null) return;

        foreach (var cell in AllCells)
        {
            if (cell.IsDiagonal) continue;
            int idx = PresetData.RoutingIndex(cell.Modulator, cell.Carrier);
            if (idx >= p.OscRoutingModes.Length) continue;
            double depth = idx < p.OscRoutingDepths.Length ? p.OscRoutingDepths[idx] : 0.5;
            cell.ApplyPreset(p.OscRoutingModes[idx], depth);
        }
    }
}

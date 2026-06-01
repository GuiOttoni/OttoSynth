using OttoSynth.Core;
using OttoSynth.Core.DSP.Filters;
using OttoSynth.Core.Preset;
using OttoSynth.Core.Voice;
using OttoSynth.UI.Commands;

namespace OttoSynth.UI.ViewModels;

public class FilterViewModel : ViewModelBase
{
    private readonly SynthEngine _engine;
    private CommandHistory? _history;
    private bool _loading;
    internal bool _suppressHistory;

    public static IReadOnlyList<string> FilterModeLabels { get; } =
    [
        "LP 12dB", "HP 12dB", "BP 12dB", "Notch", "AllPass", "Peak",
        "Moog 24dB", "K35 LP", "K35 HP", "Comb +", "Comb -",
    ];

    private static readonly Dictionary<string, StateVariableFilter.FilterMode> LabelToMode = new()
    {
        ["LP 12dB"]  = StateVariableFilter.FilterMode.LowPass,
        ["HP 12dB"]  = StateVariableFilter.FilterMode.HighPass,
        ["BP 12dB"]  = StateVariableFilter.FilterMode.BandPass,
        ["Notch"]    = StateVariableFilter.FilterMode.Notch,
        ["AllPass"]  = StateVariableFilter.FilterMode.AllPass,
        ["Peak"]     = StateVariableFilter.FilterMode.Peak,
        ["Moog 24dB"]= StateVariableFilter.FilterMode.MoogLadder,
        ["K35 LP"]   = StateVariableFilter.FilterMode.K35LP,
        ["K35 HP"]   = StateVariableFilter.FilterMode.K35HP,
        ["Comb +"]   = StateVariableFilter.FilterMode.CombPositive,
        ["Comb -"]   = StateVariableFilter.FilterMode.CombNegative,
    };

    private static readonly Dictionary<string, string> LegacyMap = new()
    {
        ["LP"] = "LP 12dB", ["HP"] = "HP 12dB", ["BP"] = "BP 12dB", ["Notch"] = "Notch",
    };

    public static IReadOnlyList<string> GlideModeLabels { get; } =
        Enum.GetNames<SynthVoice.GlideMode>().ToList();

    public static IReadOnlyList<string> FilterRoutingLabels { get; } =
        Enum.GetNames<SynthVoice.FilterRouting>().ToList();

    // Filter 1
    private string _filter1ModeLabel = "LP 12dB";
    public string Filter1ModeLabel { get => _filter1ModeLabel; set => SetField(ref _filter1ModeLabel, value); }

    private double _filter1Cutoff = 20000;
    public double Filter1Cutoff { get => _filter1Cutoff; set => SetField(ref _filter1Cutoff, value); }

    private double _filter1Resonance = 0;
    public double Filter1Resonance { get => _filter1Resonance; set => SetField(ref _filter1Resonance, value); }

    private double _filter1Drive = 0;
    public double Filter1Drive { get => _filter1Drive; set => SetField(ref _filter1Drive, value); }

    private bool _filter1Is24dB = false;
    public bool Filter1Is24dB { get => _filter1Is24dB; set => SetField(ref _filter1Is24dB, value); }

    private double _filterEnvAmount = 0;
    public double FilterEnvAmount { get => _filterEnvAmount; set => SetField(ref _filterEnvAmount, value); }

    // Filter 1 Formant
    private double _filter1FormantVowel = 0;
    public double Filter1FormantVowel { get => _filter1FormantVowel; set => SetField(ref _filter1FormantVowel, value); }

    private double _filter1FormantShift = 1.0;
    public double Filter1FormantShift { get => _filter1FormantShift; set => SetField(ref _filter1FormantShift, value); }

    // Filter 2
    private string _filter2ModeLabel = "LP 12dB";
    public string Filter2ModeLabel { get => _filter2ModeLabel; set => SetField(ref _filter2ModeLabel, value); }

    private double _filter2Cutoff = 20000;
    public double Filter2Cutoff { get => _filter2Cutoff; set => SetField(ref _filter2Cutoff, value); }

    private double _filter2Resonance = 0;
    public double Filter2Resonance { get => _filter2Resonance; set => SetField(ref _filter2Resonance, value); }

    private double _filter2Drive = 0;
    public double Filter2Drive { get => _filter2Drive; set => SetField(ref _filter2Drive, value); }

    private bool _filter2Is24dB = false;
    public bool Filter2Is24dB { get => _filter2Is24dB; set => SetField(ref _filter2Is24dB, value); }

    // Filter 2 Formant
    private double _filter2FormantVowel = 0;
    public double Filter2FormantVowel { get => _filter2FormantVowel; set => SetField(ref _filter2FormantVowel, value); }

    private double _filter2FormantShift = 1.0;
    public double Filter2FormantShift { get => _filter2FormantShift; set => SetField(ref _filter2FormantShift, value); }

    // Routing
    private string _filterRoutingLabel = "Serial";
    public string FilterRoutingLabel { get => _filterRoutingLabel; set => SetField(ref _filterRoutingLabel, value); }

    // Glide
    private double _glideTime = 0;
    public double GlideTime { get => _glideTime; set => SetField(ref _glideTime, value); }

    private string _glideModeLabel = "Off";
    public string GlideModeLabel { get => _glideModeLabel; set => SetField(ref _glideModeLabel, value); }

    public FilterViewModel(SynthEngine engine, CommandHistory? history = null)
    {
        _engine  = engine;
        _history = history;

        PropertyChanged += (_, e) =>
        {
            if (_loading) return;
            switch (e.PropertyName)
            {
                case nameof(Filter1ModeLabel):
                case nameof(Filter1Cutoff):
                case nameof(Filter1Resonance):
                case nameof(Filter1Drive):
                case nameof(Filter1Is24dB):
                    ApplyFilter1();
                    break;
                case nameof(Filter1FormantVowel):
                case nameof(Filter1FormantShift):
                    _engine.SetFormantParams(1, Filter1FormantVowel, Filter1FormantShift);
                    break;
                case nameof(FilterEnvAmount):
                    _engine.SetFilterEnvAmount(FilterEnvAmount);
                    break;
                case nameof(Filter2ModeLabel):
                case nameof(Filter2Cutoff):
                case nameof(Filter2Resonance):
                case nameof(Filter2Drive):
                case nameof(Filter2Is24dB):
                    ApplyFilter2();
                    break;
                case nameof(Filter2FormantVowel):
                case nameof(Filter2FormantShift):
                    _engine.SetFormantParams(2, Filter2FormantVowel, Filter2FormantShift);
                    break;
                case nameof(FilterRoutingLabel):
                    if (Enum.TryParse<SynthVoice.FilterRouting>(FilterRoutingLabel, out var routing))
                        _engine.SetFilterRouting(routing);
                    break;
                case nameof(GlideTime):
                case nameof(GlideModeLabel):
                    ApplyGlide();
                    break;
            }
        };

        TrackUndo(nameof(Filter1Cutoff),    () => Filter1Cutoff,    v => Filter1Cutoff    = v, "F1 Cutoff");
        TrackUndo(nameof(Filter1Resonance), () => Filter1Resonance, v => Filter1Resonance = v, "F1 Resonance");
        TrackUndo(nameof(Filter1Drive),     () => Filter1Drive,     v => Filter1Drive     = v, "F1 Drive");
        TrackUndo(nameof(Filter2Cutoff),    () => Filter2Cutoff,    v => Filter2Cutoff    = v, "F2 Cutoff");
        TrackUndo(nameof(Filter2Resonance), () => Filter2Resonance, v => Filter2Resonance = v, "F2 Resonance");
        TrackUndo(nameof(Filter2Drive),     () => Filter2Drive,     v => Filter2Drive     = v, "F2 Drive");
        TrackUndo(nameof(FilterEnvAmount),  () => FilterEnvAmount,  v => FilterEnvAmount  = v, "Filter Env Amount");
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

    private void ApplyFilter1()
    {
        if (!LabelToMode.TryGetValue(Filter1ModeLabel, out var mode)) return;
        _engine.SetFilter(1, mode, Filter1Cutoff, Filter1Resonance, Filter1Drive, Filter1Is24dB);
    }

    private void ApplyFilter2()
    {
        if (!LabelToMode.TryGetValue(Filter2ModeLabel, out var mode)) return;
        _engine.SetFilter(2, mode, Filter2Cutoff, Filter2Resonance, Filter2Drive, Filter2Is24dB);
    }

    private void ApplyGlide()
    {
        if (Enum.TryParse<SynthVoice.GlideMode>(GlideModeLabel, out var mode))
            _engine.SetPortamento(GlideTime, mode);
    }

    public void ApplyPreset(PresetData p)
    {
        _loading = true;
        try
        {
            string f1Label = LegacyMap.TryGetValue(p.Filter1.Mode, out var m1) ? m1 : p.Filter1.Mode;
            Filter1ModeLabel  = FilterModeLabels.Contains(f1Label) ? f1Label : "LP 12dB";
            Filter1Cutoff     = p.Filter1.Cutoff;
            Filter1Resonance  = p.Filter1.Resonance;
            Filter1Drive      = p.Filter1.Drive;
            Filter1Is24dB     = p.Filter1.Is24dB;
            FilterEnvAmount   = p.FilterEnvAmount;

            string f2Label = LegacyMap.TryGetValue(p.Filter2.Mode, out var m2) ? m2 : p.Filter2.Mode;
            Filter2ModeLabel  = FilterModeLabels.Contains(f2Label) ? f2Label : "LP 12dB";
            Filter2Cutoff     = p.Filter2.Cutoff;
            Filter2Resonance  = p.Filter2.Resonance;
            Filter2Drive      = p.Filter2.Drive;
            Filter2Is24dB     = p.Filter2.Is24dB;

            FilterRoutingLabel = FilterRoutingLabels.Contains(p.FilterRouting) ? p.FilterRouting : "Serial";
            GlideTime          = p.GlideTime;
            GlideModeLabel     = GlideModeLabels.Contains(p.GlideMode) ? p.GlideMode : "Off";
        }
        finally
        {
            _loading = false;
            ApplyFilter1();
            ApplyFilter2();
            _engine.SetFilterEnvAmount(FilterEnvAmount);
            if (Enum.TryParse<SynthVoice.FilterRouting>(FilterRoutingLabel, out var r))
                _engine.SetFilterRouting(r);
            ApplyGlide();
        }
    }
}

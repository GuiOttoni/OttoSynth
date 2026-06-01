using OttoSynth.Core;
using OttoSynth.Core.Preset;
using OttoSynth.UI.Commands;

namespace OttoSynth.UI.ViewModels;

public class EnvelopeViewModel : ViewModelBase
{
    private readonly SynthEngine _engine;
    private CommandHistory? _history;
    private bool _loading;
    internal bool _suppressHistory;

    private double _ampAttack = 0.01;
    public double AmpAttack { get => _ampAttack; set => SetField(ref _ampAttack, value); }

    private double _ampDecay = 0.3;
    public double AmpDecay { get => _ampDecay; set => SetField(ref _ampDecay, value); }

    private double _ampSustain = 0.7;
    public double AmpSustain { get => _ampSustain; set => SetField(ref _ampSustain, value); }

    private double _ampRelease = 0.5;
    public double AmpRelease { get => _ampRelease; set => SetField(ref _ampRelease, value); }

    private double _filterAttack = 0.01;
    public double FilterAttack { get => _filterAttack; set => SetField(ref _filterAttack, value); }

    private double _filterDecay = 0.3;
    public double FilterDecay { get => _filterDecay; set => SetField(ref _filterDecay, value); }

    private double _filterSustain = 0.7;
    public double FilterSustain { get => _filterSustain; set => SetField(ref _filterSustain, value); }

    private double _filterRelease = 0.5;
    public double FilterRelease { get => _filterRelease; set => SetField(ref _filterRelease, value); }

    private double _freeAttack = 0.01;
    public double FreeAttack { get => _freeAttack; set => SetField(ref _freeAttack, value); }

    private double _freeDecay = 0.3;
    public double FreeDecay { get => _freeDecay; set => SetField(ref _freeDecay, value); }

    private double _freeSustain = 0.7;
    public double FreeSustain { get => _freeSustain; set => SetField(ref _freeSustain, value); }

    private double _freeRelease = 0.5;
    public double FreeRelease { get => _freeRelease; set => SetField(ref _freeRelease, value); }

    public EnvelopeViewModel(SynthEngine engine, CommandHistory? history = null)
    {
        _engine  = engine;
        _history = history;

        PropertyChanged += (_, e) =>
        {
            if (_loading) return;
            switch (e.PropertyName)
            {
                case nameof(AmpAttack):
                case nameof(AmpDecay):
                case nameof(AmpSustain):
                case nameof(AmpRelease):
                    ApplyAmpEnv();
                    break;
                case nameof(FilterAttack):
                case nameof(FilterDecay):
                case nameof(FilterSustain):
                case nameof(FilterRelease):
                    ApplyFilterEnv();
                    break;
                case nameof(FreeAttack):
                case nameof(FreeDecay):
                case nameof(FreeSustain):
                case nameof(FreeRelease):
                    ApplyFreeEnv();
                    break;
            }
        };

        TrackUndo(nameof(AmpAttack),     () => AmpAttack,     v => AmpAttack     = v, "AmpEnv Attack");
        TrackUndo(nameof(AmpDecay),      () => AmpDecay,      v => AmpDecay      = v, "AmpEnv Decay");
        TrackUndo(nameof(AmpSustain),    () => AmpSustain,    v => AmpSustain    = v, "AmpEnv Sustain");
        TrackUndo(nameof(AmpRelease),    () => AmpRelease,    v => AmpRelease    = v, "AmpEnv Release");
        TrackUndo(nameof(FilterAttack),  () => FilterAttack,  v => FilterAttack  = v, "FilterEnv Attack");
        TrackUndo(nameof(FilterDecay),   () => FilterDecay,   v => FilterDecay   = v, "FilterEnv Decay");
        TrackUndo(nameof(FilterSustain), () => FilterSustain, v => FilterSustain = v, "FilterEnv Sustain");
        TrackUndo(nameof(FilterRelease), () => FilterRelease, v => FilterRelease = v, "FilterEnv Release");
        TrackUndo(nameof(FreeAttack),    () => FreeAttack,    v => FreeAttack    = v, "FreeEnv Attack");
        TrackUndo(nameof(FreeDecay),     () => FreeDecay,     v => FreeDecay     = v, "FreeEnv Decay");
        TrackUndo(nameof(FreeSustain),   () => FreeSustain,   v => FreeSustain   = v, "FreeEnv Sustain");
        TrackUndo(nameof(FreeRelease),   () => FreeRelease,   v => FreeRelease   = v, "FreeEnv Release");
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

    private void ApplyAmpEnv()
        => _engine.SetEnvelope(AmpAttack, AmpDecay, AmpSustain, AmpRelease);

    private void ApplyFilterEnv()
        => _engine.SetFilterEnvelope(FilterAttack, FilterDecay, FilterSustain, FilterRelease);

    private void ApplyFreeEnv()
        => _engine.SetFreeEnvelope(FreeAttack, FreeDecay, FreeSustain, FreeRelease);

    public void ApplyPreset(PresetData p)
    {
        _loading = true;
        try
        {
            AmpAttack     = p.EnvAmp.Attack;
            AmpDecay      = p.EnvAmp.Decay;
            AmpSustain    = p.EnvAmp.Sustain;
            AmpRelease    = p.EnvAmp.Release;

            FilterAttack  = p.EnvFilter.Attack;
            FilterDecay   = p.EnvFilter.Decay;
            FilterSustain = p.EnvFilter.Sustain;
            FilterRelease = p.EnvFilter.Release;

            FreeAttack  = p.EnvFree.Attack;
            FreeDecay   = p.EnvFree.Decay;
            FreeSustain = p.EnvFree.Sustain;
            FreeRelease = p.EnvFree.Release;
        }
        finally
        {
            _loading = false;
            ApplyAmpEnv();
            ApplyFilterEnv();
            ApplyFreeEnv();
        }
    }
}

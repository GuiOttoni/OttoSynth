using System.Reactive.Linq;
using OttoSynth.Core;
using OttoSynth.Core.Preset;
using OttoSynth.UI.Commands;
using ReactiveUI;

namespace OttoSynth.UI.ViewModels;

public class EnvelopeViewModel : ReactiveObject
{
    private readonly SynthEngine _engine;
    private CommandHistory? _history;
    private bool _loading;
    internal bool _suppressHistory;

    private double _ampAttack = 0.01;
    public double AmpAttack { get => _ampAttack; set => this.RaiseAndSetIfChanged(ref _ampAttack, value); }

    private double _ampDecay = 0.3;
    public double AmpDecay { get => _ampDecay; set => this.RaiseAndSetIfChanged(ref _ampDecay, value); }

    private double _ampSustain = 0.7;
    public double AmpSustain { get => _ampSustain; set => this.RaiseAndSetIfChanged(ref _ampSustain, value); }

    private double _ampRelease = 0.5;
    public double AmpRelease { get => _ampRelease; set => this.RaiseAndSetIfChanged(ref _ampRelease, value); }

    private double _filterAttack = 0.01;
    public double FilterAttack { get => _filterAttack; set => this.RaiseAndSetIfChanged(ref _filterAttack, value); }

    private double _filterDecay = 0.3;
    public double FilterDecay { get => _filterDecay; set => this.RaiseAndSetIfChanged(ref _filterDecay, value); }

    private double _filterSustain = 0.7;
    public double FilterSustain { get => _filterSustain; set => this.RaiseAndSetIfChanged(ref _filterSustain, value); }

    private double _filterRelease = 0.5;
    public double FilterRelease { get => _filterRelease; set => this.RaiseAndSetIfChanged(ref _filterRelease, value); }

    private double _freeAttack = 0.01;
    public double FreeAttack { get => _freeAttack; set => this.RaiseAndSetIfChanged(ref _freeAttack, value); }

    private double _freeDecay = 0.3;
    public double FreeDecay { get => _freeDecay; set => this.RaiseAndSetIfChanged(ref _freeDecay, value); }

    private double _freeSustain = 0.7;
    public double FreeSustain { get => _freeSustain; set => this.RaiseAndSetIfChanged(ref _freeSustain, value); }

    private double _freeRelease = 0.5;
    public double FreeRelease { get => _freeRelease; set => this.RaiseAndSetIfChanged(ref _freeRelease, value); }

    public EnvelopeViewModel(SynthEngine engine, CommandHistory? history = null)
    {
        _engine  = engine;
        _history = history;

        this.WhenAnyValue(x => x.AmpAttack, x => x.AmpDecay, x => x.AmpSustain, x => x.AmpRelease)
            .Skip(1).Subscribe(_ => { if (!_loading) ApplyAmpEnv(); });

        this.WhenAnyValue(x => x.FilterAttack, x => x.FilterDecay, x => x.FilterSustain, x => x.FilterRelease)
            .Skip(1).Subscribe(_ => { if (!_loading) ApplyFilterEnv(); });

        this.WhenAnyValue(x => x.FreeAttack, x => x.FreeDecay, x => x.FreeSustain, x => x.FreeRelease)
            .Skip(1).Subscribe(_ => { if (!_loading) ApplyFreeEnv(); });

        TrackUndo(x => x.AmpAttack,     v => AmpAttack     = v, "AmpEnv Attack");
        TrackUndo(x => x.AmpDecay,      v => AmpDecay      = v, "AmpEnv Decay");
        TrackUndo(x => x.AmpSustain,    v => AmpSustain    = v, "AmpEnv Sustain");
        TrackUndo(x => x.AmpRelease,    v => AmpRelease    = v, "AmpEnv Release");
        TrackUndo(x => x.FilterAttack,  v => FilterAttack  = v, "FilterEnv Attack");
        TrackUndo(x => x.FilterDecay,   v => FilterDecay   = v, "FilterEnv Decay");
        TrackUndo(x => x.FilterSustain, v => FilterSustain = v, "FilterEnv Sustain");
        TrackUndo(x => x.FilterRelease, v => FilterRelease = v, "FilterEnv Release");
        TrackUndo(x => x.FreeAttack,    v => FreeAttack    = v, "FreeEnv Attack");
        TrackUndo(x => x.FreeDecay,     v => FreeDecay     = v, "FreeEnv Decay");
        TrackUndo(x => x.FreeSustain,   v => FreeSustain   = v, "FreeEnv Sustain");
        TrackUndo(x => x.FreeRelease,   v => FreeRelease   = v, "FreeEnv Release");
    }

    private void TrackUndo(
        System.Linq.Expressions.Expression<Func<EnvelopeViewModel, double>> prop,
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

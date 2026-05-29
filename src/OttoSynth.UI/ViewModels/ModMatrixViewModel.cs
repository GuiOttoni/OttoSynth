using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive;
using OttoSynth.Core;
using OttoSynth.Core.DSP.Modulation;
using ReactiveUI;

namespace OttoSynth.UI.ViewModels;

public class ModRouteViewModel : ReactiveObject
{
    public int RouteIndex { get; set; }

    private string _source = "None";
    public string Source { get => _source; set => this.RaiseAndSetIfChanged(ref _source, value); }

    private string _destination = "None";
    public string Destination { get => _destination; set => this.RaiseAndSetIfChanged(ref _destination, value); }

    private double _amount = 0;
    public double Amount { get => _amount; set => this.RaiseAndSetIfChanged(ref _amount, value); }
}

public sealed class ModMatrixViewModel : ReactiveObject
{
    private readonly SynthEngine _engine;
    private readonly Dictionary<ModRouteViewModel, CompositeDisposable> _routeDisposables = new();

    public static IReadOnlyList<string> Sources { get; } =
        Enum.GetNames<ModSource>().ToList();

    public static IReadOnlyList<string> Destinations { get; } =
        Enum.GetNames<ModDestination>().ToList();

    public ObservableCollection<ModRouteViewModel> Routes { get; } = [];

    public ReactiveCommand<Unit, Unit> AddRouteCommand { get; }
    public ReactiveCommand<ModRouteViewModel, Unit> RemoveRouteCommand { get; }

    public ModMatrixViewModel(SynthEngine engine)
    {
        _engine = engine;
        AddRouteCommand    = ReactiveCommand.Create(AddRoute);
        RemoveRouteCommand = ReactiveCommand.Create<ModRouteViewModel>(RemoveRoute);
    }

    private void AddRoute()
    {
        int idx = _engine.AddModRoute(ModSource.Lfo1, ModDestination.Filter1Cutoff, 0.0);
        if (idx < 0) return;
        var vm = new ModRouteViewModel
        {
            RouteIndex  = idx,
            Source      = ModSource.Lfo1.ToString(),
            Destination = ModDestination.Filter1Cutoff.ToString(),
            Amount      = 0.0
        };
        WireRoute(vm);
        Routes.Add(vm);
    }

    private void RemoveRoute(ModRouteViewModel route)
    {
        if (_routeDisposables.TryGetValue(route, out var d))
        {
            d.Dispose();
            _routeDisposables.Remove(route);
        }
        _engine.RemoveModRoute(route.RouteIndex);
        Routes.Remove(route);
        for (int i = 0; i < Routes.Count; i++)
            Routes[i].RouteIndex = i;
    }

    public void Refresh()
    {
        foreach (var d in _routeDisposables.Values) d.Dispose();
        _routeDisposables.Clear();
        Routes.Clear();

        var voices = _engine.VoiceManager.Voices;
        if (voices.Length == 0) return;

        int i = 0;
        foreach (var r in voices[0].ModMatrix.Routes)
        {
            var vm = new ModRouteViewModel
            {
                RouteIndex  = i++,
                Source      = r.Source.ToString(),
                Destination = r.Destination.ToString(),
                Amount      = r.Amount
            };
            WireRoute(vm);
            Routes.Add(vm);
        }
    }

    private void WireRoute(ModRouteViewModel vm)
    {
        var disposables = new CompositeDisposable();
        _routeDisposables[vm] = disposables;

        vm.WhenAnyValue(x => x.Source, x => x.Destination).Skip(1)
            .Subscribe(_ =>
            {
                if (Enum.TryParse<ModSource>(vm.Source, out var src) &&
                    Enum.TryParse<ModDestination>(vm.Destination, out var dst))
                {
                    _engine.RemoveModRoute(vm.RouteIndex);
                    int newIdx = _engine.AddModRoute(src, dst, vm.Amount);
                    vm.RouteIndex = newIdx;
                    for (int i = Routes.IndexOf(vm) + 1; i < Routes.Count; i++)
                        Routes[i].RouteIndex = i;
                }
            }).DisposeWith(disposables);

        vm.WhenAnyValue(x => x.Amount).Skip(1)
            .Subscribe(v => _engine.SetModRouteAmount(vm.RouteIndex, v))
            .DisposeWith(disposables);
    }
}

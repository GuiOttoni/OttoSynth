using System.Collections.ObjectModel;
using System.Windows.Input;
using OttoSynth.Core;
using OttoSynth.Core.DSP.Modulation;

namespace OttoSynth.UI.ViewModels;

public class ModRouteViewModel : ViewModelBase
{
    public int RouteIndex { get; set; }

    private string _source = "None";
    public string Source { get => _source; set => SetField(ref _source, value); }

    private string _destination = "None";
    public string Destination { get => _destination; set => SetField(ref _destination, value); }

    private double _amount = 0;
    public double Amount { get => _amount; set => SetField(ref _amount, value); }
}

public sealed class ModMatrixViewModel : ViewModelBase
{
    private readonly SynthEngine _engine;

    public static IReadOnlyList<string> Sources { get; } =
        Enum.GetNames<ModSource>().ToList();

    public static IReadOnlyList<string> Destinations { get; } =
        Enum.GetNames<ModDestination>().ToList();

    public ObservableCollection<ModRouteViewModel> Routes { get; } = [];

    public ICommand AddRouteCommand { get; }
    public ICommand RemoveRouteCommand { get; }

    public ModMatrixViewModel(SynthEngine engine)
    {
        _engine = engine;
        AddRouteCommand    = new DelegateCommand(_ => AddRoute());
        RemoveRouteCommand = new DelegateCommand(p => { if (p is ModRouteViewModel vm) RemoveRoute(vm); });
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
        _engine.RemoveModRoute(route.RouteIndex);
        Routes.Remove(route);
        for (int i = 0; i < Routes.Count; i++)
            Routes[i].RouteIndex = i;
    }

    public void Refresh()
    {
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
        vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(ModRouteViewModel.Source):
                case nameof(ModRouteViewModel.Destination):
                    if (Enum.TryParse<ModSource>(vm.Source, out var src) &&
                        Enum.TryParse<ModDestination>(vm.Destination, out var dst))
                    {
                        _engine.RemoveModRoute(vm.RouteIndex);
                        int newIdx = _engine.AddModRoute(src, dst, vm.Amount);
                        vm.RouteIndex = newIdx;
                        for (int i = Routes.IndexOf(vm) + 1; i < Routes.Count; i++)
                            Routes[i].RouteIndex = i;
                    }
                    break;
                case nameof(ModRouteViewModel.Amount):
                    _engine.SetModRouteAmount(vm.RouteIndex, vm.Amount);
                    break;
            }
        };
    }
}

internal sealed class DelegateCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public event EventHandler? CanExecuteChanged;

    public DelegateCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute    = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter)    => _execute(parameter);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

using System.Collections.ObjectModel;
using System.Reactive.Linq;
using OttoSynth.Core.Preset;
using OttoSynth.UI.Services;
using ReactiveUI;
using System.Reactive;

namespace OttoSynth.UI.ViewModels;

public sealed class PresetBrowserViewModel : ReactiveObject
{
    private static readonly IReadOnlyList<string> AllCategories =
        ["All", "Favorites", "Bass", "Lead", "Synth", "Pad", "Pluck", "Keys", "FX", "Strings", "Ambient", "Init"];

    private static readonly Dictionary<string, int> CategoryOrder = new()
    {
        ["Init"] = 0, ["Bass"] = 1, ["Lead"] = 2, ["Synth"] = 3, ["Pad"] = 4,
        ["Pluck"] = 5, ["Keys"] = 6, ["FX"] = 7, ["Strings"] = 8, ["Ambient"] = 9
    };

    private List<PresetEntryViewModel> _allEntries = [];

    public IReadOnlyList<string> Categories => AllCategories;

    public ObservableCollection<PresetEntryViewModel> FilteredPresets { get; } = [];

    private string _selectedCategory = "All";
    public string SelectedCategory
    {
        get => _selectedCategory;
        set { this.RaiseAndSetIfChanged(ref _selectedCategory, value); UpdateFilter(); }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set { this.RaiseAndSetIfChanged(ref _searchText, value); UpdateFilter(); }
    }

    private PresetEntryViewModel? _selectedPreset;
    public PresetEntryViewModel? SelectedPreset
    {
        get => _selectedPreset;
        set => this.RaiseAndSetIfChanged(ref _selectedPreset, value);
    }

    public ReactiveCommand<Unit, Unit> LoadSelectedCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleFavoriteCommand { get; }

    public event Action<PresetData>? PresetLoadRequested;

    public PresetBrowserViewModel()
    {
        var hasSelection = this.WhenAnyValue(x => x.SelectedPreset).Select(p => p is not null);

        LoadSelectedCommand = ReactiveCommand.Create(() =>
        {
            if (SelectedPreset is not null)
                PresetLoadRequested?.Invoke(SelectedPreset.Preset);
        }, hasSelection);

        ToggleFavoriteCommand = ReactiveCommand.Create(() =>
        {
            if (SelectedPreset is not null)
            {
                SelectedPreset.IsFavorite = !SelectedPreset.IsFavorite;
                if (_selectedCategory == "Favorites")
                    UpdateFilter();
            }
        }, hasSelection);
    }

    public void Refresh(IEnumerable<PresetData> factory, IEnumerable<PresetData>? user = null)
    {
        _allEntries = factory.Select(p => new PresetEntryViewModel(p, false))
            .Concat((user ?? []).Select(p => new PresetEntryViewModel(p, true)))
            .ToList();
        UpdateFilter();
    }

    private void UpdateFilter()
    {
        var query = _searchText.Trim();
        var cat   = _selectedCategory;

        var filtered = _allEntries
            .Where(e =>
            {
                if (cat == "Favorites" && !e.IsFavorite) return false;
                if (cat != "All" && cat != "Favorites" && e.Category != cat) return false;
                if (query.Length > 0 &&
                    !e.Name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                    !e.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
                    return false;
                return true;
            })
            .OrderBy(e => CategoryOrder.TryGetValue(e.Category, out int i) ? i : 99)
            .ThenBy(e => e.Name);

        FilteredPresets.Clear();
        foreach (var e in filtered)
            FilteredPresets.Add(e);

        if (SelectedPreset is not null && !FilteredPresets.Contains(SelectedPreset))
            SelectedPreset = FilteredPresets.Count > 0 ? FilteredPresets[0] : null;
        else if (SelectedPreset is null && FilteredPresets.Count > 0)
            SelectedPreset = FilteredPresets[0];
    }
}

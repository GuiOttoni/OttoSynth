using OttoSynth.Core.Preset;
using OttoSynth.UI.Services;

namespace OttoSynth.UI.ViewModels;

public class PresetEntryViewModel : ViewModelBase
{
    public PresetData Preset { get; }
    public string Name     => Preset.Name;
    public string Category => Preset.Category ?? "Init";
    public bool   IsUser   { get; }

    private bool _isFavorite;
    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            SetField(ref _isFavorite, value);
            FavoritesStore.SetFavorite(Name, value);
        }
    }

    public PresetEntryViewModel(PresetData preset, bool isUser = false)
    {
        Preset     = preset;
        IsUser     = isUser;
        _isFavorite = FavoritesStore.IsFavorite(preset.Name);
    }
}

using System.IO;
using System.Text.Json;

namespace OttoSynth.UI.Services;

public static class FavoritesStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OttoSynth", "favorites.json");

    private static HashSet<string>? _cache;

    private static HashSet<string> Cache => _cache ??= Load();

    public static bool IsFavorite(string presetName) => Cache.Contains(presetName);

    public static void Toggle(string presetName)
    {
        if (!Cache.Remove(presetName))
            Cache.Add(presetName);
        Save();
    }

    public static void SetFavorite(string presetName, bool favorite)
    {
        if (favorite) Cache.Add(presetName); else Cache.Remove(presetName);
        Save();
    }

    private static HashSet<string> Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var list = JsonSerializer.Deserialize<List<string>>(json);
                return list != null ? new HashSet<string>(list) : new HashSet<string>();
            }
        }
        catch { }
        return new HashSet<string>();
    }

    private static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Cache.ToList()));
        }
        catch { }
    }
}

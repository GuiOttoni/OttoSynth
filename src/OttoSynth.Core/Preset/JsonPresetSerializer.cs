using System.Text.Json;

namespace OttoSynth.Core.Preset;

public sealed class JsonPresetSerializer : IPresetSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string FileExtension => ".otto";

    public string Serialize(PresetData preset) =>
        JsonSerializer.Serialize(preset, Options);

    public PresetData Deserialize(string data) =>
        JsonSerializer.Deserialize<PresetData>(data, Options)
        ?? throw new InvalidOperationException("Preset data is null or empty.");
}

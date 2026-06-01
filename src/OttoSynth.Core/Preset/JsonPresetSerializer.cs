using System.Text.Json;
using System.Text.Json.Serialization;

namespace OttoSynth.Core.Preset;

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> for <see cref="PresetData"/>.
/// Required by NativeAOT (CLAP plugin) so that <see cref="JsonSerializer"/> does not
/// fall back on runtime reflection / code generation for the preset DTO graph.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(PresetData))]
internal sealed partial class PresetJsonContext : JsonSerializerContext { }

public sealed class JsonPresetSerializer : IPresetSerializer
{
    public string FileExtension => ".otto";

    public string Serialize(PresetData preset) =>
        JsonSerializer.Serialize(preset, PresetJsonContext.Default.PresetData);

    public PresetData Deserialize(string data) =>
        JsonSerializer.Deserialize(data, PresetJsonContext.Default.PresetData)
        ?? throw new InvalidOperationException("Preset data is null or empty.");
}

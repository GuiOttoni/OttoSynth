using System;
using System.IO;
using System.Text.Json;

namespace OttoSynth.Standalone.Models;

public sealed class AudioSettings
{
    public int SampleRate { get; set; } = 44100;
    public int BufferSize { get; set; } = 256;

    private static readonly string FilePath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OttoSynth", "audio_settings.json");

    public static AudioSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AudioSettings>(json) ?? new AudioSettings();
            }
        }
        catch { /* fall through to defaults */ }
        return new AudioSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

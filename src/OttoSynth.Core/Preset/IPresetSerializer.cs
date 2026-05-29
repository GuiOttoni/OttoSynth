namespace OttoSynth.Core.Preset;

public interface IPresetSerializer
{
    string FileExtension { get; }
    string Serialize(PresetData preset);
    PresetData Deserialize(string data);
}

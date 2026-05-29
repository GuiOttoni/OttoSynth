using OttoSynth.Core.Preset;

namespace OttoSynth.UI.Commands;

public sealed class PresetLoadCommand : ISynthCommand
{
    private readonly Action<PresetData> _apply;
    private readonly PresetData _oldState;
    private readonly PresetData _newState;

    public string Description { get; }

    public PresetLoadCommand(string presetName, Action<PresetData> apply, PresetData oldState, PresetData newState)
    {
        Description = $"Load Preset \"{presetName}\"";
        _apply      = apply;
        _oldState   = oldState;
        _newState   = newState;
    }

    public void Execute() => _apply(_newState);
    public void Undo()    => _apply(_oldState);
}

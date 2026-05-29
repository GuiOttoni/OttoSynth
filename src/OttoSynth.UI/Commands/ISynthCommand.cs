namespace OttoSynth.UI.Commands;

public interface ISynthCommand
{
    string Description { get; }
    void Execute();
    void Undo();
}

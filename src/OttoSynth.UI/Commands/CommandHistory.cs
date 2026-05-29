namespace OttoSynth.UI.Commands;

public sealed class CommandHistory
{
    private const int MaxSteps = 100;

    private readonly List<ISynthCommand> _undoStack = new(MaxSteps + 1);
    private readonly List<ISynthCommand> _redoStack = new(MaxSteps + 1);

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public string? UndoDescription => CanUndo ? _undoStack[^1].Description : null;
    public string? RedoDescription => CanRedo ? _redoStack[^1].Description : null;

    public void Execute(ISynthCommand cmd)
    {
        cmd.Execute();
        _undoStack.Add(cmd);
        if (_undoStack.Count > MaxSteps)
            _undoStack.RemoveAt(0);
        _redoStack.Clear();
    }

    public void Undo()
    {
        if (_undoStack.Count == 0) return;
        var cmd = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        cmd.Undo();
        _redoStack.Add(cmd);
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        var cmd = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        cmd.Execute();
        _undoStack.Add(cmd);
    }
}

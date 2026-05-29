namespace OttoSynth.UI.Commands;

public sealed class ParameterCommand : ISynthCommand
{
    private readonly Action<double> _apply;
    private readonly double _oldValue;
    private readonly double _newValue;

    public string Description { get; }

    public ParameterCommand(string name, Action<double> apply, double oldValue, double newValue)
    {
        Description = $"{name}: {FormatValue(oldValue)} → {FormatValue(newValue)}";
        _apply      = apply;
        _oldValue   = oldValue;
        _newValue   = newValue;
    }

    public void Execute() => _apply(_newValue);
    public void Undo()    => _apply(_oldValue);

    private static string FormatValue(double v)
        => Math.Abs(v) >= 1000 ? $"{v:F0}" : $"{v:F3}";
}

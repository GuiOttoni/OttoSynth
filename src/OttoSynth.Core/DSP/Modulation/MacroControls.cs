using System;

namespace OttoSynth.Core.DSP.Modulation;

/// <summary>
/// 4 global macro controls that can be used as modulation sources.
/// Each macro is a simple 0..1 value controllable by the UI or MIDI CC.
/// </summary>
public sealed class MacroControls
{
    private readonly double[] _values;

    /// <summary>Number of macro controls.</summary>
    public const int Count = 4;

    public MacroControls()
    {
        _values = new double[Count];
    }

    /// <summary>Gets or sets a macro value (0..1). Index: 0-3.</summary>
    public double this[int index]
    {
        get => (index >= 0 && index < Count) ? _values[index] : 0.0;
        set
        {
            if (index >= 0 && index < Count)
                _values[index] = Math.Clamp(value, 0.0, 1.0);
        }
    }

    /// <summary>Gets macro value by ModSource enum.</summary>
    public double GetValue(ModSource source) => source switch
    {
        ModSource.Macro1 => _values[0],
        ModSource.Macro2 => _values[1],
        ModSource.Macro3 => _values[2],
        ModSource.Macro4 => _values[3],
        _ => 0.0
    };

    /// <summary>Resets all macros to 0.</summary>
    public void Reset()
    {
        Array.Clear(_values, 0, Count);
    }
}

using System;
using System.Threading;

namespace OttoSynth.Core.DSP.Modulation;

/// <summary>
/// 4 global macro controls that can be used as modulation sources.
/// Each macro is a simple 0..1 value controllable by the UI or MIDI CC.
///
/// Threading: macros are written by the UI/MIDI thread and read by the audio thread.
/// We use Volatile reads/writes (single-writer / multi-reader pattern) to guarantee
/// the audio thread always sees a consistent value without locking.
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
        get => (index >= 0 && index < Count) ? Volatile.Read(ref _values[index]) : 0.0;
        set
        {
            if (index >= 0 && index < Count)
                Volatile.Write(ref _values[index], Math.Clamp(value, 0.0, 1.0));
        }
    }

    /// <summary>Gets macro value by ModSource enum.</summary>
    public double GetValue(ModSource source) => source switch
    {
        ModSource.Macro1 => Volatile.Read(ref _values[0]),
        ModSource.Macro2 => Volatile.Read(ref _values[1]),
        ModSource.Macro3 => Volatile.Read(ref _values[2]),
        ModSource.Macro4 => Volatile.Read(ref _values[3]),
        _ => 0.0
    };

    /// <summary>Resets all macros to 0.</summary>
    public void Reset()
    {
        for (int i = 0; i < Count; i++)
            Volatile.Write(ref _values[i], 0.0);
    }
}

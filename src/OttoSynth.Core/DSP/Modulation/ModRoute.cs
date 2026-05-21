using System;

namespace OttoSynth.Core.DSP.Modulation;

/// <summary>
/// A single modulation routing: connects a source to a destination with an amount.
/// Immutable struct for zero-allocation usage in the mod matrix processing loop.
/// </summary>
public struct ModRoute
{
    /// <summary>Modulation source.</summary>
    public ModSource Source;

    /// <summary>Modulation destination.</summary>
    public ModDestination Destination;

    /// <summary>
    /// Modulation amount (-1 to +1).
    /// Positive: source increases destination. Negative: source decreases destination.
    /// The actual range depends on the destination's ModDestinationInfo.
    /// </summary>
    public double Amount;

    /// <summary>
    /// Whether this route is active (enabled).
    /// Disabled routes are skipped during processing.
    /// </summary>
    public bool Active;

    public ModRoute(ModSource source, ModDestination destination, double amount)
    {
        Source = source;
        Destination = destination;
        Amount = Math.Clamp(amount, -1.0, 1.0);
        Active = true;
    }

    /// <summary>Creates an empty (inactive) route.</summary>
    public static ModRoute Empty => new()
    {
        Source = ModSource.None,
        Destination = ModDestination.None,
        Amount = 0.0,
        Active = false
    };

    /// <summary>Whether this route has valid source and destination.</summary>
    public readonly bool IsValid =>
        Source != ModSource.None &&
        Destination != ModDestination.None &&
        Active &&
        Math.Abs(Amount) > 0.0001;
}

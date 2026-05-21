using System;
using System.Runtime.CompilerServices;

namespace OttoSynth.Core.DSP.Modulation;

/// <summary>
/// Modulation Matrix: connects modulation sources to destinations via routes.
/// Supports up to 32 simultaneous routes. Processes per-sample modulation values
/// into an output array indexed by ModDestination.
/// Zero-allocation in the Process loop — all arrays are pre-allocated.
/// </summary>
public sealed class ModMatrix
{
    /// <summary>Maximum number of simultaneous modulation routes.</summary>
    public const int MaxRoutes = 32;

    /// <summary>Maximum destination enum value (for output array sizing).</summary>
    private const int MaxDestinations = 128;

    // ─── Route storage ──────────────────────────────────────────
    private readonly ModRoute[] _routes;
    private int _routeCount;

    // ─── Source value cache (populated before processing) ────────
    // Per-voice sources (envelopes, LFOs, velocity, etc.)
    private readonly double[] _sourceValues;
    private const int MaxSources = 64;

    // ─── Output: accumulated modulation per destination ─────────
    // Indexed by (byte)ModDestination. Contains the SUM of all
    // modulations targeting that destination for the current sample.
    private readonly double[] _modOutput;

    // ─── Global source references ───────────────────────────────
    private double _modWheelValue;
    private double _pitchBendValue;
    private double _aftertouchValue;

    // ─── Macro controls (global, shared across voices) ──────────
    private MacroControls? _macros;

    /// <summary>Number of active routes.</summary>
    public int RouteCount => _routeCount;

    /// <summary>Read-only access to routes (for UI display).</summary>
    public ReadOnlySpan<ModRoute> Routes => _routes.AsSpan(0, _routeCount);

    /// <summary>Read-only access to the modulation output array.</summary>
    public ReadOnlySpan<double> Output => _modOutput.AsSpan();

    public ModMatrix()
    {
        _routes = new ModRoute[MaxRoutes];
        _sourceValues = new double[MaxSources];
        _modOutput = new double[MaxDestinations];
        _routeCount = 0;
    }

    /// <summary>Sets the reference to the macro controls (shared with SynthEngine).</summary>
    public void SetMacros(MacroControls macros)
    {
        _macros = macros;
    }

    // ─── Route Management ───────────────────────────────────────

    /// <summary>
    /// Adds a modulation route. Returns the route index, or -1 if full.
    /// </summary>
    public int AddRoute(ModSource source, ModDestination destination, double amount)
    {
        if (_routeCount >= MaxRoutes) return -1;

        _routes[_routeCount] = new ModRoute(source, destination, amount);
        return _routeCount++;
    }

    /// <summary>
    /// Removes a modulation route by index.
    /// </summary>
    public void RemoveRoute(int index)
    {
        if (index < 0 || index >= _routeCount) return;

        // Shift remaining routes down
        for (int i = index; i < _routeCount - 1; i++)
        {
            _routes[i] = _routes[i + 1];
        }
        _routes[_routeCount - 1] = ModRoute.Empty;
        _routeCount--;
    }

    /// <summary>
    /// Updates the amount of an existing route.
    /// </summary>
    public void SetRouteAmount(int index, double amount)
    {
        if (index < 0 || index >= _routeCount) return;
        _routes[index].Amount = Math.Clamp(amount, -1.0, 1.0);
    }

    /// <summary>
    /// Enables or disables a route.
    /// </summary>
    public void SetRouteActive(int index, bool active)
    {
        if (index < 0 || index >= _routeCount) return;
        _routes[index].Active = active;
    }

    /// <summary>
    /// Clears all routes.
    /// </summary>
    public void ClearRoutes()
    {
        for (int i = 0; i < _routeCount; i++)
            _routes[i] = ModRoute.Empty;
        _routeCount = 0;
    }

    /// <summary>
    /// Gets a route by index (for UI editing).
    /// </summary>
    public ModRoute GetRoute(int index)
    {
        if (index < 0 || index >= _routeCount) return ModRoute.Empty;
        return _routes[index];
    }

    // ─── Global Source Updates (called from SynthEngine) ─────────

    /// <summary>Updates the mod wheel value (0..1).</summary>
    public void SetModWheel(double value) => _modWheelValue = value;

    /// <summary>Updates the pitch bend value (-1..+1).</summary>
    public void SetPitchBend(double value) => _pitchBendValue = value;

    /// <summary>Updates the aftertouch value (0..1).</summary>
    public void SetAftertouch(double value) => _aftertouchValue = value;

    // ─── Per-Voice Processing ───────────────────────────────────

    /// <summary>
    /// Sets the per-voice source values for the current processing block.
    /// Called once per voice per block, before Process().
    /// </summary>
    /// <param name="env1">Average ENV1 value for this block.</param>
    /// <param name="env2">Average ENV2 value for this block.</param>
    /// <param name="env3">Average ENV3 value for this block.</param>
    /// <param name="lfo1">Average LFO1 value for this block.</param>
    /// <param name="lfo2">Average LFO2 value for this block.</param>
    /// <param name="lfo3">Average LFO3 value for this block.</param>
    /// <param name="velocity">Note velocity (0..1).</param>
    /// <param name="noteNumber">MIDI note number (0-127).</param>
    /// <param name="noteRandom">Random value assigned at NoteOn (0..1).</param>
    public void SetVoiceSources(
        double env1, double env2, double env3,
        double lfo1, double lfo2, double lfo3,
        double velocity, int noteNumber, double noteRandom)
    {
        _sourceValues[(int)ModSource.Envelope1] = env1;
        _sourceValues[(int)ModSource.Envelope2] = env2;
        _sourceValues[(int)ModSource.Envelope3] = env3;
        _sourceValues[(int)ModSource.Lfo1] = lfo1;
        _sourceValues[(int)ModSource.Lfo2] = lfo2;
        _sourceValues[(int)ModSource.Lfo3] = lfo3;
        _sourceValues[(int)ModSource.Velocity] = velocity;

        // Key tracking: centered on C4 (note 60), range ~-1 to +1 over 10 octaves
        _sourceValues[(int)ModSource.KeyTracking] = (noteNumber - 60) / 60.0;

        _sourceValues[(int)ModSource.NoteRandom] = noteRandom;

        // Global sources
        _sourceValues[(int)ModSource.ModWheel] = _modWheelValue;
        _sourceValues[(int)ModSource.PitchBend] = _pitchBendValue;
        _sourceValues[(int)ModSource.Aftertouch] = _aftertouchValue;

        // Macros
        if (_macros != null)
        {
            _sourceValues[(int)ModSource.Macro1] = _macros[0];
            _sourceValues[(int)ModSource.Macro2] = _macros[1];
            _sourceValues[(int)ModSource.Macro3] = _macros[2];
            _sourceValues[(int)ModSource.Macro4] = _macros[3];
        }
    }

    /// <summary>
    /// Processes all active routes and calculates the modulation output per destination.
    /// Must be called after SetVoiceSources(). The results are available via GetModValue().
    /// Zero-allocation.
    /// </summary>
    public void Process()
    {
        // Clear outputs
        Array.Clear(_modOutput, 0, MaxDestinations);

        // Accumulate modulation from all active routes
        for (int i = 0; i < _routeCount; i++)
        {
            ref ModRoute route = ref _routes[i];
            if (!route.IsValid) continue;

            double sourceValue = GetSourceValue(route.Source);
            double modulation = sourceValue * route.Amount;

            // Scale modulation to destination range
            var info = ModDestinationInfo.GetInfo(route.Destination);
            double range = info.MaxValue - info.MinValue;

            if (info.IsLogarithmic)
            {
                // For log-scale params (filter cutoff), modulation is in octaves
                // Amount=1.0 → ±7 octaves of range
                _modOutput[(byte)route.Destination] += modulation * 7.0;
            }
            else
            {
                // Linear: scale modulation to full parameter range
                _modOutput[(byte)route.Destination] += modulation * range;
            }
        }
    }

    /// <summary>
    /// Gets the total modulation offset for a destination.
    /// This value should be ADDED to the base parameter value.
    /// For logarithmic destinations (filter cutoff), the value is in octaves.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetModValue(ModDestination destination)
    {
        return _modOutput[(byte)destination];
    }

    /// <summary>
    /// Applies modulation to a base value, respecting the destination's range and scale.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ApplyMod(ModDestination destination, double baseValue)
    {
        double mod = _modOutput[(byte)destination];
        if (Math.Abs(mod) < 0.00001) return baseValue;

        var info = ModDestinationInfo.GetInfo(destination);

        if (info.IsLogarithmic)
        {
            // Logarithmic: mod is in octaves, apply as frequency multiplier
            double result = baseValue * Math.Pow(2.0, mod);
            return Math.Clamp(result, info.MinValue, info.MaxValue);
        }
        else
        {
            // Linear: add modulation offset
            return Math.Clamp(baseValue + mod, info.MinValue, info.MaxValue);
        }
    }

    /// <summary>
    /// Gets the raw value of a modulation source.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetSourceValue(ModSource source)
    {
        byte idx = (byte)source;
        if (idx < MaxSources)
            return _sourceValues[idx];
        return 0.0;
    }
}

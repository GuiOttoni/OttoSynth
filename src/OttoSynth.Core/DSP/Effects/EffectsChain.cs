using System;
using System.Collections.Generic;

namespace OttoSynth.Core.DSP.Effects;

/// <summary>
/// Ordered chain of effects applied to the synth output.
/// Effects are processed in order; each can be bypassed individually.
/// The chain is thread-safe for parameter updates but Process() must run
/// only from the audio thread.
/// </summary>
public sealed class EffectsChain
{
    private readonly List<IEffect> _effects = new();
    private double _sampleRate = 44100.0;

    /// <summary>Number of effects currently in the chain.</summary>
    public int Count => _effects.Count;

    /// <summary>Read-only access to effects.</summary>
    public IReadOnlyList<IEffect> Effects => _effects;

    /// <summary>Indexer for getting an effect by position.</summary>
    public IEffect this[int index] => _effects[index];

    /// <summary>Sets the sample rate for all effects in the chain.</summary>
    public void SetSampleRate(double sampleRate)
    {
        _sampleRate = sampleRate;
        foreach (var fx in _effects)
            fx.SetSampleRate(sampleRate);
    }

    /// <summary>Adds an effect to the end of the chain.</summary>
    public void Add(IEffect effect)
    {
        effect.SetSampleRate(_sampleRate);
        _effects.Add(effect);
    }

    /// <summary>Inserts an effect at the specified position.</summary>
    public void Insert(int index, IEffect effect)
    {
        effect.SetSampleRate(_sampleRate);
        _effects.Insert(index, effect);
    }

    /// <summary>Removes an effect by index.</summary>
    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _effects.Count) return;
        _effects.RemoveAt(index);
    }

    /// <summary>Clears all effects from the chain.</summary>
    public void Clear() => _effects.Clear();

    /// <summary>Reorders effects: moves the effect at fromIndex to toIndex.</summary>
    public void Move(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _effects.Count) return;
        if (toIndex < 0 || toIndex >= _effects.Count) return;
        var fx = _effects[fromIndex];
        _effects.RemoveAt(fromIndex);
        _effects.Insert(toIndex, fx);
    }

    /// <summary>Resets internal state for all effects.</summary>
    public void Reset()
    {
        foreach (var fx in _effects)
            fx.Reset();
    }

    /// <summary>Processes audio through all effects in order.</summary>
    public void Process(double[] left, double[] right, int sampleCount)
    {
        for (int i = 0; i < _effects.Count; i++)
            _effects[i].Process(left, right, sampleCount);
    }
}

namespace OttoSynth.Core.DSP.Effects;

/// <summary>Which channels the effect processes and affects.</summary>
public enum ChannelRoute { Both, Left, Right }

/// <summary>
/// Base interface for all audio effects.
/// Effects process stereo audio buffers in place.
/// All implementations must be zero-allocation in the Process loop.
/// </summary>
public interface IEffect
{
    /// <summary>Effect display name (e.g. "Reverb", "Delay").</summary>
    string Name { get; }

    /// <summary>Whether this effect is bypassed. If true, audio passes through unchanged.</summary>
    bool Bypass { get; set; }

    /// <summary>
    /// Dry/Wet mix (0..1). 0 = fully dry (no effect), 1 = fully wet (effect only).
    /// Each effect blends its internal output with the input using this value.
    /// </summary>
    double Mix { get; set; }

    /// <summary>Initializes the effect with the current sample rate.</summary>
    void SetSampleRate(double sampleRate);

    /// <summary>
    /// Processes a stereo block of audio in place.
    /// The effect reads from and writes to the same buffers.
    /// </summary>
    void Process(double[] left, double[] right, int sampleCount);

    /// <summary>Resets all internal state (filters, delay lines, etc.).</summary>
    void Reset();
}

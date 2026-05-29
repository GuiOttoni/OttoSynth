namespace OttoSynth.Core.DSP.Oscillators;

/// <summary>
/// Common contract for any audio source that can occupy an oscillator slot in a voice.
/// Implemented by <see cref="WavetableOscillator"/> (single) and <see cref="UnisonEngine"/> (stacked).
/// Lets <c>SynthVoice</c> treat both uniformly — no branching on voice count in the process loop.
/// </summary>
public interface IOscillatorSource
{
    // ─── Parameters ─────────────────────────────────────────────
    double Level { get; set; }
    double Pan { get; set; }
    int CoarseTune { get; set; }
    double FineTune { get; set; }
    double WavetablePosition { get; set; }
    WavetableOscillator.WaveWarp Warp { get; set; }
    double WarpAmount { get; set; }

    // ─── Lifecycle ───────────────────────────────────────────────
    void SetFrequency(double frequency);
    void SetSampleRate(double sampleRate);
    void SetWavetable(double[][] wavetable, bool hasMipmap);

    /// <summary>Called on NoteOn. Resets or randomizes phase depending on implementation.</summary>
    void NoteOn();

    /// <summary>Hard reset of phase accumulator(s) to zero.</summary>
    void ResetPhase();

    // ─── Audio generation ────────────────────────────────────────

    /// <summary>
    /// Generates one block of audio.
    /// If <paramref name="rawMono"/> is non-null, each sample (post-warp, pre-level/pan) is
    /// accumulated into it — giving a pan-independent signal suitable for FM extraction.
    /// </summary>
    void Process(double[] outL, double[] outR, double[]? rawMono, int sampleCount);

    /// <summary>
    /// Generates one block of audio with exponential FM applied.
    /// <paramref name="rawMono"/> behaves the same as in <see cref="Process"/>.
    /// </summary>
    void ProcessWithFM(double[] outL, double[] outR, double[]? rawMono,
                       double[] fmBuf, double fmDepth, int sampleCount);

    // Convenience overloads — no allocation, just forward with rawMono=null.
    void Process(double[] outL, double[] outR, int sampleCount)
        => Process(outL, outR, null, sampleCount);
    void ProcessWithFM(double[] outL, double[] outR, double[] fmBuf, double fmDepth, int sampleCount)
        => ProcessWithFM(outL, outR, null, fmBuf, fmDepth, sampleCount);
}

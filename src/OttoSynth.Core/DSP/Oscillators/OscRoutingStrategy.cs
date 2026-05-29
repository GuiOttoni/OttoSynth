using System.Runtime.CompilerServices;

namespace OttoSynth.Core.DSP.Oscillators;

/// <summary>
/// Strategy for inter-oscillator routing.  Each implementation encodes one routing mode:
/// how the preceding oscillator's output affects the next oscillator.
///
/// Implementations are zero-allocation: stateless strategies are singletons,
/// stateful ones (FM depth) are pre-allocated once per voice and reused.
///
/// The two-phase split matches the audio topology:
///   GenerateCarrier — carrier generation (FM must happen here, per-sample)
///   PostProcess     — post-generation transform (ring mod multiplies after)
/// </summary>
public interface IOscRoutingStrategy
{
    /// <summary>
    /// Generate audio for the carrier oscillator.
    /// For FM routing this modulates the carrier's frequency during generation.
    /// For other modes it simply delegates to <see cref="IOscillatorSource.Process"/>.
    /// </summary>
    /// <param name="source">Carrier oscillator source.</param>
    /// <param name="outL">Pre-cleared output — left channel.</param>
    /// <param name="outR">Pre-cleared output — right channel.</param>
    /// <param name="modL">Modulator output — left channel (from previous OSC).</param>
    /// <param name="modR">Modulator output — right channel.</param>
    /// <param name="monoScratch">Pre-allocated scratch buffer for mono FM conversion.</param>
    /// <param name="sampleCount">Samples to process.</param>
    void GenerateCarrier(IOscillatorSource source,
                         double[] outL, double[] outR,
                         double[] modL, double[] modR,
                         double[] monoScratch,
                         int sampleCount);

    /// <summary>Post-process the carrier buffer (e.g., multiply for ring mod).</summary>
    void PostProcess(double[] carrierL, double[] carrierR,
                     double[] modL, double[] modR, int sampleCount);
}

// ─── Concrete strategies ────────────────────────────────────────────────────

/// <summary>Default: carrier runs independently; no inter-oscillator interaction.</summary>
public sealed class MixRouting : IOscRoutingStrategy
{
    public static readonly MixRouting Instance = new();
    private MixRouting() { }

    public void GenerateCarrier(IOscillatorSource source,
                                double[] outL, double[] outR,
                                double[] modL, double[] modR,
                                double[] monoScratch, int n)
        => source.Process(outL, outR, n);

    public void PostProcess(double[] cL, double[] cR, double[] mL, double[] mR, int n) { }
}

/// <summary>
/// Exponential FM: carrier frequency is modulated per-sample by the preceding OSC's output.
/// Maximum deviation is ±4 octaves at <see cref="Depth"/> = 1.
/// </summary>
public sealed class FmRouting : IOscRoutingStrategy
{
    private double _depth = 0.5;

    /// <summary>FM depth (0..1): scales the ±4-octave maximum frequency deviation.</summary>
    public double Depth
    {
        get => _depth;
        set => _depth = Math.Clamp(value, 0.0, 1.0);
    }

    public void GenerateCarrier(IOscillatorSource source,
                                double[] outL, double[] outR,
                                double[] modL, double[] modR,
                                double[] monoScratch, int n)
    {
        // Convert stereo modulator to mono for the FM signal
        for (int i = 0; i < n; i++)
            monoScratch[i] = (modL[i] + modR[i]) * 0.5;

        source.ProcessWithFM(outL, outR, monoScratch, _depth, n);
    }

    public void PostProcess(double[] cL, double[] cR, double[] mL, double[] mR, int n) { }
}

/// <summary>
/// Ring modulation: carrier output is multiplied sample-by-sample by the modulator output.
/// Produces sum-and-difference sidebands; OSC1 level controls the modulation depth.
/// </summary>
public sealed class RingModRouting : IOscRoutingStrategy
{
    public static readonly RingModRouting Instance = new();
    private RingModRouting() { }

    public void GenerateCarrier(IOscillatorSource source,
                                double[] outL, double[] outR,
                                double[] modL, double[] modR,
                                double[] monoScratch, int n)
        => source.Process(outL, outR, n);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PostProcess(double[] cL, double[] cR, double[] mL, double[] mR, int n)
    {
        for (int i = 0; i < n; i++)
        {
            cL[i] *= mL[i];
            cR[i] *= mR[i];
        }
    }
}

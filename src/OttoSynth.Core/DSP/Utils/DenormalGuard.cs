using System;

namespace OttoSynth.Core.DSP.Utils;

/// <summary>
/// RAII-style scope intended to enable FTZ (Flush-To-Zero) and DAZ (Denormals-Are-Zero)
/// on the SSE control register (MXCSR) for the audio thread.
///
/// Denormals (subnormal floats near 1e-38) cause severe CPU spikes on x86 because
/// they fall through to microcoded paths. Audio DSP loops are particularly vulnerable
/// because IIR filters, smoothers, and feedback paths all converge toward zero.
///
/// <para>
/// <b>Status (1.1.0):</b> .NET does not expose <c>_mm_getcsr</c> / <c>_mm_setcsr</c>
/// in <see cref="System.Runtime.Intrinsics.X86"/>. A proper implementation would require
/// either P/Invoke to a small native helper or compiling with NativeAOT and an exported
/// asm shim. For now this scope is a no-op; the most critical denormal hot-spots
/// (Compressor gain reduction, etc.) carry explicit flush-to-zero guards inline.
/// </para>
///
/// <para>
/// <b>Future:</b> When the CLAP plugin ships with NativeAOT we can include a small
/// native helper that calls the SSE intrinsic; the managed shim here then becomes
/// a P/Invoke into that helper. Until then, this struct preserves the call sites so
/// the migration is a single-file change.
/// </para>
/// </summary>
public readonly struct DenormalGuard : IDisposable
{
    /// <summary>
    /// Enters a denormal-flushing scope. Call from the audio thread once per buffer.
    /// </summary>
    public static DenormalGuard Enter() => default;

    public void Dispose() { }
}

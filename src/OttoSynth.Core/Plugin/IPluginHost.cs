namespace OttoSynth.Core.Plugin;

/// <summary>
/// Minimal abstraction over the plugin host's parameter store.
/// Allows the same <see cref="ParameterDispatcher"/> to drive both the VST3 plugin
/// (parameters stored in AudioPlugSharp's <c>Parameters</c> collection) and the CLAP
/// plugin (parameters stored in a CLAP-specific value array) without OttoSynth.Core
/// depending on either host SDK.
/// </summary>
public interface IPluginHost
{
    /// <summary>
    /// Returns the current value of a parameter by its stable integer ID.
    /// Used during dispatch when a setter needs sibling values (e.g. Filter1Cutoff
    /// also needs Filter1Resonance to call <c>SynthEngine.SetFilter</c>).
    /// </summary>
    double GetParameterValue(int id);

    /// <summary>
    /// Writes a parameter value into the host's store. Used by
    /// <see cref="ParameterDispatcher.CaptureAll"/> after preset load to keep the
    /// host display in sync with the engine state.
    /// </summary>
    void SetParameterValue(int id, double value);
}

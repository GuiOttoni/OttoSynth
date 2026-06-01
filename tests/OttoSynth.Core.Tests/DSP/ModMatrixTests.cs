using OttoSynth.Core.DSP.Modulation;

namespace OttoSynth.Core.Tests.DSP;

public class ModMatrixTests
{
    [Fact]
    public void AddRoute_SingleRoute_StoresAtIndexZero()
    {
        var matrix = new ModMatrix();

        int idx = matrix.AddRoute(ModSource.Lfo1, ModDestination.Filter1Cutoff, 0.5);

        Assert.Equal(0, idx);
        Assert.Equal(1, matrix.RouteCount);
    }

    [Fact]
    public void AddRoute_BeyondMax_ReturnsMinusOne()
    {
        var matrix = new ModMatrix();
        for (int i = 0; i < ModMatrix.MaxRoutes; i++)
            matrix.AddRoute(ModSource.Lfo1, ModDestination.Filter1Cutoff, 0.5);

        int overflowIdx = matrix.AddRoute(ModSource.Envelope1, ModDestination.MasterVolume, 0.5);

        Assert.Equal(-1, overflowIdx);
        Assert.Equal(ModMatrix.MaxRoutes, matrix.RouteCount);
    }

    [Fact]
    public void RemoveRoute_DecreasesCount()
    {
        var matrix = new ModMatrix();
        matrix.AddRoute(ModSource.Envelope1, ModDestination.Filter1Cutoff, 0.5);
        matrix.AddRoute(ModSource.Lfo1, ModDestination.Filter2Cutoff, 0.3);

        matrix.RemoveRoute(0);

        Assert.Equal(1, matrix.RouteCount);
    }

    [Fact]
    public void Process_LinearDestination_AppliesAmountTimesRange()
    {
        var matrix = new ModMatrix();
        // OSC1 level range is 0..1 (range = 1).
        // Amount 0.5, source velocity 1.0 → mod = 0.5
        matrix.AddRoute(ModSource.Velocity, ModDestination.Osc1Level, 0.5);
        matrix.SetVoiceSources(0, 0, 0, 0, 0, 0, velocity: 1.0, noteNumber: 60, noteRandom: 0);

        matrix.Process();

        double mod = matrix.GetModValue(ModDestination.Osc1Level);
        Assert.Equal(0.5, mod, precision: 6);
    }

    [Fact]
    public void Process_LogarithmicDestination_ReturnsOctaves()
    {
        var matrix = new ModMatrix();
        // Filter cutoff is logarithmic, mod should be in octaves
        // Amount 1.0 × source 1.0 → 7 octaves
        matrix.AddRoute(ModSource.Velocity, ModDestination.Filter1Cutoff, 1.0);
        matrix.SetVoiceSources(0, 0, 0, 0, 0, 0, velocity: 1.0, noteNumber: 60, noteRandom: 0);

        matrix.Process();

        double octaves = matrix.GetModValue(ModDestination.Filter1Cutoff);
        Assert.Equal(7.0, octaves, precision: 6);
    }

    [Fact]
    public void ApplyMod_LogScale_MultipliesByPowerOfTwo()
    {
        var matrix = new ModMatrix();
        matrix.AddRoute(ModSource.Velocity, ModDestination.Filter1Cutoff, 1.0 / 7.0); // 1 octave
        matrix.SetVoiceSources(0, 0, 0, 0, 0, 0, velocity: 1.0, noteNumber: 60, noteRandom: 0);
        matrix.Process();

        double modulated = matrix.ApplyMod(ModDestination.Filter1Cutoff, 1000.0);

        Assert.Equal(2000.0, modulated, precision: 1);
    }

    [Fact]
    public void Process_TwoRoutesSameDestination_Accumulates()
    {
        var matrix = new ModMatrix();
        matrix.AddRoute(ModSource.Velocity, ModDestination.Osc1Level, 0.3);
        matrix.AddRoute(ModSource.ModWheel, ModDestination.Osc1Level, 0.2);
        matrix.SetModWheel(1.0);
        matrix.SetVoiceSources(0, 0, 0, 0, 0, 0, velocity: 1.0, noteNumber: 60, noteRandom: 0);

        matrix.Process();

        double mod = matrix.GetModValue(ModDestination.Osc1Level);
        Assert.Equal(0.5, mod, precision: 6);
    }

    [Fact]
    public void SetRouteActive_False_SkipsRoute()
    {
        var matrix = new ModMatrix();
        int idx = matrix.AddRoute(ModSource.Velocity, ModDestination.Osc1Level, 1.0);
        matrix.SetRouteActive(idx, false);
        matrix.SetVoiceSources(0, 0, 0, 0, 0, 0, velocity: 1.0, noteNumber: 60, noteRandom: 0);

        matrix.Process();

        Assert.Equal(0.0, matrix.GetModValue(ModDestination.Osc1Level));
    }

    [Fact]
    public void ClearRoutes_ResetsCount()
    {
        var matrix = new ModMatrix();
        matrix.AddRoute(ModSource.Envelope1, ModDestination.Filter1Cutoff, 0.5);
        matrix.AddRoute(ModSource.Lfo1, ModDestination.Osc1Pitch, 0.3);

        matrix.ClearRoutes();

        Assert.Equal(0, matrix.RouteCount);
    }

    [Fact]
    public void MacroControls_ContributeAsSource()
    {
        var matrix = new ModMatrix();
        var macros = new MacroControls();
        macros[0] = 0.5;
        matrix.SetMacros(macros);
        matrix.AddRoute(ModSource.Macro1, ModDestination.Osc1Level, 1.0);

        matrix.SetVoiceSources(0, 0, 0, 0, 0, 0, velocity: 0, noteNumber: 60, noteRandom: 0);
        matrix.Process();

        Assert.Equal(0.5, matrix.GetModValue(ModDestination.Osc1Level), precision: 6);
    }

    [Fact]
    public void KeyTracking_ZeroAtMidiNote60()
    {
        var matrix = new ModMatrix();
        matrix.AddRoute(ModSource.KeyTracking, ModDestination.Osc1Level, 1.0);

        matrix.SetVoiceSources(0, 0, 0, 0, 0, 0, velocity: 0, noteNumber: 60, noteRandom: 0);
        matrix.Process();

        Assert.Equal(0.0, matrix.GetModValue(ModDestination.Osc1Level), precision: 6);
    }

    [Fact]
    public void Process_NegativeAmount_NegatesContribution()
    {
        var matrix = new ModMatrix();
        matrix.AddRoute(ModSource.Velocity, ModDestination.Osc1Level, -0.4);
        matrix.SetVoiceSources(0, 0, 0, 0, 0, 0, velocity: 1.0, noteNumber: 60, noteRandom: 0);

        matrix.Process();

        Assert.Equal(-0.4, matrix.GetModValue(ModDestination.Osc1Level), precision: 6);
    }

    [Fact]
    public void Macros_Concurrent_ReadWrite_ProducesValidValues()
    {
        // Bug 1.2: macros are written by UI/MIDI thread and read by audio thread
        // without synchronization. Without volatile reads/writes, the audio thread
        // could observe a torn double on 32-bit platforms or stale values on x64.
        // We exercise the contract by having one writer + one reader hammer
        // [0,1] sweeps and checking every observed value is finite and in range.
        var macros = new OttoSynth.Core.DSP.Modulation.MacroControls();
        var matrix = new ModMatrix();
        matrix.SetMacros(macros);

        const int Iterations = 50_000;
        var cts = new System.Threading.CancellationTokenSource();
        bool observedBadValue = false;

        var reader = System.Threading.Tasks.Task.Run(() =>
        {
            for (int i = 0; i < Iterations && !cts.IsCancellationRequested; i++)
            {
                matrix.SetVoiceSources(0, 0, 0, 0, 0, 0, velocity: 0, noteNumber: 60, noteRandom: 0);
                for (int m = 0; m < 4; m++)
                {
                    double v = macros[m];
                    if (double.IsNaN(v) || double.IsInfinity(v) || v < 0.0 || v > 1.0)
                    {
                        observedBadValue = true;
                        return;
                    }
                }
            }
        });

        var writer = System.Threading.Tasks.Task.Run(() =>
        {
            for (int i = 0; i < Iterations && !cts.IsCancellationRequested; i++)
            {
                for (int m = 0; m < 4; m++)
                    macros[m] = (i * 0.0001 + m * 0.25) % 1.0;
            }
        });

        bool finished = System.Threading.Tasks.Task.WaitAll([reader, writer], TimeSpan.FromSeconds(10));
        cts.Cancel();

        Assert.True(finished, "Test did not finish — possible deadlock");
        Assert.False(observedBadValue, "Reader observed torn or out-of-range macro value");
    }
}

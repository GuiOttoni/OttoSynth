using System;
using Xunit;
using OttoSynth.Core.DSP.Envelopes;

namespace OttoSynth.Core.Tests.DSP;

public class AdsrEnvelopeTests
{
    private const double SampleRate = 44100.0;

    [Fact]
    public void Initial_State_IsIdle()
    {
        var env = new AdsrEnvelope();
        Assert.Equal(AdsrEnvelope.State.Idle, env.CurrentState);
        Assert.Equal(0.0, env.CurrentValue);
        Assert.False(env.IsActive);
    }

    [Fact]
    public void NoteOn_TransitionsToAttack()
    {
        var env = new AdsrEnvelope();
        env.SetSampleRate(SampleRate);
        env.NoteOn();

        Assert.Equal(AdsrEnvelope.State.Attack, env.CurrentState);
        Assert.True(env.IsActive);
    }

    [Fact]
    public void Attack_RampsUpToOne()
    {
        // Arrange
        var env = new AdsrEnvelope();
        env.SetSampleRate(SampleRate);
        env.AttackTime = 0.01; // 10ms attack
        env.SustainLevel = 1.0;
        env.NoteOn();

        // Process enough samples for attack + some decay
        int attackSamples = (int)(SampleRate * 0.01) + 100;
        var buffer = new double[attackSamples];
        env.Process(buffer, attackSamples);

        // Assert — value should reach 1.0 during attack
        bool reachedPeak = false;
        for (int i = 0; i < attackSamples; i++)
        {
            if (buffer[i] >= 0.99) reachedPeak = true;
        }
        Assert.True(reachedPeak, "Envelope should reach peak (1.0) during attack");
    }

    [Fact]
    public void Decay_DropsToSustainLevel()
    {
        // Arrange
        var env = new AdsrEnvelope();
        env.SetSampleRate(SampleRate);
        env.AttackTime = 0.001; // Very short attack (1ms)
        env.DecayTime = 0.01;  // 10ms decay
        env.SustainLevel = 0.5;
        env.NoteOn();

        // Process enough samples for attack + full decay
        int totalSamples = (int)(SampleRate * 0.05); // 50ms total
        var buffer = new double[totalSamples];
        env.Process(buffer, totalSamples);

        // Assert — end of buffer should be near sustain level
        double endValue = buffer[totalSamples - 1];
        Assert.InRange(endValue, 0.45, 0.55); // Should be close to 0.5
    }

    [Fact]
    public void Sustain_MaintainsLevel()
    {
        // Arrange
        var env = new AdsrEnvelope();
        env.SetSampleRate(SampleRate);
        env.AttackTime = 0.001;
        env.DecayTime = 0.001;
        env.SustainLevel = 0.7;
        env.NoteOn();

        // Process through attack and decay
        int setupSamples = (int)(SampleRate * 0.01);
        var setupBuffer = new double[setupSamples];
        env.Process(setupBuffer, setupSamples);

        // Now should be in sustain — process more
        int sustainSamples = 1000;
        var buffer = new double[sustainSamples];
        env.Process(buffer, sustainSamples);

        // Assert — all values should be at sustain level
        for (int i = 0; i < sustainSamples; i++)
        {
            Assert.InRange(buffer[i], 0.65, 0.75);
        }
    }

    [Fact]
    public void NoteOff_TransitionsToRelease()
    {
        // Arrange
        var env = new AdsrEnvelope();
        env.SetSampleRate(SampleRate);
        env.AttackTime = 0.001;
        env.DecayTime = 0.001;
        env.SustainLevel = 0.7;
        env.ReleaseTime = 0.1;
        env.NoteOn();

        // Process to sustain
        var setup = new double[500];
        env.Process(setup, 500);

        // Act
        env.NoteOff();

        // Assert
        Assert.Equal(AdsrEnvelope.State.Release, env.CurrentState);
    }

    [Fact]
    public void Release_DropsToZero()
    {
        // Arrange
        var env = new AdsrEnvelope();
        env.SetSampleRate(SampleRate);
        env.AttackTime = 0.001;
        env.DecayTime = 0.001;
        env.SustainLevel = 0.7;
        env.ReleaseTime = 0.01; // 10ms release
        env.NoteOn();

        // Process to sustain
        var setup = new double[500];
        env.Process(setup, 500);

        // Note off
        env.NoteOff();

        // Process through release
        int releaseSamples = (int)(SampleRate * 0.05);
        var buffer = new double[releaseSamples];
        bool stillActive = env.Process(buffer, releaseSamples);

        // Assert — should reach zero and go idle
        Assert.False(stillActive, "Envelope should be idle after full release");
        Assert.Equal(AdsrEnvelope.State.Idle, env.CurrentState);
    }

    [Fact]
    public void ForceRelease_RapidFadeout()
    {
        // Arrange
        var env = new AdsrEnvelope();
        env.SetSampleRate(SampleRate);
        env.AttackTime = 0.001;
        env.DecayTime = 0.001;
        env.SustainLevel = 1.0;
        env.NoteOn();

        // Process to sustain
        var setup = new double[500];
        env.Process(setup, 500);

        // Force release with 3ms fade
        env.ForceRelease(3.0);

        // Process through the fast release
        int fadeSamples = (int)(SampleRate * 0.01); // 10ms (more than enough for 3ms fade)
        var buffer = new double[fadeSamples];
        env.Process(buffer, fadeSamples);

        // Assert — should be idle
        Assert.Equal(AdsrEnvelope.State.Idle, env.CurrentState);
    }

    [Fact]
    public void Retrigger_DuringRelease_StartsNewAttack()
    {
        // Arrange
        var env = new AdsrEnvelope();
        env.SetSampleRate(SampleRate);
        env.AttackTime = 0.01;
        env.SustainLevel = 0.7;
        env.ReleaseTime = 0.5; // Long release
        env.NoteOn();

        // Process to sustain
        var setup = new double[1000];
        env.Process(setup, 1000);

        // Note off → release
        env.NoteOff();
        var releaseBuffer = new double[500];
        env.Process(releaseBuffer, 500);
        Assert.Equal(AdsrEnvelope.State.Release, env.CurrentState);

        // Retrigger
        env.NoteOn();
        Assert.Equal(AdsrEnvelope.State.Attack, env.CurrentState);
        Assert.True(env.IsActive);
    }

    [Fact]
    public void AllValues_AlwaysWithinZeroToOne()
    {
        var env = new AdsrEnvelope();
        env.SetSampleRate(SampleRate);
        env.AttackTime = 0.01;
        env.DecayTime = 0.1;
        env.SustainLevel = 0.6;
        env.ReleaseTime = 0.1;
        env.NoteOn();

        // Full lifecycle: attack + decay + sustain + release
        int totalSamples = (int)(SampleRate * 2.0);
        var buffer = new double[totalSamples];

        // Note on for 1 second
        env.Process(buffer, (int)SampleRate);

        // Note off
        env.NoteOff();
        env.Process(buffer, (int)SampleRate);

        // Assert all values in range
        for (int i = 0; i < totalSamples; i++)
        {
            Assert.InRange(buffer[i], 0.0, 1.0);
        }
    }

    [Fact]
    public void ZeroAttack_DoesNotProduceNaN()
    {
        // Edge case: AttackTime=0 historically risks div-by-zero in coefficient calc.
        var env = new OttoSynth.Core.DSP.Envelopes.AdsrEnvelope
        {
            AttackTime = 0.0,
            DecayTime = 0.1,
            SustainLevel = 0.5,
            ReleaseTime = 0.1
        };
        env.SetSampleRate(SampleRate);
        env.NoteOn();

        var buf = new double[512];
        env.Process(buf, 512);

        for (int i = 0; i < 512; i++)
        {
            Assert.False(double.IsNaN(buf[i]), $"NaN at {i} with AttackTime=0");
            Assert.False(double.IsInfinity(buf[i]), $"Inf at {i} with AttackTime=0");
        }
    }

    [Fact]
    public void ZeroRelease_DoesNotProduceNaN()
    {
        var env = new OttoSynth.Core.DSP.Envelopes.AdsrEnvelope
        {
            AttackTime = 0.01,
            DecayTime = 0.01,
            SustainLevel = 0.5,
            ReleaseTime = 0.0
        };
        env.SetSampleRate(SampleRate);
        env.NoteOn();
        var buf = new double[512];
        env.Process(buf, 512);

        env.NoteOff();
        env.Process(buf, 512);

        for (int i = 0; i < 512; i++)
        {
            Assert.False(double.IsNaN(buf[i]), $"NaN at {i} with ReleaseTime=0");
        }
    }
}

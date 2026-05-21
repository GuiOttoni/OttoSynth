using System;
using Xunit;
using OttoSynth.Core.Voice;
using OttoSynth.Core.DSP.Oscillators;

namespace OttoSynth.Core.Tests.Voice;

public class VoiceManagerTests
{
    [Fact]
    public void NoteOn_AllocatesVoice()
    {
        var vm = new VoiceManager(maxVoices: 8);
        vm.SetSampleRate(44100);
        vm.SetWavetable(BasicWavetables.GenerateSine(), hasMipmap: false);

        vm.NoteOn(60, 100);

        Assert.Equal(1, vm.ActiveVoiceCount);
    }

    [Fact]
    public void NoteOff_ReleasesVoice()
    {
        var vm = new VoiceManager(maxVoices: 8);
        vm.SetSampleRate(44100);
        vm.SetWavetable(BasicWavetables.GenerateSine(), hasMipmap: false);

        vm.NoteOn(60, 100);
        Assert.Equal(1, vm.ActiveVoiceCount);

        vm.NoteOff(60);
        // Voice should still be active (in release phase), but will go idle after processing
        Assert.Equal(1, vm.ActiveVoiceCount);
    }

    [Fact]
    public void MultipleNotes_AllocatesDifferentVoices()
    {
        var vm = new VoiceManager(maxVoices: 8);
        vm.SetSampleRate(44100);
        vm.SetWavetable(BasicWavetables.GenerateSine(), hasMipmap: false);

        vm.NoteOn(60, 100); // C4
        vm.NoteOn(64, 100); // E4
        vm.NoteOn(67, 100); // G4

        Assert.Equal(3, vm.ActiveVoiceCount);
    }

    [Fact]
    public void VoiceStealing_WhenAllVoicesBusy()
    {
        int maxVoices = 4;
        var vm = new VoiceManager(maxVoices: maxVoices);
        vm.SetSampleRate(44100);
        vm.SetWavetable(BasicWavetables.GenerateSine(), hasMipmap: false);

        // Fill all voices
        for (int i = 0; i < maxVoices; i++)
        {
            vm.NoteOn(60 + i, 100);
        }
        Assert.Equal(maxVoices, vm.ActiveVoiceCount);

        // One more note should trigger voice stealing
        vm.NoteOn(70, 100);
        Assert.Equal(maxVoices, vm.ActiveVoiceCount); // Should still be maxVoices
    }

    [Fact]
    public void SameNote_RetriggersExistingVoice()
    {
        var vm = new VoiceManager(maxVoices: 8);
        vm.SetSampleRate(44100);
        vm.SetWavetable(BasicWavetables.GenerateSine(), hasMipmap: false);

        vm.NoteOn(60, 100);
        vm.NoteOn(60, 80); // Same note, different velocity — should retrigger

        Assert.Equal(1, vm.ActiveVoiceCount); // Still 1 voice
    }

    [Fact]
    public void AllNotesOff_ReleasesAllVoices()
    {
        var vm = new VoiceManager(maxVoices: 8);
        vm.SetSampleRate(44100);
        vm.SetWavetable(BasicWavetables.GenerateSine(), hasMipmap: false);

        vm.NoteOn(60, 100);
        vm.NoteOn(64, 100);
        vm.NoteOn(67, 100);

        vm.AllNotesOff();

        // All voices should be in releasing state (still "active" until envelope completes)
        // But they should all be marked as releasing
        foreach (var voice in vm.Voices)
        {
            if (voice.IsActive)
            {
                Assert.Equal(SynthVoice.VoiceState.Releasing, voice.State);
            }
        }
    }

    [Fact]
    public void Reset_KillsAllVoicesImmediately()
    {
        var vm = new VoiceManager(maxVoices: 8);
        vm.SetSampleRate(44100);
        vm.SetWavetable(BasicWavetables.GenerateSine(), hasMipmap: false);

        vm.NoteOn(60, 100);
        vm.NoteOn(64, 100);

        vm.Reset();

        Assert.Equal(0, vm.ActiveVoiceCount);
    }

    [Fact]
    public void Process_ProducesAudio()
    {
        var vm = new VoiceManager(maxVoices: 8);
        vm.SetSampleRate(44100);
        vm.SetWavetable(BasicWavetables.GenerateSaw(), hasMipmap: true);

        vm.NoteOn(60, 100);

        int bufferSize = 256;
        var left = new double[bufferSize];
        var right = new double[bufferSize];

        vm.Process(left, right, bufferSize);

        // Should have non-zero audio
        double energy = 0;
        for (int i = 0; i < bufferSize; i++)
            energy += left[i] * left[i];

        Assert.True(energy > 0.01, "Voice manager should produce audio when notes are active");
    }

    [Fact]
    public void Process_NoNotes_ProducesSilence()
    {
        var vm = new VoiceManager(maxVoices: 8);
        vm.SetSampleRate(44100);

        int bufferSize = 256;
        var left = new double[bufferSize];
        var right = new double[bufferSize];

        vm.Process(left, right, bufferSize);

        for (int i = 0; i < bufferSize; i++)
        {
            Assert.Equal(0.0, left[i]);
            Assert.Equal(0.0, right[i]);
        }
    }
}

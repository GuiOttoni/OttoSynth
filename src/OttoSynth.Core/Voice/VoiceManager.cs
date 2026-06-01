using System;
using OttoSynth.Core.DSP.Filters;
using OttoSynth.Core.DSP.Modulation;
using OttoSynth.Core.DSP.Oscillators;

namespace OttoSynth.Core.Voice;

/// <summary>
/// Manages a pool of synthesizer voices with polyphonic allocation and voice stealing.
/// Thread-safe for parameter updates from the UI thread.
/// </summary>
public sealed class VoiceManager
{
    private readonly SynthVoice[] _voices;
    private readonly int _maxVoices;
    private long _timestamp;

    // Sustain pedal state (pre-allocated, zero-alloc safe)
    private bool _sustainPedalOn = false;
    private readonly bool[] _sustainHeld = new bool[128];

    /// <summary>Maximum number of simultaneous voices.</summary>
    public int MaxVoices => _maxVoices;

    /// <summary>Number of currently active voices.</summary>
    public int ActiveVoiceCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _maxVoices; i++)
            {
                if (_voices[i].IsActive)
                    count++;
            }
            return count;
        }
    }

    /// <summary>Read-only access to voices (for diagnostics/UI).</summary>
    public ReadOnlySpan<SynthVoice> Voices => _voices.AsSpan();

    public VoiceManager(int maxVoices = 16, int maxBufferSize = 1024)
    {
        _maxVoices = maxVoices;
        _voices = new SynthVoice[maxVoices];
        _timestamp = 0;

        for (int i = 0; i < maxVoices; i++)
        {
            _voices[i] = new SynthVoice(maxBufferSize);
        }
    }

    /// <summary>Sets the sample rate for all voices.</summary>
    public void SetSampleRate(double sampleRate)
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            _voices[i].SetSampleRate(sampleRate);
        }
    }

    /// <summary>Sets the wavetable for all voices (OSC1 by default).</summary>
    public void SetWavetable(double[][] wavetable, bool hasMipmap)
    {
        SetWavetable(1, wavetable, hasMipmap);
    }

    /// <summary>Sets the wavetable for a specific oscillator (1-3) across all voices.</summary>
    public void SetWavetable(int oscIndex, double[][] wavetable, bool hasMipmap)
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            _voices[i].SetWavetable(oscIndex, wavetable, hasMipmap);
        }
    }

    /// <summary>Sets amp envelope (ENV1) parameters for all voices.</summary>
    public void SetEnvelopeParameters(double attack, double decay, double sustain, double release)
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            var env = _voices[i].EnvAmp;
            env.AttackTime = attack;
            env.DecayTime = decay;
            env.SustainLevel = sustain;
            env.ReleaseTime = release;
        }
    }

    /// <summary>Sets filter envelope (ENV2) parameters for all voices.</summary>
    public void SetFilterEnvelopeParameters(double attack, double decay, double sustain, double release)
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            var env = _voices[i].EnvFilter;
            env.AttackTime = attack;
            env.DecayTime = decay;
            env.SustainLevel = sustain;
            env.ReleaseTime = release;
        }
    }

    /// <summary>Sets the free envelope (ENV3) parameters across all voices.</summary>
    public void SetFreeEnvelopeParameters(double attack, double decay, double sustain, double release)
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            var env = _voices[i].EnvFree;
            env.AttackTime = attack;
            env.DecayTime = decay;
            env.SustainLevel = sustain;
            env.ReleaseTime = release;
        }
    }

    /// <summary>Sets unison configuration for a specific oscillator (1-3) across all voices.</summary>
    public void SetOscillatorUnison(int oscIndex, int voiceCount, double detuneCents, double spread)
    {
        for (int i = 0; i < _maxVoices; i++)
            _voices[i].SetUnison(oscIndex, voiceCount, detuneCents, spread);
    }

    /// <summary>Sets filter parameters for a specific filter (1 or 2) across all voices.</summary>
    public void SetFilterParameters(int filterIndex, StateVariableFilter.FilterMode mode,
        double cutoff, double resonance, double drive = 0.0, bool is24dB = false)
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            var voice = _voices[i];
            var filter = filterIndex == 1 ? voice.Filter1 : voice.Filter2;
            filter.Mode = mode;
            filter.Cutoff = cutoff;
            filter.Resonance = resonance;
            filter.Drive = drive;
            filter.Is24dB = is24dB;

            // Also update BASE values so modulation (env/LFO/mod matrix)
            // tracks the user-requested cutoff/resonance/drive.
            if (filterIndex == 1)
                voice.SetFilter1Base(cutoff, resonance, drive);
            else
                voice.SetFilter2Base(cutoff, resonance, drive);
        }
    }

    /// <summary>Sets formant filter parameters for filter 1 or 2 across all voices.</summary>
    public void SetFormantParameters(int filterIndex, double vowel, double shift)
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            var filter = filterIndex == 1 ? _voices[i].Filter1 : _voices[i].Filter2;
            filter.FormantVowel = vowel;
            filter.FormantShift = shift;
        }
    }

    /// <summary>Sets LFO parameters for a specific LFO (1-3) across all voices.</summary>
    public void SetLfoParameters(int lfoIndex, LfoGenerator.LfoShape shape,
        double rate, double depth, bool retrigger = true)
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            var lfo = lfoIndex switch
            {
                1 => _voices[i].Lfo1,
                2 => _voices[i].Lfo2,
                3 => _voices[i].Lfo3,
                _ => _voices[i].Lfo1
            };
            lfo.Shape = shape;
            lfo.Rate = rate;
            lfo.Depth = depth;
            lfo.Retrigger = retrigger;
        }
    }

    /// <summary>Sets oscillator mix levels and enable states across all voices.</summary>
    public void SetOscillatorMix(int oscIndex, double level, bool enabled)
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            var voice = _voices[i];
            switch (oscIndex)
            {
                case 1: voice.Osc1Level = level; voice.Osc1Enabled = enabled; break;
                case 2: voice.Osc2Level = level; voice.Osc2Enabled = enabled; break;
                case 3: voice.Osc3Level = level; voice.Osc3Enabled = enabled; break;
            }
        }
    }

    /// <summary>
    /// Sets oscillator tuning/position/warp/pan on the active source across all voices.
    /// Routes to the active source (single oscillator or unison engine) so parameters
    /// reach sub-oscillators even when unison is enabled.
    /// </summary>
    public void SetOscillatorParams(int oscIndex, int coarseTune, double fineTune,
        double position, double warpAmount, double pan)
    {
        for (int i = 0; i < _maxVoices; i++)
            _voices[i].SetOscillatorTuning(oscIndex, coarseTune, fineTune, position, warpAmount, pan);
    }

    /// <summary>Sets the warp mode on the active oscillator source across all voices.</summary>
    public void SetOscillatorWarp(int oscIndex, WavetableOscillator.WaveWarp warp)
    {
        for (int i = 0; i < _maxVoices; i++)
            _voices[i].SetOscillatorWarpMode(oscIndex, warp);
    }

    /// <summary>Sets portamento parameters across all voices.</summary>
    public void SetPortamento(double glideTime, SynthVoice.GlideMode mode)
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            _voices[i].GlideTime = glideTime;
            _voices[i].Glide = mode;
        }
    }

    /// <summary>Sets the routing from modulator to carrier (both 1-indexed) across all voices.</summary>
    public void SetOscillatorRouting(int modulator, int carrier, SynthVoice.OscRouting routing, double depth)
    {
        for (int i = 0; i < _maxVoices; i++)
            _voices[i].SetOscillatorRouting(modulator, carrier, routing, depth);
    }

    /// <summary>Returns the routing mode for a modulator→carrier pair (1-indexed) from voice 0.</summary>
    public SynthVoice.OscRouting GetOscillatorRouting(int modulator, int carrier)
        => _voices[0].GetOscRouting(modulator, carrier);

    /// <summary>Returns the FM depth for a modulator→carrier pair (1-indexed) from voice 0.</summary>
    public double GetOscillatorFmDepth(int modulator, int carrier)
        => _voices[0].GetOscFmDepth(modulator, carrier);

    /// <summary>Sets the filter routing mode across all voices.</summary>
    public void SetFilterRouting(SynthVoice.FilterRouting routing)
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            _voices[i].Routing = routing;
        }
    }

    /// <summary>Sets the filter envelope amount across all voices.</summary>
    public void SetFilterEnvAmount(double amount)
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            _voices[i].FilterEnvAmount = amount;
        }
    }

    // ─── Modulation Matrix Management ───────────────────────────

    /// <summary>Adds a modulation route to all voices' mod matrices.</summary>
    public int AddModRoute(ModSource source, ModDestination destination, double amount)
    {
        int routeIndex = -1;
        for (int i = 0; i < _maxVoices; i++)
        {
            routeIndex = _voices[i].ModMatrix.AddRoute(source, destination, amount);
        }
        return routeIndex; // Return the last index (all voices should be in sync)
    }

    /// <summary>Removes a modulation route from all voices.</summary>
    public void RemoveModRoute(int routeIndex)
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            _voices[i].ModMatrix.RemoveRoute(routeIndex);
        }
    }

    /// <summary>Sets the amount of a modulation route across all voices.</summary>
    public void SetModRouteAmount(int routeIndex, double amount)
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            _voices[i].ModMatrix.SetRouteAmount(routeIndex, amount);
        }
    }

    /// <summary>Clears all modulation routes from all voices.</summary>
    public void ClearModRoutes()
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            _voices[i].ModMatrix.ClearRoutes();
        }
    }

    /// <summary>Sets the macro controls reference for all voice mod matrices.</summary>
    public void SetMacros(MacroControls macros)
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            _voices[i].ModMatrix.SetMacros(macros);
        }
    }

    /// <summary>Updates the mod wheel value for all voice mod matrices.</summary>
    public void SetModWheel(double value)
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            _voices[i].ModMatrix.SetModWheel(value);
        }
    }

    /// <summary>Updates the pitch bend value for all voice mod matrices.</summary>
    public void SetPitchBend(double value)
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            _voices[i].ModMatrix.SetPitchBend(value);
        }
    }

    /// <summary>Updates the aftertouch value for all voice mod matrices.</summary>
    public void SetAftertouch(double value)
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            _voices[i].ModMatrix.SetAftertouch(value);
        }
    }

    /// <summary>
    /// Sets the sustain pedal (CC#64). When released, any held-by-sustain notes are freed.
    /// </summary>
    public void SetSustainPedal(bool on)
    {
        _sustainPedalOn = on;
        if (!on)
        {
            for (int n = 0; n < 128; n++)
            {
                if (_sustainHeld[n])
                {
                    _sustainHeld[n] = false;
                    NoteOffInternal(n);
                }
            }
        }
    }

    /// <summary>
    /// Triggers a note on. Allocates a voice using round-robin with voice stealing.
    /// </summary>
    public void NoteOn(int noteNumber, int velocity)
    {
        _sustainHeld[noteNumber] = false; // Re-press clears sustain hold

        _timestamp++;

        // 1. Check if this note is already playing — retrigger the same voice
        for (int i = 0; i < _maxVoices; i++)
        {
            if (_voices[i].IsActive && _voices[i].NoteNumber == noteNumber)
            {
                _voices[i].NoteOn(noteNumber, velocity, _timestamp);
                return;
            }
        }

        // 2. Find an idle voice
        for (int i = 0; i < _maxVoices; i++)
        {
            if (!_voices[i].IsActive)
            {
                _voices[i].NoteOn(noteNumber, velocity, _timestamp);
                return;
            }
        }

        // 3. All voices busy — steal one
        SynthVoice victim = FindStealCandidate();
        // Randomize fade time across [2, 5]ms to decorrelate clicks when many voices
        // are stolen at once (e.g. fast strumming a chord). Fixed-time fades stack
        // their micro-discontinuities at exactly the same sample offset.
        double fadeMs = 2.0 + Random.Shared.NextDouble() * 3.0;
        victim.ForceSteal(fadeMs);
        victim.NoteOn(noteNumber, velocity, _timestamp);
    }

    /// <summary>
    /// Releases a note. If sustain pedal is held, defers the release.
    /// </summary>
    public void NoteOff(int noteNumber)
    {
        if (_sustainPedalOn)
        {
            // Check that the note is actually sounding before marking it
            for (int i = 0; i < _maxVoices; i++)
            {
                if (_voices[i].NoteNumber == noteNumber &&
                    _voices[i].State == SynthVoice.VoiceState.Active)
                {
                    _sustainHeld[noteNumber] = true;
                    return;
                }
            }
        }
        NoteOffInternal(noteNumber);
    }

    private void NoteOffInternal(int noteNumber)
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            if (_voices[i].NoteNumber == noteNumber &&
                (_voices[i].State == SynthVoice.VoiceState.Active ||
                 _voices[i].State == SynthVoice.VoiceState.Stealing))
            {
                _voices[i].NoteOff();
                return;
            }
        }
    }

    /// <summary>
    /// Releases all notes (panic / all notes off).
    /// </summary>
    public void AllNotesOff()
    {
        _sustainPedalOn = false;
        Array.Clear(_sustainHeld, 0, _sustainHeld.Length);
        for (int i = 0; i < _maxVoices; i++)
        {
            if (_voices[i].IsActive)
                _voices[i].NoteOff();
        }
    }

    /// <summary>
    /// Kills all voices immediately (hard reset).
    /// </summary>
    public void Reset()
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            _voices[i].Reset();
        }
        _timestamp = 0;
    }

    /// <summary>
    /// Processes audio for all active voices and mixes into output.
    /// </summary>
    public void Process(double[] outputLeft, double[] outputRight, int sampleCount)
    {
        for (int i = 0; i < _maxVoices; i++)
        {
            if (_voices[i].IsActive)
            {
                _voices[i].Process(outputLeft, outputRight, sampleCount);
            }
        }
    }

    /// <summary>
    /// Finds the best candidate for voice stealing.
    /// Priority: Releasing > Stealing > Oldest Active.
    /// </summary>
    private SynthVoice FindStealCandidate()
    {
        SynthVoice? releasingCandidate = null;
        long oldestReleasingTime = long.MaxValue;

        SynthVoice? activeCandidate = null;
        long oldestActiveTime = long.MaxValue;

        for (int i = 0; i < _maxVoices; i++)
        {
            var voice = _voices[i];

            if (voice.State == SynthVoice.VoiceState.Releasing)
            {
                if (voice.NoteOnTimestamp < oldestReleasingTime)
                {
                    oldestReleasingTime = voice.NoteOnTimestamp;
                    releasingCandidate = voice;
                }
            }
            else if (voice.State == SynthVoice.VoiceState.Active)
            {
                if (voice.NoteOnTimestamp < oldestActiveTime)
                {
                    oldestActiveTime = voice.NoteOnTimestamp;
                    activeCandidate = voice;
                }
            }
        }

        return releasingCandidate ?? activeCandidate ?? _voices[0];
    }
}

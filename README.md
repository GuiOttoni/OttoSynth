# OttoSynth

**OttoSynth** is a polyphonic wavetable synthesizer built in C# / .NET 10. It runs as a **VST3 plugin** (Ableton Live, FL Studio, Reaper, etc.) and as a **standalone WPF application**, sharing a single DSP core.

The UI is inspired by Vital and Serum — dark Matrix/cyberpunk aesthetic, custom knobs, waveform and spectrum displays.

---

## Features

### Synthesis Engine
- **3 wavetable oscillators** — Sine, Saw, Square, Triangle with mipmap anti-aliasing
- **Serum-style wave warping** — Bend, Squeeze, Mirror, Fold, Sync, Phase, Formant and more
- **White noise oscillator**
- **Portamento / glide** — Off, Always, Legato modes with logarithmic interpolation
- **16-voice polyphony** with intelligent voice stealing

### Filters (11 modes)
| Mode | Algorithm |
|---|---|
| LP 12dB, HP 12dB, BP 12dB | Simper TPT SVF (unconditionally stable) |
| Notch, AllPass, Peak | Simper TPT SVF |
| Moog 24dB | Huovilainen ladder model |
| K35 LP, K35 HP | Korg MS-20 inspired 2-pole with tanh feedback |
| Comb +, Comb − | IIR resonant / FIR notch comb |

Dual filters with Serial, Parallel, and Split routing modes.

### Modulation
- **3 ADSR envelopes** — Amp, Filter, Free (with custom attack/decay/release curves)
- **3 LFOs** — Sine, Triangle, Saw Up/Down, Square, Sample & Hold; retriggerable
- **Modulation matrix** — 32 routes, 18+ sources (ENV1/2/3, LFO1/2/3, Velocity, Pitch Bend, Mod Wheel, Aftertouch, Macros, etc.), ~25 destinations
- **4 assignable macro knobs**
- **Sustain pedal** support (CC#64), zero-allocation implementation

### Effects Rack
8 serial effects, each with Bypass and Mix controls:

| Effect | Parameters |
|---|---|
| Distortion | Drive, Output Gain, Bit Depth; modes: Soft, Hard, Tube, Fold, Bit |
| Delay | Time L/R, Feedback, Damping, Ping-Pong |
| Reverb | Size, Decay, Damping, Pre-delay, Width |
| Chorus | Rate, Depth, Feedback |
| Phaser | Rate, Depth, Feedback, Stages |
| Flanger | Rate, Depth, Feedback |
| 3-Band EQ | Low/Mid/High freq + gain, Mid Q |
| Compressor | Threshold, Ratio, Attack, Release, Knee, Makeup Gain |

### Preset System
- **50 factory presets** across categories (Leads, Pads, Basses, Keys, etc.)
- **User presets** saved as `.otto` files (JSON) with one-click Save/Export/Import
- Backward-compatible with legacy `.ottopreset` files
- Preset captures full state: oscillators, filters, envelopes, LFOs, mod matrix, effects, glide

---

## Architecture

```
OttoSynth/
├── src/
│   ├── OttoSynth.Core        # DSP engine — zero dependencies (BCL only)
│   ├── OttoSynth.UI          # Custom WPF controls (SynthKnob, WaveformDisplay, etc.)
│   ├── OttoSynth.Standalone  # WPF app + NAudio audio/MIDI
│   └── OttoSynth.Plugin      # VST3 host via AudioPlugSharp
└── tests/
    └── OttoSynth.Core.Tests  # 72 xUnit tests
```

**Key design principles:**
- **Zero-allocation audio thread** — all buffers pre-allocated; no GC pressure during `ProcessAudio`
- **UI ↔ Audio separation** — parameter writes are atomic `double` fields; no events from the audio thread
- **Internal DSP in `double`** — converted to `float` only at the NAudio/VST3 boundary
- **Pluggable components** — each DSP unit (`WavetableOscillator`, `StateVariableFilter`, `AdsrEnvelope`, `LfoGenerator`) is independently testable

---

## Requirements

- **OS:** Windows 10/11 (64-bit)
- **Runtime:** [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- **DAW (plugin):** Any VST3-compatible host (Ableton Live 11+, FL Studio 21+, Reaper 6+, etc.)

---

## Build & Run

```powershell
# Restore packages
dotnet restore OttoSynth.slnx

# Run standalone app
dotnet run --project src/OttoSynth.Standalone -c Release

# Run tests
dotnet test tests/OttoSynth.Core.Tests

# Build everything in Release
dotnet build OttoSynth.slnx -c Release
```

### VST3 Plugin

1. Build `OttoSynth.Plugin` in Release
2. Copy `AudioPlugSharpVst.vst3` (from the NuGet package) and rename to `OttoSynth.PluginBridge.vst3`
3. Copy the matching `.runtimeconfig.json`
4. Drop all files into `C:\Program Files\Common Files\VST3\OttoSynth\`

---

## Preset Files (.otto)

User presets are stored in `%APPDATA%\OttoSynth\Presets\` as `.otto` files (standard JSON).

- **Save** — captures current engine state to a named `.otto` file
- **Import** — load any `.otto` or `.ottopreset` file from disk
- **Export** — save to a custom path for sharing

---

## Project Status

| Phase | Status | Description |
|---|---|---|
| Phase 1 — DSP Foundation | ✅ Complete | Engine, polyphony, MIDI |
| Phase 2 — Full Synthesis | ✅ Complete | 3 OSCs + Serum-style warp, 2 filters, 3 envelopes, 3 LFOs |
| Phase 3 — Modulation Matrix | ✅ Complete | 32 routes, 18+ sources, ~25 destinations |
| Phase 4 — WPF UI | ✅ Complete | Custom controls + Matrix/cyberpunk layout |
| Phase 5 — VST3 Plugin | ✅ Complete | Entry point + parameter mapping |
| Phase 6 — Effects Rack | ✅ Complete | 8 effects |
| Phase 7 — Preset System | ✅ Complete | JSON + 50 factory presets + .otto format |
| Phase 8 — Polish | ✅ Complete | Moog Ladder, DC Blocker, additional tests |
| Phase 9 — Filters & UX | ✅ Complete | 11 filter modes, knob manual input popup, portamento, sustain pedal |
| Phase 10 — UI Completion | 🔄 Planned | Effects editor, Filter 2 UI, Mod Matrix editor, Unison |
| Phase 11 — MIDI & Playability | 🔄 Planned | MIDI Learn, Expression, Velocity curves |
| Phase 12 — Synthesis Expansion | 🔄 Planned | LFO sync, Formant filter, Wavetable library |

---

## Tech Stack

| Layer | Technology |
|---|---|
| Language | C# 13 / .NET 10 |
| UI | WPF (Windows Presentation Foundation) |
| Audio output | NAudio (WaveOut, ~50ms latency) |
| MIDI input | NAudio.Midi |
| VST3 hosting | AudioPlugSharp |
| Tests | xUnit |
| Docs | Docusaurus 3 |

---

## License

Private project — all rights reserved.

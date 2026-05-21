---
sidebar_position: 11
title: Arquivos por Pasta
---

# OttoSynth — Lista de Arquivos

:::info
Cada arquivo `.cs` do projeto, com seu propósito em uma linha.
Use isto para localizar rapidamente onde algo está implementado.
:::

---

## src/OttoSynth.Core/

| Arquivo | Propósito |
|---|---|
| `SynthEngine.cs` | Fachada principal do motor. Orquestra voices + effects + master volume |

### DSP/Utils/
| Arquivo | Propósito |
|---|---|
| `MathUtils.cs` | Conversões DSP (MIDI→Hz, dB↔linear), FastSin, Hermite, PolyBLEP, SoftClip |
| `AudioBuffer.cs` | Buffer estéreo (L/R) com tamanho fixo |
| `DcBlocker.cs` | Filtro HP de 1 pólo para remover offset DC |

### DSP/Oscillators/
| Arquivo | Propósito |
|---|---|
| `WavetableOscillator.cs` | Oscilador wavetable com Hermite interp + multi-frame morphing |
| `BasicWavetables.cs` | Gera wavetables (Sine, Saw, Square, Triangle) com mipmap |
| `NoiseOscillator.cs` | White e Pink noise |
| `UnisonEngine.cs` | Empilhamento de até 16 vozes com detune/spread/phase rand |

### DSP/Filters/
| Arquivo | Propósito |
|---|---|
| `StateVariableFilter.cs` | SVF Chamberlin: LP/HP/BP/Notch + 24dB cascade + drive + key tracking |
| `MoogLadderFilter.cs` | Moog 4-pole ladder com tanh + 2× oversampling |

### DSP/Envelopes/
| Arquivo | Propósito |
|---|---|
| `AdsrEnvelope.cs` | ADSR com curvas ajustáveis e ForceRelease para voice stealing |

### DSP/Modulation/
| Arquivo | Propósito |
|---|---|
| `LfoGenerator.cs` | LFO 6 formas (sine, tri, saw up/down, square, S&H) com retrigger |
| `ModSource.cs` | Enum das sources (env, lfo, velocity, macro, etc.) |
| `ModDestination.cs` | Enum dos destinos + ModDestinationInfo com ranges |
| `ModRoute.cs` | Struct source→dst com amount, active |
| `ModMatrix.cs` | Matriz de até 32 rotas, processamento per-block |
| `MacroControls.cs` | 4 knobs globais 0..1 |

### DSP/Effects/
| Arquivo | Propósito |
|---|---|
| `IEffect.cs` | Interface base de todo efeito |
| `EffectBase.cs` | Implementa Bypass + Mix dry/wet com buffer dry pre-alocado |
| `EffectsChain.cs` | Lista ordenável de efeitos (Add, Insert, Move, RemoveAt) |
| `Distortion.cs` | 4 modos: Overdrive, Waveshape, Bitcrush, Foldback |
| `Delay.cs` | Stereo delay com feedback, damping, ping-pong |
| `Reverb.cs` | FDN com 4 delays + Hadamard mix + pre-delay |
| `Chorus.cs` | 3-voice chorus com modulated delay |
| `Phaser.cs` | Cascata de all-pass + LFO sweep |
| `Flanger.cs` | Modulated short delay + feedback |
| `Eq3Band.cs` | 3-band biquad EQ (low shelf + peak + high shelf) |
| `Compressor.cs` | Feedforward log-domain compressor com soft knee |

### Voice/
| Arquivo | Propósito |
|---|---|
| `SynthVoice.cs` | Voz completa: 3 osc + noise + 2 filters + 3 envs + 3 LFOs + ModMatrix |
| `VoiceManager.cs` | Pool de vozes, allocation, voice stealing, parameter propagation |

### Midi/
| Arquivo | Propósito |
|---|---|
| `MidiProcessor.cs` | Parser de bytes MIDI → MidiEvent struct |

### Preset/
| Arquivo | Propósito |
|---|---|
| `PresetData.cs` | POCOs serializáveis com todos os parâmetros |
| `PresetManager.cs` | Save/Load JSON, Capture, Apply ao engine, Scan diretório |
| `FactoryPresets.cs` | 50+ presets fábrica em 8 categorias |

---

## src/OttoSynth.UI/

### Themes/
| Arquivo | Propósito |
|---|---|
| `DarkTheme.xaml` | ResourceDictionary com paleta, brushes, styles base |

### Controls/
| Arquivo | Propósito |
|---|---|
| `SynthKnob.cs` | Knob rotativo com mouse drag, bipolar, default, mod ring |
| `WaveformDisplay.cs` | Visualização wave em tempo real (StreamGeometry) |
| `SpectrumAnalyzer.cs` | FFT 512 com escala log de frequência |
| `EnvelopeEditor.cs` | Visualização ADSR (não interativo ainda) |
| `LfoDisplay.cs` | Visualização da forma de onda LFO (2 ciclos) |
| `PianoKeyboard.cs` | Teclado clicável com white/black keys |
| `ModMatrixGrid.cs` | Listagem visual das rotas de modulação |
| `EffectSlot.cs` | Card visual para slot de efeito |

---

## src/OttoSynth.Standalone/

| Arquivo | Propósito |
|---|---|
| `App.xaml`, `App.xaml.cs` | Entry point WPF |
| `MainWindow.xaml` | Layout principal (header + main grid + effects + keyboard + status) |
| `MainWindow.xaml.cs` | Code-behind: wiring UI ↔ SynthEngine, NAudio audio/MIDI, UI timer |
| `AssemblyInfo.cs` | Atributos do assembly |

---

## src/OttoSynth.Plugin/

| Arquivo | Propósito |
|---|---|
| `OttoSynthPlugin.cs` | Entry point AudioPlugSharp: parameter mapping, MIDI handlers, Process() |

---

## tests/OttoSynth.Core.Tests/

### DSP/
| Arquivo | Propósito | Testes |
|---|---|---|
| `WavetableOscillatorTests.cs` | Cobertura do oscilador wavetable | 5 |
| `AdsrEnvelopeTests.cs` | Cobertura do envelope ADSR | 5 |
| `StateVariableFilterTests.cs` | Cobertura do SVF | 5 |
| `NoiseOscillatorTests.cs` | Cobertura de white/pink noise | 3 |
| `LfoGeneratorTests.cs` | Cobertura das formas LFO | 4 |
| `MoogLadderTests.cs` | Cobertura do Moog filter | 3 |
| `ModMatrixTests.cs` | Cobertura da Modulation Matrix | 12 |
| `EffectsTests.cs` | Cobertura da EffectsChain e efeitos | 9 |

### Voice/
| Arquivo | Testes |
|---|---|
| `VoiceManagerTests.cs` | 3 |

### Preset/
| Arquivo | Testes |
|---|---|
| `PresetManagerTests.cs` | 5 |

**Total: 72 testes**, todos passando.

---

## Outros artefatos

| Arquivo / Pasta | Propósito |
|---|---|
| `OttoSynth.slnx` | Solution file (formato `.slnx`, novo do VS 2022) |
| `.context/` | Documentos de pesquisa, diretrizes (input do projeto) |
| `.context/research.md` | Pesquisa técnica inicial |
| `.context/directives.json` | Diretrizes para IA durante desenvolvimento |
| `.context/phases.md` | Plano das 8 fases |
| `.docs/` | Documentação completa (este conjunto) |

---

## Estatísticas rápidas

| Métrica | Valor |
|---|---|
| Linhas de código (Core, aprox) | ~6500 |
| Linhas de UI (controles + theme) | ~1500 |
| Linhas de testes | ~1200 |
| Total `.cs` files | ~50 |
| Total efeitos disponíveis | 8 |
| Total wavetables built-in | 4 (Sine, Saw, Square, Triangle) |
| Total presets fábrica | 50+ |
| Mod sources | 17 |
| Mod destinations | 25+ |

---

> **Fim da documentação.** Comece sempre por `01-VisaoGeral.md` se está vindo do zero.

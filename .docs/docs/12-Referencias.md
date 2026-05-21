---
sidebar_position: 12
title: Referências & Roadmap
---

# Referências & Roadmap

:::info
Tecnologias usadas para construir o OttoSynth, e ideias de melhoria inspiradas em sintetizadores open-source consagrados.
:::

## 1. Stack e bibliotecas usadas

### .NET / linguagens
| Item | Versão | Uso |
|---|---|---|
| .NET SDK | 10.0 | Runtime e compilador C# |
| C# | 12+ | Linguagem |
| WPF | net10.0-windows | Toolkit gráfico para Standalone/UI |

### NuGet packages
| Pacote | Versão | Onde | Razão |
|---|---|---|---|
| [AudioPlugSharp](https://github.com/mikeoliphant/AudioPlugSharp) | 0.7.9 | OttoSynth.Plugin | Bridge C# → VST3 |
| AudioPlugSharpHost | 0.7.9 | OttoSynth.Plugin | Suporte para hosting standalone |
| AudioPlugSharpWPF | 0.7.9 | OttoSynth.Plugin | Embed de WPF no plugin |
| [NAudio](https://github.com/naudio/NAudio) | 2.3.0 | OttoSynth.Standalone | I/O de áudio + MIDI input |
| [xUnit](https://xunit.net) | 2.5.3 | OttoSynth.Core.Tests | Framework de testes |
| coverlet.collector | 6.0.0 | OttoSynth.Core.Tests | Cobertura de código |
| Microsoft.NET.Test.Sdk | 17.8.0 | OttoSynth.Core.Tests | Test runner |

### Padrões de design
- **MVVM** light (data binding direto via dependency properties).
- **Strategy pattern** para algoritmos DSP plugáveis (`IEffect`, vários `EnvelopeMode`).
- **Object pooling** para vozes (`SynthVoice[]` pré-alocado).
- **Lock-free** com `double` atômico em .NET 64-bit (sem mutex no audio thread).

### Algoritmos DSP de referência
| Algoritmo | Fonte | Onde implementado |
|---|---|---|
| State Variable Filter (Chamberlin) | "Musical Applications of Microprocessors" (Hal Chamberlin, 1985) | `StateVariableFilter.cs` |
| Moog Ladder (Huovilainen model) | A. Huovilainen, "Non-Linear Digital Implementation of the Moog Ladder Filter" (DAFx 2004) | `MoogLadderFilter.cs` |
| RBJ EQ Cookbook (biquads) | [Robert Bristow-Johnson EQ Cookbook](https://www.w3.org/TR/audio-eq-cookbook/) | `Eq3Band.cs` |
| Hermite cubic interpolation | Standard catmull-rom variant | `MathUtils.HermiteInterpolation` |
| PolyBLEP anti-aliasing | Vesa Välimäki, Antti Huovilainen (2007) | `MathUtils.PolyBlep` |
| Voss-McCartney pink noise | Voss/McCartney algorithm | `NoiseOscillator.cs` |
| FDN (Feedback Delay Network) reverb | Schroeder/Stautner, Jot 1991 | `Reverb.cs` |
| Cooley-Tukey FFT radix-2 | Cooley & Tukey 1965 | `SpectrumAnalyzer.cs` |

---

## 2. Documentação Docusaurus

Este site é construído com:
- [Docusaurus 3.5](https://docusaurus.io) (React-based static site generator)
- [@docusaurus/theme-mermaid](https://docusaurus.io/docs/markdown-features/diagrams) para diagramas
- Tema customizado em `src/css/custom.css` (paleta Matrix verde/preto + Cascadia Mono)
- Servido via Docker Compose (`docker-compose.yml`) em desenvolvimento ou produção

Para rodar localmente sem Docker:
```bash
cd .docs
npm install
npm start
```

Com Docker:
```bash
cd .docs
docker compose up
# → http://localhost:3000
```

---

## 3. Inspirações: sintetizadores open-source

### [Vital](https://github.com/mtytel/vital) (C++ / open source)
Sintetizador moderno wavetable, gratuito. **Principais referências adotadas pelo OttoSynth**:
- Estrutura 3 oscs + warp + unison + 2 filters
- Modulation matrix visual
- Custom LFOs com pontos editáveis

**A adicionar (TODO)**:
- ✨ **Random LFO** — saídas diferentes a cada ciclo
- ✨ **Custom LFO** — usuário desenha a forma de onda
- ✨ **Sample player** como 4º oscilador
- ✨ **Visual feedback** das modulações na UI (mod rings nos knobs)
- ✨ **Spectral wavetable view** mostrando os harmônicos

### [Serum](https://www.xferrecords.com/products/serum) (C++, closed-source — referência conceitual)
Padrão da indústria de wavetable synths. **Referências conceituais**:
- ✅ **Warp modes** (Bend, Asym, Sync, FM, Fold, Drive) — implementado em `WavetableOscillator.WaveWarp`
- ✅ **Wavetable position** — implementado (knob "POSITION")
- TODO **Wavetable editor** — drawing/import/FFT edit de wavetables
- TODO **Unison engine** — 16 voices com detune, spread, blend, stack

### [Helm](https://github.com/mtytel/helm) (C++ / open source)
Predecessor do Vital, subtractive + monophonic-friendly. **A adicionar**:
- ✨ **Formant filter** (simulação vocal A/E/I/O/U) — referência em `Helm/src/synthesis/formant_filter.cpp`
- ✨ **Comb filter** para timbres metálicos / Karplus-Strong
- ✨ **Stutter / arpeggiator**

### [Surge XT](https://github.com/surge-synthesizer/surge) (C++ / open source)
Synth profissional com muitas features. **A adicionar**:
- ✨ **Macro modulation visual feedback** (ver no knob alvo quanto a macro está movendo)
- ✨ **Polyphonic aftertouch** routing
- ✨ **Twist FM** entre osciladores
- ✨ **Tuning import** (Scala SCL/KBM files)

### [ZynAddSubFX / Zyn-Mixxxer](https://github.com/zynaddsubfx/zynaddsubfx)
Synth Linux clássico. Notável por:
- ✨ Sistema de **bandas** (até 12) com layering e split keyboard

### [Geonkick](https://github.com/iurie-sw/geonkick) (Linux)
Sintetizador de percussão. Notável por:
- ✨ Envelope visual com **drag de pontos** (já listado como TODO no `EnvelopeEditor.cs`)

---

## 4. Roadmap consolidado

Ideias priorizadas para futuras versões do OttoSynth.

### 🔴 Alta prioridade
- **Mod ring nos knobs** — visualizar quanto de modulação está aplicada (sombreado externo do anel). Inspiração: Vital.
- **Drag-and-drop mod matrix** — arrastar uma source para um destino. Inspiração: Vital, Surge.
- **Envelope editor interativo** — arrastar pontos ADSR diretamente no controle. Inspiração: Geonkick.
- **Polyphonic aftertouch** — mod source per-note ao invés de global.
- **Wavetable editor** — desenhar forma de onda no mouse + import de áudio.

### 🟡 Média prioridade
- **Unison integration** no SynthVoice (já existe `UnisonEngine.cs`, falta wirar).
- **Sample player** como 4º oscilador.
- **Formant filter** + **comb filter** (referenciados na Phase 8 do plano).
- **Custom LFOs** — usuário desenha a forma do LFO (Vital-style).
- **Random LFO** — sample-and-hold com taxa controlável.
- **Tempo sync** para LFOs e delays (precisa BPM do host).

### 🟢 Baixa prioridade / polish
- **GUI VST3 embedded** (atualmente `HasUserInterface = false`).
- **Oversampling 2×** na distortion para reduzir aliasing.
- **MIDI Out** (atualmente só MIDI In).
- **Arpeggiator** integrado.
- **Step sequencer** (8/16 passos) como mod source.
- **Drone mode** (notas latched).
- **Multi-band compressor** ou EQ dinâmico.
- **Spectrum tilt** no analyzer (+3dB/oct para correção perceptual).
- **Cross-platform** via Avalonia/MAUI (substituir WPF) e NPlug (substituir AudioPlugSharp).

---

## 5. Livros / papers recomendados

| Título | Autor | Foco |
|---|---|---|
| "Designing Software Synthesizer Plug-Ins in C++" | Will Pirkle | Engenharia de plugins |
| "The Art of VA Filter Design" | Vadim Zavalishin | Filtros analógicos virtuais |
| "DAFX: Digital Audio Effects" | Udo Zölzer (ed.) | Efeitos clássicos |
| "Audio EQ Cookbook" | Robert Bristow-Johnson | Biquad EQs |
| "Designing Synthesizer Plugins in C++ with WDL-OL" | Mike Olive | Plugin frameworks |
| "Real-Time Audio Programming 101" | Ross Bencina | Regras audio thread |

---

## 6. Comunidades

- 🌐 [KVR Audio Forum](https://www.kvraudio.com/forum/) — desenvolvimento de plugins
- 🌐 [The Audio Programmer](https://theaudioprogrammer.com) — tutoriais e Discord ativo
- 🌐 [r/synthesizers](https://reddit.com/r/synthesizers) — comunidade
- 🌐 [DSP Stack Exchange](https://dsp.stackexchange.com) — perguntas técnicas
- 🌐 [Music DSP](https://www.musicdsp.org) — repositório de algoritmos

---

## 7. Licenças e atribuições

- Código do OttoSynth: licença a definir (provavelmente MIT ou GPLv3).
- **AudioPlugSharp**: licença MIT (Mike Oliphant).
- **NAudio**: licença Microsoft Public License (Mark Heath).
- **xUnit**: licença Apache 2.0.
- **VST3 SDK** (consumido indirectly via AudioPlugSharp bridge): licença Steinberg (free for non-commercial; GPLv3 ou commercial dependendo do uso).

> Antes de distribuir como produto comercial, aceitar o **VST3 License Agreement** da Steinberg.

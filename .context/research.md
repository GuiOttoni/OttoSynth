# OttoSynth — Pesquisa Técnica Completa

> Documento de pesquisa para a construção de um sintetizador wavetable VST3 em C#/.NET
> Data: 2026-05-20

---

## 1. Visão Geral do Projeto

O **OttoSynth** é um sintetizador wavetable polifônico com capacidades de modulação avançadas, inspirado em sintetizadores profissionais como **Vital**, **Serum** e **Helm**. O plugin será desenvolvido em **C#** e distribuído no formato **VST3** para uso em DAWs como Ableton Live, FL Studio, Bitwig Studio, etc.

---

## 2. Formato de Plugin — VST3

### 2.1 O que é VST3?
VST3 (Virtual Studio Technology 3) é o padrão da Steinberg para plugins de áudio. DAWs carregam arquivos `.vst3` que contêm código nativo (C/C++). Como desenvolvemos em C#, precisamos de uma camada de interoperabilidade.

### 2.2 Frameworks C# para VST3

| Framework | Abordagem | GUI | Plataformas | Status |
|---|---|---|---|---|
| **AudioPlugSharp** | C++/CLI bridge | WPF / WinForms | Windows | Ativo, NuGet |
| **NPlug** | NativeAOT (.NET 7+) | IAudioPluginView | Win/Mac/Linux | Ativo, GitHub |
| **VST.NET** | Interop wrapper | WPF / WinForms | Windows | VST2 (legado) |
| **SharpSoundDevice** | Abstração VST 2.4 | Custom | Windows | Manutenção |

### 2.3 Recomendação: **AudioPlugSharp**
- **Razão principal**: Suporte nativo a WPF para GUI rica, NuGet simplificado, exemplos prontos.
- **Alternativa futura**: NPlug se precisarmos de cross-platform (Mac/Linux).
- AudioPlugSharp cuida de toda a comunicação VST3 ↔ DAW, permitindo foco total no DSP e UI.

### 2.4 Licenciamento VST3
- Plugins closed-source: Licença proprietária da Steinberg (gratuita para uso).
- Plugins open-source: GPLv3.
- É necessário aceitar o VST3 License Agreement da Steinberg.

---

## 3. Arquitetura do Sintetizador

### 3.1 Signal Flow (Fluxo de Sinal)

```
MIDI Input
    │
    ▼
┌─────────────────┐
│  Voice Manager   │ ← Polifonia (até 16 vozes)
│  (Allocation)    │
└────────┬────────┘
         │ (por voz)
         ▼
┌─────────────────────────────────────────────────────┐
│  Oscillator 1  ──┐                                   │
│  Oscillator 2  ──┼──► Mixer ──► Filter 1 ──► Filter 2│
│  Oscillator 3  ──┘                                   │
│  Noise OSC     ──────────────────────────────────────│
│                                                       │
│  Envelope 1 (Amp)                                    │
│  Envelope 2 (Filter)                                 │
│  Envelope 3 (Mod)                                    │
│  LFO 1, LFO 2, LFO 3                               │
└────────────────────┬────────────────────────────────┘
                     │
                     ▼
              ┌──────────────┐
              │  Amp Envelope │
              │  (Volume)     │
              └──────┬───────┘
                     │
                     ▼
              ┌──────────────┐
              │  Effects Rack │
              │  (Global)     │
              └──────┬───────┘
                     │
                     ▼
              Audio Output (L/R)
```

### 3.2 Componentes Principais

#### 3.2.1 Osciladores Wavetable (×3)
- **Wavetable**: Coleção de formas de onda single-cycle armazenadas em memória
- **Morphing**: Sweep suave entre posições da wavetable via interpolação
- **Unisson**: Até 16 vozes por oscilador com detune, spread e phase randomization
- **Wavetable Editor**: Importar/criar/editar wavetables (desenho, FFT, importação de áudio)
- **Anti-aliasing**: PolyBLEP para osciladores tradicionais, mipmap de wavetable para wavetable

#### 3.2.2 Oscilador de Noise (×1)
- White noise, pink noise, e sample-based noise
- Usado para texturas, percussão e efeitos atmosféricos

#### 3.2.3 Filtros (×2)
Dois filtros em série, paralelo ou split routing:

| Tipo | Slope | Características |
|---|---|---|
| **SVF (State Variable)** | 12/24 dB/oct | LP, HP, BP, Notch, versátil para modulação |
| **Moog Ladder** | 24 dB/oct | Quente, agressivo, "vintage" |
| **Biquad** | 12 dB/oct (cascadeável) | EQ, geral |
| **Comb** | — | Efeitos metálicos, flanger |
| **Formant** | — | Simulação vocal |

**Implementação recomendada**: Começar com **SVF** (estável para modulação real-time), depois adicionar Moog Ladder.

#### 3.2.4 Envelopes ADSR (×3+)
- **Envelope 1**: Amplitude (obrigatório)
- **Envelope 2**: Filtro (cutoff modulation)
- **Envelope 3+**: Modulação livre (assinalável via mod matrix)
- Curvas ajustáveis (linear, exponencial, logarítmica)
- Tempo em ms ou synced ao BPM

#### 3.2.5 LFOs (×3+)
- Formas: Sine, Triangle, Saw, Square, S&H (Sample & Hold), Custom
- Rate: Free (Hz) ou Sync (1/4, 1/8, etc.)
- Fase e offset ajustáveis
- Opção de retrigger por note-on
- **Stereo mode**: LFO diferente para L/R (phase offset)

#### 3.2.6 Modulation Matrix
O coração da flexibilidade do sintetizador:
- **Sources**: Envelopes, LFOs, Velocity, Key Tracking, Mod Wheel, Aftertouch, Macros
- **Destinations**: Qualquer parâmetro (osc pitch, wavetable position, filter cutoff, pan, etc.)
- **Amount**: Bipolar (-100% a +100%)
- **Mod Remap**: Curva de resposta customizável por rota
- Interface drag-and-drop (arrastar source para destino)

#### 3.2.7 Effects Rack (Global)
Cadeia de efeitos reordenável:
- **Reverb** (algorithmic)
- **Delay** (stereo, ping-pong, synced)
- **Chorus** 
- **Phaser**
- **Flanger**
- **Distortion** (waveshaping, bitcrush, overdrive)
- **EQ** (parametric 3-band)
- **Compressor**
- **Filter** (global, independente dos voice filters)

---

## 4. DSP — Processamento Digital de Sinal

### 4.1 Anti-Aliasing

#### PolyBLEP (Polynomial Band-Limited Step)
```
Ideal para: Osciladores clássicos (Saw, Square, Triangle)
CPU: Muito baixo
Qualidade: Boa (quasi-bandlimited)

Algoritmo:
1. Gerar onda "naive" (dente-de-serra puro, por exemplo)
2. Aplicar correção polinomial nos pontos de descontinuidade
3. poly_blep(t, dt) retorna valor de correção baseado na fase
```

#### Oversampling
```
Ideal para: Processos não-lineares (distorção, saturação de filtro)
CPU: Alto (2x, 4x, 8x)
Qualidade: Excelente

Algoritmo:
1. Upsample (aumentar sample rate)
2. Processar a taxa elevada (Nyquist mais alto = menos aliasing)
3. Low-pass filter (anti-aliasing)
4. Downsample (decimação)
```

#### Wavetable Mipmap
```
Para wavetable oscillators especificamente:
1. Pré-gerar versões band-limited da wavetable para diferentes faixas de frequência
2. Selecionar o mipmap level baseado na frequência da nota
3. Interpolar entre levels para transições suaves
```

### 4.2 Interpolação de Wavetable
- **Linear**: Rápida, mas pode gerar aliasing
- **Hermite (cúbica)**: Boa qualidade com custo aceitável ← **Recomendada**
- **Sinc**: Máxima qualidade, custo alto (offline/pré-processamento)

### 4.3 Filtros Digitais — Implementação

#### State Variable Filter (SVF)
```csharp
// Pseudo-código SVF
double cutoff, resonance;
double ic1eq = 0, ic2eq = 0;

void Process(double input, out double lp, out double hp, out double bp) {
    double g = Math.Tan(Math.PI * cutoff / sampleRate);
    double k = 2.0 - 2.0 * resonance; // Q = 1/(2-2*res)
    double a1 = 1.0 / (1.0 + g * (g + k));
    double a2 = g * a1;
    double a3 = g * a2;
    
    double v3 = input - ic2eq;
    double v1 = a1 * ic1eq + a2 * v3;
    double v2 = ic2eq + a2 * ic1eq + a3 * v3;
    
    ic1eq = 2 * v1 - ic1eq;
    ic2eq = 2 * v2 - ic2eq;
    
    lp = v2;
    bp = v1;
    hp = input - k * v1 - v2;
}
```

#### Moog Ladder Filter (Huovilainen)
```
4 polos em cascata com feedback não-linear
- Modelo os transistores como tanh() waveshaping
- Feedback global com compensação de ganho
- Oversampling recomendado (2x) para estabilidade
```

### 4.4 Fórmulas Essenciais

```
Frequência MIDI → Hz:
  freq = 440 * 2^((noteNumber - 69) / 12)

Phase Increment:
  phaseInc = freq / sampleRate

Pitch Bend (14-bit):
  bendValue = (MSB << 7) | LSB  // 0-16383, centro = 8192
  bendSemitones = (bendValue - 8192) / 8192.0 * bendRange
  freqMultiplier = 2^(bendSemitones / 12)

dB → Linear:
  linear = 10^(dB / 20)

Linear → dB:
  dB = 20 * log10(linear)

Tempo sync (BPM):
  periodSeconds = (60.0 / bpm) * noteDivision
```

---

## 5. Gerenciamento de Vozes (Polifonia)

### 5.1 Voice Pool
- Pool fixo de vozes (padrão: 16, configurável até 32)
- Cada voz contém: 3 osciladores, 2 filtros, envelopes, LFOs
- Status: `Idle`, `Active`, `Releasing`, `Stealing`

### 5.2 Algoritmos de Alocação
| Algoritmo | Descrição |
|---|---|
| **Round Robin** | Cicla sequencialmente pelas vozes (padrão) |
| **Oldest** | Reutiliza a voz que está tocando há mais tempo |
| **Quietest** | Rouba a voz com menor amplitude |
| **Same Key** | Reatribui a mesma voz para a mesma nota MIDI |

### 5.3 Voice Stealing
Quando todas as vozes estão ocupadas:
1. Verificar vozes em estado `Releasing` (prioridade para roubar)
2. Se nenhuma em release: aplicar algoritmo (oldest/quietest)
3. Aplicar **rapid fade-out** (1-5ms) na voz roubada para evitar clicks
4. Proteção de notas: opcionalmente proteger a nota mais baixa (bass)

---

## 6. Entrada MIDI

### 6.1 Mensagens Suportadas
| Mensagem | Bytes | Uso |
|---|---|---|
| Note On | `0x90` + note + velocity | Iniciar nota |
| Note Off | `0x80` + note + velocity | Encerrar nota |
| Pitch Bend | `0xE0` + LSB + MSB | Bend de pitch (14-bit) |
| Mod Wheel | `0xB0` + 0x01 + value | Modulação |
| Aftertouch | `0xD0` + pressure | Pressão pós-toque |
| CC (geral) | `0xB0` + cc# + value | Controles contínuos |

### 6.2 Bibliotecas C# para MIDI
- **AudioPlugSharp**: Já fornece MIDI input via interface VST3
- **NAudio**: Para standalone e testes (`MidiIn`, `MidiOut`)
- **DryWetMidi**: Para manipulação avançada de arquivos MIDI

---

## 7. Interface Gráfica (GUI)

### 7.1 Tecnologia: WPF (via AudioPlugSharp)
AudioPlugSharp suporta WPF nativamente, permitindo:
- Rendering hardware-acelerado (DirectX via WPF)
- Custom controls com ControlTemplate
- Data Binding para parâmetros do sintetizador
- Animações suaves (60fps)

### 7.2 Controles Customizados Necessários
| Controle | Descrição |
|---|---|
| **Knob** | Rotary control com mouse drag, value display |
| **Slider** | Vertical/horizontal com range customizável |
| **Waveform Display** | Visualização em tempo real da forma de onda |
| **Spectrum Analyzer** | FFT display do espectro de frequência |
| **Wavetable 3D View** | Visualização 3D da wavetable (como Serum) |
| **Mod Matrix Grid** | Grid visual de source → destination |
| **Envelope Editor** | ADSR visual com drag nos pontos |
| **LFO Shape Display** | Visualização da forma do LFO |
| **Keyboard** | Piano virtual clicável |
| **Preset Browser** | Lista/busca de presets |

### 7.3 Layout Inspiração (Vital/Serum)
```
┌──────────────────────────────────────────────────────────┐
│  [Logo] [Preset Browser ▼]  [Menu]        [Settings]    │
├──────────┬──────────┬──────────┬────────────────────────┤
│  OSC 1   │  OSC 2   │  OSC 3   │   Waveform / Spectrum  │
│ [Wave]   │ [Wave]   │ [Wave]   │   [Visual Display]     │
│ Position │ Position │ Position │                         │
│ Unison   │ Unison   │ Unison   │                         │
├──────────┴──────────┴──────────┼────────────────────────┤
│  Filter 1      │  Filter 2     │   Modulation Matrix    │
│  [Type ▼]      │  [Type ▼]     │   [Grid/List]          │
│  Cutoff Res    │  Cutoff Res   │   Drag & Drop routing  │
├────────────────┴───────────────┼────────────────────────┤
│  ENV 1  │  ENV 2  │  ENV 3     │   LFO 1 │ LFO 2 │ LFO3│
│  [ADSR] │  [ADSR] │  [ADSR]   │   [Wave] │ [Wave]│[Wav]│
├────────────────────────────────┼────────────────────────┤
│  Effects: [Rev] [Del] [Cho] [Dist] [EQ] [Comp] [Phas]  │
├──────────────────────────────────────────────────────────┤
│  [Piano Keyboard - Clicável]                             │
└──────────────────────────────────────────────────────────┘
```

### 7.4 Design Visual
- **Tema escuro** (padrão da indústria)
- **Cores vibrantes** para indicadores (neon blue, green, purple)
- **Glassmorphism** sutil em painéis
- **Feedback visual** em tempo real (ondas se movendo, espectro animado)
- **Resolução**: Suportar DPI scaling (100%, 125%, 150%, 200%)

---

## 8. Persistência e Presets

### 8.1 Formato de Preset
- **JSON** ou **XML** para presets (fácil de editar, versionável)
- Estrutura: todos os parâmetros do synth + metadados (nome, autor, tags, categoria)
- Diretório padrão: `%APPDATA%/OttoSynth/Presets/`

### 8.2 Categorias de Preset
Bass, Lead, Pad, Pluck, Keys, Strings, FX, Drum, Sequence, Ambient

### 8.3 Init Preset
- Estado "zerado" do synth para começar do zero
- OSC1 = Saw, Filter = LP 100%, Envelope = default ADSR

---

## 9. Performance e Otimização

### 9.1 Considerações C# / .NET
- **Garbage Collector**: Evitar alocações no audio thread (object pooling)
- **Buffers pré-alocados**: Usar arrays fixos, evitar `new` no processamento
- **SIMD**: System.Numerics.Vector<T> para operações vetoriais
- **Span<T>**: Para manipulação eficiente de buffers sem cópias
- **Lock-free**: Comunicação UI ↔ Audio via lock-free queues (Interlocked)

### 9.2 Benchmarks Target
| Métrica | Target |
|---|---|
| Latência de processamento | < 5ms @ 44100 Hz |
| CPU por voz (idle) | < 1% |
| CPU total (16 vozes + FX) | < 25% de um núcleo |
| Tempo de carregamento | < 2 segundos |
| Memória RAM | < 200 MB |

### 9.3 Estratégias de Otimização
1. **Object pooling** para vozes e buffers
2. **Lookup tables** para funções trigonométricas (sin, cos, tan)
3. **SIMD** para processamento em batch de samples
4. **Buffer size adaptável** (64, 128, 256, 512, 1024 samples)
5. **Lazy initialization** de efeitos (não processar se bypass)

---

## 10. Estrutura de Projeto Proposta

```
OttoSynth/
├── .context/                  # Contexto do projeto (pesquisa, diretrizes)
│   ├── research.md            # Este documento
│   └── directives.json        # Diretrizes para IA
│
├── src/
│   ├── OttoSynth.Core/        # Engine DSP (Class Library)
│   │   ├── DSP/
│   │   │   ├── Oscillators/    # WavetableOscillator, NoiseOscillator
│   │   │   ├── Filters/        # SVFilter, MoogLadder, BiquadFilter
│   │   │   ├── Envelopes/      # ADSREnvelope
│   │   │   ├── LFO/            # LFOGenerator
│   │   │   ├── Effects/        # Reverb, Delay, Chorus, etc.
│   │   │   └── Utils/          # MathUtils, Interpolation, Lookup tables
│   │   ├── Modulation/
│   │   │   ├── ModMatrix.cs
│   │   │   ├── ModSource.cs
│   │   │   └── ModDestination.cs
│   │   ├── Voice/
│   │   │   ├── SynthVoice.cs
│   │   │   └── VoiceManager.cs
│   │   ├── Midi/
│   │   │   └── MidiProcessor.cs
│   │   ├── Preset/
│   │   │   ├── PresetManager.cs
│   │   │   └── PresetData.cs
│   │   └── SynthEngine.cs      # Motor principal
│   │
│   ├── OttoSynth.Plugin/      # VST3 Plugin (AudioPlugSharp)
│   │   ├── OttoSynthPlugin.cs  # Entry point VST3
│   │   └── ParameterMap.cs     # Mapeamento de parâmetros VST3
│   │
│   ├── OttoSynth.UI/          # Interface WPF
│   │   ├── Controls/           # Knob, Slider, WaveformDisplay, etc.
│   │   ├── Views/              # MainView, OscView, FilterView, etc.
│   │   ├── ViewModels/         # MVVM ViewModels
│   │   ├── Themes/             # Dark theme, colors, styles
│   │   └── Resources/          # Ícones, fontes, imagens
│   │
│   └── OttoSynth.Standalone/  # App standalone para teste
│       └── Program.cs
│
├── tests/
│   ├── OttoSynth.Core.Tests/  # Testes unitários do DSP
│   └── OttoSynth.Plugin.Tests/# Testes de integração VST3
│
├── presets/                    # Factory presets
│   ├── Init.json
│   ├── Bass/
│   ├── Lead/
│   └── Pad/
│
├── wavetables/                 # Wavetables padrão
│   ├── Basic/                  # Sine, Saw, Square, Triangle
│   ├── Analog/
│   ├── Digital/
│   └── Custom/
│
├── docs/                       # Documentação
│   ├── architecture.md
│   ├── dsp-guide.md
│   └── user-manual.md
│
├── OttoSynth.sln              # Solution file
└── README.md
```

---

## 11. Dependências e Pacotes NuGet

| Pacote | Versão | Uso |
|---|---|---|
| **AudioPlugSharp** | Latest | Framework VST3 + WPF bridge |
| **System.Numerics.Vectors** | Built-in | SIMD para DSP |
| **Newtonsoft.Json** | 13.x | Serialização de presets |
| **NAudio** | 2.x | Standalone audio I/O + testes |
| **xUnit** | Latest | Framework de testes |
| **BenchmarkDotNet** | Latest | Benchmarks de performance |

---

## 12. Referências e Recursos

### Livros e Papers
- "Designing Software Synthesizer Plug-Ins in C++" — Will Pirkle
- "The Art of VA Filter Design" — Vadim Zavalishin
- "DAFX: Digital Audio Effects" — Udo Zölzer
- Audio EQ Cookbook — Robert Bristow-Johnson

### Repositórios de Referência
- [AudioPlugSharp](https://github.com/mikeoliphant/AudioPlugSharp) — Framework C# VST3
- [NPlug](https://github.com/xoofx/NPlug) — Alternativa NativeAOT
- [Vital](https://github.com/mtytel/vital) — Synth open-source (C++, referência de arquitetura)
- [Helm](https://github.com/mtytel/helm) — Synth open-source predecessor do Vital

### Comunidades
- [KVR Audio Forum](https://www.kvraudio.com/forum/) — Desenvolvimento de plugins
- [The Audio Programmer](https://www.theaudioprogrammer.com/) — Tutoriais
- [r/synthesizers](https://www.reddit.com/r/synthesizers/) — Comunidade

---

## 13. Riscos e Mitigações

| Risco | Impacto | Mitigação |
|---|---|---|
| Performance do GC em C# | Glitches de áudio | Object pooling, pré-alocação, zero-alloc no audio thread |
| Complexidade do DSP | Bugs de áudio | Testes unitários extensivos, comparação com referências |
| Compatibilidade com DAWs | Plugin não carrega | Testar em múltiplas DAWs, usar VST3 validator |
| GUI performance | UI lagada | Separar thread de UI e Audio, throttle de updates visuais |
| AudioPlugSharp limitações | Funcionalidade faltando | Contribuir ao projeto ou migrar para NPlug |
| Cross-platform (futuro) | Apenas Windows | NPlug como alternativa futura, MAUI como GUI alternativa |

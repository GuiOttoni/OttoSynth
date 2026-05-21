---
sidebar_position: 3
title: DSP Engine
---

# OttoSynth — DSP Engine

:::info
Como cada componente do motor DSP funciona, com referências aos arquivos `.cs`.
:::

## 1. MathUtils — fundamentos matemáticos

`src/OttoSynth.Core/DSP/Utils/MathUtils.cs`

Métodos críticos:

| Método | O que faz | Uso |
|---|---|---|
| `MidiNoteToFrequency(int)` | 69→440Hz, fórmula `440 * 2^((note-69)/12)` | Em `NoteOn` |
| `DbToLinear(dB)` | `10^(dB/20)` | Conversão de ganho |
| `LinearToDb(linear)` | `20*log10(linear)` | Para displays |
| `FastSin(phase)` | Lookup table com 4096 pontos + interpolação linear | LFOs |
| `HermiteInterpolation(ym1,y0,y1,y2,t)` | Interpolação cúbica (3a ordem) entre 4 pontos | Wavetable read |
| `PolyBlep(t, dt)` | Correção polinomial para osciladores sem aliasing | Anti-aliasing |
| `SoftClip(x)` | tanh-approx `x*(27+x²)/(27+9x²)` | Limitação suave |
| `EqualPowerPan(pan)` | Retorna (L, R) com lei `cos/sin` | Panning |

### Lookup tables
Para evitar `Math.Sin/Cos` no hot path, há uma tabela `_sinTable` de 4097 doubles (potência de 2 + guard point para interpolação). Inicializada uma vez no static constructor.

---

## 2. AudioBuffer

`src/OttoSynth.Core/DSP/Utils/AudioBuffer.cs`

Estrutura simples que aloca dois arrays (L, R) com tamanho máximo. Usado como buffer intermediário em alguns componentes.

> Note: a maior parte do DSP usa `double[]` diretamente — `AudioBuffer` é uma conveniência para casos onde a abstração ajuda.

---

## 3. WavetableOscillator

`src/OttoSynth.Core/DSP/Oscillators/WavetableOscillator.cs`

### Conceito
Uma wavetable é um array de samples representando 1 ciclo de uma forma de onda. Tocar uma nota é "ler" essa tabela na taxa apropriada para a frequência.

### Como funciona
```
phase: [0..1)              ← acumulador
phaseIncrement = freq / sampleRate

a cada amostra:
   index = phase * tableSize           ← em [0..tableSize)
   amostra = HermiteInterp(table, index)
   phase += phaseIncrement
   if (phase >= 1) phase -= 1
```

### Hermite (cúbica)
4 pontos (ym1, y0, y1, y2) e uma fração `t ∈ [0,1)`. Mais qualidade que linear, custo aceitável (~4 muls + ~4 adds por sample).

### Multi-frame morphing
Se a wavetable tem múltiplas frames (e.g., [wave_quente, wave_brilhante, wave_metálica]), o `WavetablePosition` (0..1) seleciona o frame:
- Posição 0 = wave_quente
- Posição 0.5 = interpolação 50/50 entre frame_1 e frame_2
- Posição 1 = wave_metálica

### Mipmap (anti-aliasing)
Se `hasMipmap = true`, cada elemento de `_wavetable` é uma versão **band-limited** para uma faixa de freq diferente. O método `SelectTable()` escolhe qual usar baseado em `_phaseIncrement`. Como o sample rate efetivo da wavetable é fixo, frequências altas (próximas de Nyquist) precisam de menos harmonicos.

### Parâmetros
- `Level` (0..1)
- `Pan` (-1..+1)
- `CoarseTune` (-48..+48 semitones)
- `FineTune` (-100..+100 cents)
- `WavetablePosition` (0..1)
- `SetFrequency(Hz)` — chamado pelo NoteOn

---

## 4. BasicWavetables

`src/OttoSynth.Core/DSP/Oscillators/BasicWavetables.cs`

Gera 4 wavetables clássicas em runtime:
- **Sine** — só fundamental
- **Saw** — soma de harmônicos `Σ sin(k·2πx)/k`, com Nyquist limit por mipmap level
- **Square** — só harmônicos ímpares
- **Triangle** — só ímpares, com decay 1/n² + alternância de sinal

Cada uma é construída com **vários mipmap levels** (exceto Sine que não precisa).

### Como adicionar uma nova wavetable
1. Adicione um método estático `GenerateXxx()` retornando `double[][]`.
2. Em `SynthEngine.LoadDefaultWavetables`, adicione `_wavetables["Xxx"] = BasicWavetables.GenerateXxx(...)`.
3. Adicione `"Xxx"` ao ComboBox da UI.

---

## 5. NoiseOscillator

`src/OttoSynth.Core/DSP/Oscillators/NoiseOscillator.cs`

Dois tipos:
- **White noise**: `Random.NextDouble() * 2 - 1` (uniforme em [-1,+1])
- **Pink noise**: Voss-McCartney com 16 buckets, dá decay de -3dB/oitava

Saída sempre estéreo (L e R independentes para descorrelação).

---

## 6. StateVariableFilter (SVF) — multi-algoritmo

`src/OttoSynth.Core/DSP/Filters/StateVariableFilter.cs`

### Conceito
Filtro multi-modo que agrega **5 algoritmos distintos** selecionáveis via `FilterMode`. É o ponto de entrada único do sistema de filtragem — delega internamente para o algoritmo correto.

### Modos disponíveis

| FilterMode | Algoritmo | Caráter |
|---|---|---|
| `LowPass` | Simper TPT SVF | Transparente, estável |
| `HighPass` | Simper TPT SVF | Limpo, sem aliasing |
| `BandPass` | Simper TPT SVF | Pico de banda preciso |
| `Notch` | Simper TPT SVF | Rejeita frequência central |
| `AllPass` | Simper TPT SVF | Muda fase, preserva magnitude |
| `Peak` | Simper TPT SVF | Boost/cut na frequência central |
| `MoogLadder` | Huovilainen 4-pole | Vintage, quente, 24 dB/oct |
| `K35LP` | Korg MS-20 LP | Agressivo, com saturação |
| `K35HP` | Korg MS-20 HP | Nítido, com tanh feedback |
| `CombPositive` | IIR com damping | Metálico, pentes ressonantes |
| `CombNegative` | FIR (feedback negativo) | Hollow, "phase comb" |

### Algoritmo base: Simper TPT SVF

Topologia **Topology-Preserving Transform** (Andy Simper, Cytomic). Incondicionalmente estável — não produz NaN independente de cutoff ou resonância.

```
g  = tan(π × cutoff / sampleRate)
k  = 2 - 2 × Q                    (onde Q ∈ [0,1])
a1 = 1 / (1 + g × (g + k))
a2 = g × a1
a3 = g × a2

v3 = x - ic2
v1 = a1 × ic1 + a2 × v3           ← saída BP
v2 = ic2 + a2 × ic1 + a3 × v3     ← saída LP
ic1 = 2 × v1 - ic1                ← atualiza integrador 1
ic2 = 2 × v2 - ic2                ← atualiza integrador 2

LP = v2
HP = x - k×v1 - v2
BP = v1
Notch = x - k×v1
AP    = x - 2k×v1
Peak  = x - k×v1 - 2×v2
```

### Slope 24 dB/oct
Quando `Is24dB = true`, dois SVFs são cascateados. Cada SVF dá 12 dB/oct → cascata = 24.

### Algoritmo K35 (Korg MS-20)

Dois one-poles TPT em série com feedback tanh de 1 sample de atraso:

```
xIn = x - k × FastTanh(fb)        ← feedback do sample anterior
v1 = g × (xIn - s1);  lp1 = v1 + s1;  s1 = lp1 + v1
v2 = g × (lp1 - s2);  lp2 = v2 + s2;  s2 = lp2 + v2
fb = lp2
output = lp2 × (1 + k × 0.28)     ← ganho de compensação
```

O K35HP usa o complemento HP em cada estágio (`hp = x - lp`) e faz feedback da saída HP.

### Comb Positive

IIR com damping de um-polo na trilha de feedback. Buffer de 8192 samples:

```
delay_out = buffer[pos]
filtered  = delay_out + damp × (filtered_prev - delay_out)   ← LP de 1 polo
buffer[pos] = x + feedback × filtered
output = 0.5 × (x + filtered)
```

### Comb Negative

FIR simples (sem feedback): armazena o **input** no buffer (não output) e soma com o sinal atual negado, criando cancelamentos periódicos de fase.

### Drive e Key Tracking

`Drive` (0..1): pre-saturação `x *= 1 + drive×10` antes do filtro → tom mais quente.  
`KeyTracking` (0..1): `cutoffEffective = cutoff × (noteFreq / 440)^keyTracking`.

---

## 7. MoogLadderFilter

`src/OttoSynth.Core/DSP/Filters/MoogLadderFilter.cs`

Modelo Huovilainen — 4 estágios one-pole em cascata, com feedback global passando por `Math.Tanh()` (não-linearidade que dá o "warmth" característico).

```
x = input - stage3 × feedback
x = tanh(x)               ← saturação
stage0 = x×f + delay0×(1-f)
stage1 = stage0×f + delay1×(1-f)
stage2 = stage1×f + delay2×(1-f)
stage3 = stage2×f + delay3×(1-f)
output = stage3
```

Processa **2 vezes por sample** (2× oversampling interno) para estabilidade com resonância alta.

Apenas Low-Pass, 24 dB/oct. É o filtro **vintage** clássico. Acessível via `FilterMode.MoogLadder` no `StateVariableFilter` — não requer instanciação separada.

---

## 8. AdsrEnvelope

`src/OttoSynth.Core/DSP/Envelopes/AdsrEnvelope.cs`

### Modelo
4 estágios: **Attack → Decay → Sustain → Release**.

Cada estágio é uma **rampa exponencial** governada por um time constant. A curva pode ser ajustada via `attackCurve`/`decayCurve`/`releaseCurve` ∈ [-1, 1]:
- `-1` = log (rápido no início)
- `0` = linear
- `+1` = exp (lento no início, acelera)

### Estados
```
Idle → (NoteOn) → Attack → Decay → Sustain → (NoteOff) → Release → Idle
                                          ↑
                                  permanece até NoteOff
```

### ForceRelease(ms)
Para voice stealing: força um release ultra-rápido (3ms padrão) para evitar clicks ao roubar uma voz.

---

## 9. LfoGenerator

`src/OttoSynth.Core/DSP/Modulation/LfoGenerator.cs`

Oscilador low-frequency com 6 formas:
- Sine (lookup table)
- Triangle (analítica)
- Saw Up / Saw Down
- Square
- Sample & Hold (random a cada ciclo)

Saída bipolar (-1..+1) ou unipolar (0..+1) configurável.

### Retrigger
Se `retrigger = true`, a fase é resetada a cada NoteOn. Senão, o LFO continua "free running" no fundo (útil para LFOs lentos que devem se manter sincronizados entre notas).

---

## 10. UnisonEngine

`src/OttoSynth.Core/DSP/Oscillators/UnisonEngine.cs`

Empilha até 16 cópias do oscilador com:
- **Detune spread**: vozes distribuídas em torno da freq central (-detune..+detune cents).
- **Stereo spread**: voice 1 → left, voice 2 → right, etc.
- **Phase randomization**: fase inicial aleatória para evitar cancelamento.

> Atualmente o `UnisonEngine` existe mas não está totalmente integrado ao SynthVoice (deliverable parcial). O hook está pronto: basta o SynthVoice instanciar e chamar.

---

## 11. DcBlocker

`src/OttoSynth.Core/DSP/Utils/DcBlocker.cs`

Filtro high-pass de 1 pólo com corner ~5Hz:
```
y[n] = x[n] - x[n-1] + R * y[n-1]
R ≈ 1 - 2π*5/sampleRate
```

Remove o **offset DC** que pode acumular após distortion/saturação não-linear. Aplicado no final do `SynthEngine.ProcessAudio`.

---

## 12. Effects (resumo — detalhes em 05)

Pasta `src/OttoSynth.Core/DSP/Effects/`:
- `IEffect`, `EffectBase` (base classes)
- `Distortion` (4 tipos: Overdrive, Waveshape, Bitcrush, Foldback)
- `Delay` (estéreo, ping-pong, damping)
- `Reverb` (FDN com 4 delay lines + Hadamard mixing)
- `Chorus` (3 voices, stereo spread)
- `Phaser` (cascata de all-pass + LFO)
- `Flanger` (delay muito curto + feedback)
- `Eq3Band` (low shelf + peak + high shelf, biquads RBJ)
- `Compressor` (feedforward, log-domain detection, soft knee)

---

## 13. Como sintetizar 1 nota — código mínimo

```csharp
var engine = new SynthEngine(maxVoices: 16);
engine.Initialize(44100, 256);

// Ajusta envelope
engine.SetEnvelope(attack: 0.01, decay: 0.3, sustain: 0.7, release: 0.5);

// Toca nota
engine.ProcessMidiEvent(MidiEvent.NoteOn(60, 100));

// Buffer de saída
var left = new double[256];
var right = new double[256];
engine.ProcessAudio(left, right, 256);   // gera 256 samples

// Para a nota
engine.ProcessMidiEvent(MidiEvent.NoteOff(60));
engine.ProcessAudio(left, right, 256);   // continua processando release
```

---

> **Próximo**: `04-Modulation-System.md` para Modulation Matrix.

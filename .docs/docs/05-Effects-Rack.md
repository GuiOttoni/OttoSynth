---
sidebar_position: 5
title: Effects Rack
---

# OttoSynth — Effects Rack

:::info
Detalhe de cada efeito (algoritmo, parâmetros, onde editar).
:::

## 1. Visão geral

O **EffectsChain** (`DSP/Effects/EffectsChain.cs`) é uma lista ordenada de `IEffect`. Vive em `SynthEngine.Effects`. Após todas as vozes serem mixadas, a cadeia processa o áudio **uma vez** (efeito global, não per-voice).

```
[voices mix] → EffectsChain.Process → [DcBlocker] → [Master Vol] → [SoftClip] → Output
```

Cada efeito é **reordenável** (`EffectsChain.Move(from, to)`), tem **bypass** e **mix** individuais (herdados de `EffectBase`).

---

## 2. IEffect / EffectBase

`IEffect`:
```csharp
string Name { get; }
bool Bypass { get; set; }
double Mix { get; set; }                   // 0..1, dry/wet
void SetSampleRate(double sr);
void Process(double[] L, double[] R, int n);
void Reset();
```

`EffectBase`:
- Implementa Bypass / Mix.
- Mix == 1.0 → processa in-place (sem copiar dry).
- Mix < 1.0 → copia dry para buffers internos, processa wet em L/R, depois faz crossfade.
- Mix == 0.0 → early-return (sem trabalho).

> **Importante**: `MaxBufferSize` (default 4096) limita o tamanho de bloco. Buffers maiores são truncados! Aumente se sua DAW usa blocos maiores.

---

## 3. Distortion

`Distortion.cs` — 4 tipos:

### Overdrive
```
x = SoftClip(x * gain)
```
Saturação suave, controlável via `Drive` (0..1 → gain 1..40).

### Waveshape
```
x = tanh(x * gain)
```
Mais "harsh" que overdrive em altos níveis. Curva matematicamente correta.

### Bitcrush
```
steps = 2^BitDepth - 1
x = round((x + 1) * 0.5 * steps) * 2/steps - 1
```
Quantiza a amplitude para `BitDepth` bits. 8 bits = som chiptune típico.

### Foldback
```
while (|x| > threshold):
   if (x > threshold) x = 2*threshold - x
   else x = -2*threshold - x
```
Reflete o sinal nos limites de threshold. Cria harmônicos ímpares + pares; tom "ringy".

### Parâmetros
- `Drive` (0..1) — intensidade
- `Type` (enum) — Overdrive/Waveshape/Bitcrush/Foldback
- `BitDepth` (1..16) — só Bitcrush
- `OutputGain` (0..1) — compensação

---

## 4. Delay

`Delay.cs` — buffer circular estéreo (~2s @ 96kHz = 200k samples por canal).

### Estrutura
- 2 buffers `_bufferL`, `_bufferR` (200k samples cada)
- Write index move 1 a cada sample
- Read index = `(write - delaySamples) % size`

### Damping
Aplica um one-pole LP no caminho do feedback:
```
damped = damped * dampCoeff + tap * (1 - dampCoeff)
```
Cada repetição fica progressivamente mais escura — simula reverb-like aging.

### Ping-pong
Quando `PingPong = true`, o feedback do canal L vai para R e vice-versa. Tem que ter `TimeLeft = TimeRight` para o efeito clássico.

### Parâmetros
- `TimeLeft`, `TimeRight` (s, 0..2)
- `Feedback` (0..0.95)
- `Damping` (0..0.99) — alto = som progressivamente mais surdo
- `PingPong` (bool)

---

## 5. Reverb (FDN)

`Reverb.cs` — **Feedback Delay Network** com 4 delay lines.

### Algoritmo
```
1. Mix mono do input
2. Pre-delay (até 333ms)
3. Lê 4 delays (primos: 1116, 1356, 1422, 1617 samples)
4. Aplica damping (LP) em cada
5. Mistura via matriz de Hadamard (4x4 unitária, mistura tudo):
   m0 = 0.5*(d0 + d1 + d2 + d3)
   m1 = 0.5*(d0 - d1 + d2 - d3)
   m2 = 0.5*(d0 + d1 - d2 - d3)
   m3 = 0.5*(d0 - d1 - d2 + d3)
6. Escreve input + m_i * feedback em cada delay
7. Output: (d0+d2) → L, (d1+d3) → R, ajustado por Width
```

### Por que Hadamard?
A matriz unitária 4×4 espalha energia entre as linhas. Sem ela, cada delay seria um eco isolado — com ela, os echos se entrelaçam, criando densidade.

### Parâmetros
- `Size` (0..1) — escala os tempos de delay (0.5..2.0)
- `Decay` (0..1) — feedback (0.5..0.99)
- `Damping` (0..0.99) — LP no feedback
- `PreDelay` (s, 0..0.2)
- `Width` (0..1) — stereo width (0=mono, 1=full)

---

## 6. Chorus

`Chorus.cs` — 3 voices modulating short delay (~15ms ± 12ms).

```
3 LFOs com taxas levemente diferentes (rate, rate*0.87, rate*1.13)
Cada um modula a posição de leitura num buffer compartilhado
Output: dry * 0.7 + média_dos_taps
```

Estéreo: tap1+tap3 → L, tap2+tap4 → R (com phase offsets).

### Parâmetros
- `Rate` (0.01..10 Hz)
- `Depth` (0..1)
- `Feedback` (0..0.9)

---

## 7. Phaser

`Phaser.cs` — Cascata de all-pass filters varridos por LFO.

```
LFO sweep cutoff entre 200Hz e 2200Hz
Cada amostra: passa por N estágios (default 6) de all-pass
Cada all-pass: y = a*x + state; state = x - a*y
Feedback global = última saída * fb
Output: (dry + filtered) * 0.5
```

A magic é o número de **stages**: 6 dá phaser "thick", 12 dá phaser "intricate".

### Parâmetros
- `Rate` (Hz)
- `Depth` — controla amplitude do sweep
- `Feedback` (0..0.95)
- `Stages` (2..12)

---

## 8. Flanger

`Flanger.cs` — Como chorus, mas delay muito curto (1..9ms) + feedback forte.

```
LFO modula delay entre 1ms e 9ms
Feedback alto cria "metallic" resonance
```

Diferença vs Chorus:
- **Tempo de delay menor** → comb filter audível.
- **Feedback mais alto** (pode ser negativo).

### Parâmetros
- `Rate`, `Depth`
- `Feedback` (-0.95..+0.95) — negativo dá um tom diferente

---

## 9. Eq3Band

`Eq3Band.cs` — Equalizador 3 bandas: Low Shelf + Mid Peak + High Shelf.

Implementado como **3 biquads em cascata** (Robert Bristow-Johnson EQ Cookbook).

```
Cada biquad: y = b0*x + b1*x[-1] + b2*x[-2] - a1*y[-1] - a2*y[-2]
Direct Form II Transposed (numericamente estável)
```

Coeficientes recalculados apenas quando parâmetros mudam (caching via dirty flag).

### Parâmetros
- `LowFreq` (Hz), `LowGainDb` (-24..+24)
- `MidFreq`, `MidGainDb`, `MidQ` (0.1..10)
- `HighFreq`, `HighGainDb`

---

## 10. Compressor

`Compressor.cs` — Feedforward, detecção em dB.

```
1. inMax = max(|L|, |R|)
2. inDb = 20*log10(inMax)
3. Compute target gain reduction (dB):
   - Below threshold-knee/2: 0
   - Above threshold+knee/2: (inDb - threshold) * (1 - 1/ratio)
   - In knee region: quadratic interpolation
4. Smooth (attack/release ballistics)
5. Apply: L *= 10^(-gr/20) * makeup
```

### Parâmetros
- `ThresholdDb` (-60..0)
- `Ratio` (1..20)
- `Attack`, `Release` (s)
- `KneeDb` (0..24, 0=hard knee)
- `MakeupGainDb` (-12..+24)

---

## 11. Adicionar um efeito novo — checklist

1. Criar classe `MyEffect : EffectBase` em `DSP/Effects/`.
2. Override:
   - `Name`
   - `ProcessInternal(L, R, n)` — só processa wet
   - `Reset()`
   - opcional: `SetSampleRate(sr)` (sempre `base.SetSampleRate(sr)`!)
3. Adicionar parâmetros expostos como `public double X { get; set; }`.
4. Garantir **zero alocação** em `ProcessInternal` (use buffers preallocated no constructor).
5. (Opcional) Adicionar entrada no `PresetManager.CaptureEffect` e `CreateEffect` para suporte a presets.
6. (Opcional) Adicionar ao standalone:
   ```csharp
   _engine.Effects.Add(new MyEffect());
   ```
7. Testar com testes unitários (ex: `EffectsTests.cs`).

---

## 12. Performance budget

Em meu teste informal com:
- 16 vozes ativas
- 3 osc + 1 noise + 2 filters por voz
- Cadeia: Distortion + Delay + Reverb + EQ + Compressor
- Buffer 256@44100

O total de CPU é tipicamente ~8-15% num single-core moderno (Intel i7-12700H). O efeito mais caro é Reverb (FDN com 4 delays).

Pontos onde otimizar:
- Reverb: poderia usar 8 delays em vez de 4 para densidade, mas custa 2× CPU.
- Spectrum analyzer da UI usa FFT 512 a 30fps — gasto razoável de UI thread.

---

> **Próximo**: `06-Voice-Management.md`.

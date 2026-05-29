---
sidebar_position: 14
title: Pesquisa — Moog Synthesizer
---

# Moog Synthesizer — Arquitetura, Funcionamento e Emulação Digital

:::info
Pesquisa técnica de referência sobre o sintetizador Moog, seu funcionamento analógico e estratégias
de emulação digital. Usada como guia para evoluções do OttoSynth.
:::

---

## 1. Contexto Histórico

Robert Moog lançou o primeiro sintetizador modular comercial em 1964, criando um instrumento baseado em
**síntese subtrativa por tensão controlada** (Voltage-Controlled Synthesis). A ideia central: começa-se
com um som harmonicamente rico e remove-se frequências para esculpir o timbre.

O **Minimoog Model D** (1970) condensou o sistema modular em um instrumento portátil e se tornou o
template de quase todo sintetizador analógico subsequente.

---

## 2. Arquitetura Geral — Fluxo de Sinal

```
[TECLADO / CV] ────────────────────────────────────────────┐
                                                           ▼
[VCO 1] ──┐                                         [Exp. Converter]
[VCO 2] ──┤──► [MIXER] ──► [VCF] ──► [VCA] ──► [SAÍDA]
[VCO 3] ──┤               ▲  ▲        ▲
[NOISE] ───┘               │  │        │
                           │  │        │
                    [ENV2] ┘  └─[LFO] └─[ENV1]
```

Cada bloco é **voltage-controlled**: a tensão de controle (CV) determina frequência de corte do filtro,
volume, pitch — tudo modulável por qualquer outra fonte de tensão.

---

## 3. VCO — Voltage-Controlled Oscillator

### Como funciona no analógico

O núcleo do VCO Moog é um **integrador sawtooth**:

1. Um capacitor é carregado por uma corrente constante proporcional à tensão de controle
2. A tensão sobe linearmente → forma o flanco ascendente do dente de serra
3. Um comparador detecta o threshold e dispara um reset instantâneo
4. O ciclo recomeça → `frequência = I / (C × Vthreshold)`

**Conversor exponencial**: o ouvido percebe pitch logaritmicamente. O padrão **1V/oitava**
usa a curva exponencial do transistor bipolar para converter tensão linear em corrente exponencial:

```
Icoletor = Is × e^(Vbe / Vt)     onde Vt ≈ 26mV a 25°C
```

**Problema clássico**: a curva exponencial é extremamente sensível à temperatura (~0.33% por °C).
Os primeiros Moogs levavam 1 hora para estabilizar — o drift térmico tornou-se parte do "som Moog".

### Waveforms derivadas

| Waveform | Como é gerada |
|---|---|
| Sawtooth | Diretamente do núcleo integrador |
| Triangle | Detector de pico + inversor de corrente |
| Square/Pulse | Comparador aplicado ao sawtooth ou triangle |
| Sine | Aproximação por diodo shaper a partir do triangle |

### Emulação digital do VCO

O problema principal é o **aliasing**: waveforms com descontinuidades (saw, square) geram
harmônicos que ultrapassam a frequência de Nyquist.

**PolyBLEP** — modifica amostras em torno de cada descontinuidade com correção polinomial.
Sem lookup table, custo baixíssimo. Eficaz até ~16kHz.

**Oversampling** — processa o oscilador em 2×/4× a taxa de amostragem com filtro anti-aliasing.
Qualidade máxima, custo proporcional ao fator.

**Wavetable com mipmap** — múltiplas versões da wavetable, cada uma com menos harmônicos,
selecionadas por nota. Eficiente para síntese polifônica. *(Abordagem atual do OttoSynth.)*

**Drift térmico simulado** — random walk de baixíssima frequência (~0.1-0.5 Hz) em cada
oscilador, ±2-3 cents de amplitude. Cria a "vida" orgânica do analógico:

```csharp
// Por voz/oscilador, a cada bloco:
_driftPhase += 0.3 / sampleRate;
_driftValue  = Math.Sin(_driftPhase * Math.PI * 2) * 0.002; // ±2 cents
// pitch *= Math.Pow(2, _driftValue / 12)
```

---

## 4. VCF — Voltage-Controlled Filter (Transistor Ladder)

Patenteado por Robert Moog em 1969. O componente mais influente e estudado do sintetizador.

### Estrutura física

```
INPUT ──► [Pair diferencial] ──► [Estágio 1] ──► [Estágio 2] ──► [Estágio 3] ──► [Estágio 4] ──► OUT
                                    │C1              │C2              │C3              │C4
                                   GND              GND              GND              GND
                                                                                            │
                                                                   FEEDBACK (negativo) ◄───┘
```

Quatro estágios RC de 1 polo com pares diferenciais de transistores NPN e capacitores.
Cada estágio atenua 6dB/oitava → total **24dB/oitava** (4 polos). O feedback negativo cria
**ressonância** (pico em torno da frequência de corte).

### Por que soa diferente de um filtro digital simples

O transistor opera em região não-linear. A relação tensão-corrente:

```
I = Is × (e^(V/Vt) - 1)
```

Para sinais de amplitude real, a aproximação linear não vale. Isso gera:

1. **Saturação suave** — quando o sinal é alto, o ganho cai graciosamente (comportamento `tanh`)
2. **Intermodulação** — frequências diferentes interagem produzindo harmônicos extras
3. **Compressão de ressonância** — ao aumentar o feedback, o filtro comprime o sinal de entrada
4. **Self-oscillation** — com ressonância máxima, o filtro oscila como um oscilador de sine puro

### Modelo de Huovilainen (2004)

O modelo não-linear mais influente. Quatro estágios com não-linearidade `tanh`:

```
y1[n] = y1[n-1] + f × (tanh(x[n] - 4k × y4[n-1]) - tanh(y1[n-1]))
y2[n] = y2[n-1] + f × (tanh(y1[n]) - tanh(y2[n-1]))
y3[n] = y3[n-1] + f × (tanh(y2[n]) - tanh(y3[n-1]))
y4[n] = y4[n-1] + f × (tanh(y3[n]) - tanh(y4[n-1]))
```

Onde:
- `f = 2 × sin(π × fc / fs)` — frequência de corte normalizada
- `k` — ressonância (0 a 4; k ≥ 4 → self-oscillation)
- `tanh(x)` — não-linearidade dos transistores
- Feedback usa `y4[n-1]` (sample anterior, evita loop algébrico)

:::caution Oversampling obrigatório
O modelo Huovilainen requer **oversampling de pelo menos 2×** com as não-linearidades ativas,
pois a saturação gera harmônicos que podem exceder Nyquist.
:::

### Outros modelos

| Modelo | Autor | Característica |
|---|---|---|
| Stilson/Smith | Tim Stilson | Linear, rápido, sem não-linearidade |
| Krajeski | Aaron Krajeski | Equação de estado, mais precisa que Stilson |
| RK simulation | Miller Puckette (Bob~) | Runge-Kutta, fisicamente preciso, custo alto |
| Improved | D'Angelo & Välimäki (2013) | Delay-free loop, melhor estabilidade numérica |

Repositório de referência em C++: [ddiakopoulos/MoogLadders](https://github.com/ddiakopoulos/MoogLadders)

---

## 5. VCA — Voltage-Controlled Amplifier

O mais simples dos três. No analógico: multiplicador de sinal com gain determinado por CV,
implementado com transistores em região linear ou OTAs.

No digital: `output[i] = input[i] × amplitude`. A dificuldade está nos clicks quando a amplitude
muda abruptamente — resolvido com smoothing de 1-5ms.

---

## 6. Envelope Generator — ADSR

O envelope Moog clássico (módulo 911) foi co-desenvolvido com Vladimir Ussachevsky.

No analógico, um switch conecta/desconecta corrente em uma rede capacitiva — as curvas são
**naturalmente exponenciais** (RC), não lineares.

```
// Analógico realista (exponencial)
envelope = 1.0 - exp(-t / τ_attack)

// Digital ingênuo (linear)
envelope = t / T_attack
```

A diferença é sutil mas perceptível: o exponencial atinge 63% do pico em `τ` e depois desacelera
suavemente, soando mais "natural".

---

## 7. O "Som Moog" — Origem do Caráter

O caráter Moog não vem de um componente isolado, mas da **soma de imperfeições coerentes**:

| Característica | Causa | Efeito Sonoro |
|---|---|---|
| Saturação do filtro | `tanh` nos estágios do ladder | Calor, agressividade suave |
| Compressão de ressonância | Feedback reduz gain de entrada | Tensão ao girar o cutoff |
| Keyboard tracking no filtro | Cutoff acompanha o pitch | Timbre consistente entre oitavas |
| Drift entre osciladores | Sensibilidade térmica dos transistores | "Chorus" orgânico natural |
| Crosstalk e ruído | Capacitância parasita | Textura, "vida" |

---

## 8. Roteiro de Emulação para o OttoSynth

### Nível 1 — Estrutural (já implementado)

- [x] Síntese subtrativa: VCO → Filter → VCA
- [x] 3 osciladores com waveforms + warp
- [x] Filtro Moog 24dB (modelo Huovilainen — `MoogLadderFilter.cs`)
- [x] ADSR com curvas customizadas
- [x] Keyboard tracking no filtro
- [x] Portamento / glide

### Nível 2 — Caráter analógico

- [ ] **Oversampling 2× no filtro Ladder**: processar a 2× para o `tanh` não aliar
- [ ] **Drive de entrada no filtro**: ganho variável antes do VCF (emula o "input level" do Minimoog)
- [ ] **Drift por oscilador**: LFO ~0.3Hz, amplitude ±2 cents, independente por voz
- [ ] **Saturação de saída do VCA**: leve `tanh` no final da cadeia
- [ ] **Ruído de fundo**: −75dBFS de ruído branco misturado ao output

### Nível 3 — Fidelidade física

- [ ] **Self-oscillation do filtro**: com `k ≥ 4`, o ladder oscila com sine pura rastreando o cutoff
- [ ] **Compressão de ressonância**: ao aumentar resonance, reduzir gain de entrada proporcionalmente
- [ ] **Curvas exponenciais no ADSR**: substituir lineares por exponenciais para attack/release
- [ ] **Drift de LFO entre vozes**: LFOs têm leve variação de fase por voz

---

## 9. Referências

- [Moog synthesizer — Wikipedia](https://en.wikipedia.org/wiki/Moog_synthesizer)
- [The Moog Ladder Filter — North Coast Synthesis](https://northcoastsynthesis.com/news/modular-synthesis-intro-part-7-the-moog-ladder-filter/)
- [MoogLadders C++ implementations (GitHub)](https://github.com/ddiakopoulos/MoogLadders)
- [Non-Linear Digital Implementation of the Moog Ladder Filter — Huovilainen, DaFX 2004](https://dafx.de/paper-archive/2004/P_061.PDF)
- [Improved virtual analog model of the Moog ladder filter (ResearchGate)](https://www.researchgate.net/publication/261193653_An_improved_virtual_analog_model_of_the_Moog_ladder_filter)
- [Oscillator and Filter Algorithms for Virtual Analog Synthesis (ResearchGate)](https://www.researchgate.net/publication/220386519_Oscillator_and_Filter_Algorithms_for_Virtual_Analog_Synthesis)
- [VCO Cores & Exponential Converters — Jon Dent Blog](https://djjondent.blogspot.com/2019/06/vco-cores.html)
- [PolyBLEP Anti-Aliasing — Martin Finke](https://www.martin-finke.de/articles/audio-plugins-018-polyblep-oscillator/)
- [Analysis of the Moog Transistor Ladder — Tim Stinchcombe](https://www.timstinchcombe.co.uk/synth/Moog_ladder_tf.pdf)

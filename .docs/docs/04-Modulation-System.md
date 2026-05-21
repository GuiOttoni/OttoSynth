---
sidebar_position: 4
title: Sistema de Modulação
---

# OttoSynth — Sistema de Modulação

:::info
Como funciona a Modulation Matrix, suas sources, destinations e macros.
:::

## 1. Conceito geral

A **Modulation Matrix** permite **rotear qualquer fonte de modulação para qualquer parâmetro** com uma amount (bipolar -1..+1).

Exemplos:
- **LFO 1 → Filter Cutoff** (vibrato no filtro)
- **Envelope 3 → OSC1 Pitch** (efeito de bend)
- **Velocity → Filter Cutoff** (notas mais fortes abrem o filtro)
- **Macro 1 → várias coisas ao mesmo tempo** ("morph" knob)

A matriz suporta **até 32 rotas simultâneas** por voz. Cada voz tem sua própria instância de `ModMatrix`, mas todas compartilham as **mesmas rotas** (sincronizadas pelo `VoiceManager`).

---

## 2. Arquivos envolvidos

| Arquivo | Responsabilidade |
|---|---|
| `DSP/Modulation/ModSource.cs` | Enum das fontes (Envelope1, LFO2, Velocity, etc.) |
| `DSP/Modulation/ModDestination.cs` | Enum dos destinos + metadados (min, max, log scale) |
| `DSP/Modulation/ModRoute.cs` | Struct que conecta uma source a um destino com amount |
| `DSP/Modulation/ModMatrix.cs` | Processador per-block |
| `DSP/Modulation/MacroControls.cs` | 4 knobs globais |
| `Voice/SynthVoice.cs` | Consome a matriz (método `ApplyModulation`) |

---

## 3. Sources (fontes de modulação)

`ModSource` enum:

```
None = 0,

// Per-voice envelopes (unipolar 0..1)
Envelope1 = 1,    // ENV1 (Amp)
Envelope2 = 2,    // ENV2 (Filter)
Envelope3 = 3,    // ENV3 (Free)

// Per-voice LFOs (bipolar -1..+1)
Lfo1 = 10, Lfo2 = 11, Lfo3 = 12,

// Per-note (constante durante a nota)
Velocity = 20,        // 0..1 (do MIDI)
KeyTracking = 21,     // -1..+1 (centrado em C4 = nota 60)
NoteRandom = 22,      // 0..1 random por nota

// Global
ModWheel = 30,        // CC#1
PitchBend = 31,       // -1..+1
Aftertouch = 32,      // pressão (CC#74 / channel pressure)

// Macros
Macro1..4 = 40..43
```

Cada source é um valor numérico de [-1..+1] (bipolar) ou [0..+1] (unipolar). Para Envelopes/LFOs/Velocity/KeyTrack/Mod/etc, é a média do bloco — para per-sample seria custoso e raramente necessário.

### Adicionar uma source nova
1. Adicione um valor ao enum `ModSource`.
2. Em `ModMatrix.SetVoiceSources`, escreva o valor em `_sourceValues[(int)YourNewSource]`.
3. Pronto — a UI já pode usar.

---

## 4. Destinations (destinos)

`ModDestination` enum:

```
None = 0,

// OSC 1
Osc1Pitch (-48..+48 semitones),
Osc1WavetablePos (0..1),
Osc1Level (0..1),
Osc1Pan (-1..+1),

// OSC 2 / OSC 3 / Noise → análogos

// Filter 1 / Filter 2
Filter1Cutoff (20..20000 Hz, log),
Filter1Resonance (0..1),
Filter1Drive (0..1),

// LFO Rate/Depth
Lfo1Rate (0.01..30 Hz),
Lfo1Depth (0..1),

// Global
MasterVolume (0..1)
```

### ModDestinationInfo — metadados
Para cada destination, `ModDestinationInfo.GetInfo(dest)` retorna:
- `MinValue` e `MaxValue` — limites do parâmetro.
- `DefaultValue`
- `IsLogarithmic` — se for `true` (Filter Cutoff), a modulação é interpretada em **oitavas** ao invés de unidades absolutas.

### Adicionar um destination novo
1. Adicione um valor ao enum `ModDestination`.
2. Adicione um case em `ModDestinationInfo.GetInfo(...)` para definir o range.
3. Em `SynthVoice.ApplyModulation()`, aplique a modulação ao componente alvo.

---

## 5. ModRoute

Struct que captura uma única ligação:

```csharp
public struct ModRoute {
    public ModSource Source;
    public ModDestination Destination;
    public double Amount;    // -1..+1
    public bool Active;
}
```

`IsValid` é true quando source ≠ None, destination ≠ None, active, |amount| > 0.0001.

---

## 6. ModMatrix — fluxo

Por bloco (chamado dentro de `SynthVoice.Process`):

```
1. SetVoiceSources(env1, env2, env3, lfo1, lfo2, lfo3, velocity, note, random)
   → preenche _sourceValues[64]

2. Process()
   → Para cada rota válida:
     - sourceValue = _sourceValues[(int)route.Source]
     - modulation = sourceValue * route.Amount
     - Se destino é log: _modOutput[dst] += modulation * 7.0  (oitavas)
       Senão: _modOutput[dst] += modulation * (max - min)
   → Resultado: _modOutput[128] tem a modulação total acumulada por destino

3. ApplyMod(destination, baseValue) ou GetModValue(destination)
   → SynthVoice usa esses valores para offsetar parâmetros
```

### Scale: linear vs log
**Linear destinations** (Osc1Level, Filter1Resonance, etc.):
```
final = clamp(baseValue + modOffset, min, max)
```
onde `modOffset = sourceValue * amount * (max - min)`.

**Logarithmic destinations** (Filter Cutoff):
```
final = clamp(baseValue * 2^modOctaves, min, max)
```
onde `modOctaves = sourceValue * amount * 7.0`. Com amount=1.0 e source=1.0, isso dá ±7 oitavas — toda a faixa audível.

---

## 7. Macros

`MacroControls` armazena 4 valores `double` (0..1). Cada macro é uma fonte da matriz (Macro1..Macro4).

Tipicamente:
- A UI conecta cada macro knob a `MacroControls[index]`.
- O usuário adiciona rotas no preset: e.g., **Macro1 → Filter Cutoff** com amount 0.5.
- Ao girar Macro1 na performance, todos os destinos ligados respondem.

Compartilhado entre todas as vozes (instância única em `SynthEngine.Macros`).

---

## 8. Como aplicar modulação a um parâmetro novo

Exemplo: você quer modular o **Pan do efeito Reverb**.

### Passo 1: adicionar o destination
```csharp
// ModDestination.cs
public enum ModDestination : byte {
    // ... existing values ...
    ReverbPan = 80,   // novo
}

// ModDestinationInfo.GetInfo
ModDestination.ReverbPan => new(-1.0, 1.0, 0.0),
```

### Passo 2: aplicar na cadeia de áudio
A modulação se aplica no `SynthVoice` (per-voice). Como Reverb é global (não per-voice), você precisa **mover** o cálculo para o `SynthEngine.ProcessAudio` ou criar uma rota global.

```csharp
// SynthEngine.ProcessAudio (após processar voices)
double reverbPanMod = _voiceManager.Voices[0].ModMatrix.GetModValue(ModDestination.ReverbPan);
// ... aplicar a algum parâmetro do reverb (precisaria adicionar Pan no Reverb.cs)
```

> Note que rotas per-voice modulam alvos **per-voice**; rotas globais são mais complicadas porque cada voz tem sua matriz. Em geral, prefira modular per-voice (Osc/Filter/Env por voz).

---

## 9. Snapshot per-block vs per-sample

A matriz **opera por bloco** (1 valor por destino para todo o buffer). Para envelopes e LFOs **lentos**, isso não introduz audible smoothing. Para parâmetros que precisam mudar dentro de um bloco (e.g., um LFO em 30Hz com buffer de 256@44k → 1.5 ciclos por bloco), isto cria "ziggurat" sutil — mitigado pelo fato de que destinations log-scale (cutoff) já tem alta resolução perceptual em oitavas.

> Se precisar de per-sample modulation no futuro, refatore `ModMatrix.Process` para processar dentro do loop principal de áudio (custo: 32 rotas × N samples × bloco).

---

## 10. Exemplos de patches comuns

```csharp
// LFO 1 modulando vibrato (fast tremolo)
engine.AddModRoute(ModSource.Lfo1, ModDestination.Osc1Pitch, 0.05);  // 5% × 48 = 2.4 semis

// Envelope 3 modulando filtro (filter sweep "plucky")
engine.AddModRoute(ModSource.Envelope3, ModDestination.Filter1Cutoff, 0.5);  // 0.5 × 7 = 3.5 oitavas

// Velocity modulando volume (expressividade)
engine.AddModRoute(ModSource.Velocity, ModDestination.MasterVolume, 0.3);

// Mod wheel abrindo o filtro
engine.AddModRoute(ModSource.ModWheel, ModDestination.Filter1Cutoff, 0.4);

// Macro 1 morphing entre wave positions de 3 oscs
engine.AddModRoute(ModSource.Macro1, ModDestination.Osc1WavetablePos, 1.0);
engine.AddModRoute(ModSource.Macro1, ModDestination.Osc2WavetablePos, -1.0);  // opposite
```

---

## 11. Limites e considerações

- **32 rotas no total** por voz. Suficiente para ~95% dos patches.
- Modulação é **somada** quando vários routes apontam para o mesmo destino.
- Source `None` ou Destination `None` é tratado como rota desativada.
- `route.Amount = 0` é tratado como inativa (early-out).

---

> **Próximo**: `05-Effects-Rack.md` para detalhes dos efeitos.

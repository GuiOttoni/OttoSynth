---
sidebar_position: 6
title: Voice Management
---

# OttoSynth — Voice Management

:::info
Como o motor aloca vozes, faz polifonia e voice stealing.
:::

## 1. VoiceManager

`src/OttoSynth.Core/Voice/VoiceManager.cs`

Responsabilidades:
- Manter um pool fixo de `SynthVoice` (default 16).
- Alocar uma voz no NoteOn.
- Encontrar a voz que está tocando uma nota no NoteOff.
- Propagar mudanças de parâmetros para todas as vozes.
- Fazer voice stealing quando o pool está cheio.

### Tamanho do pool
Definido no construtor: `new VoiceManager(maxVoices: 16)`. O número de vozes determina:
- **Polifonia máxima**: notas simultâneas.
- **Uso de RAM**: cada voz pré-aloca ~10 buffers de até 2048 samples = ~160KB.
- **CPU**: linear no número de vozes ativas (não no max).

> Recomendado: 16 para uso geral, 32 para pads/strings com muita sustain, 8 para baixos/leads monofônicos polyphonic-emulados.

---

## 2. Estados de uma voz

`SynthVoice.VoiceState`:

```
Idle      ── (NoteOn) ──►  Active
                              │
                              ├── (NoteOff) ──► Releasing
                              │                     │
                              ├── (Steal)   ──► Stealing
                              │                     │
                              └─ (Env Amp termina) ─┘
                                                    │
                                                    ▼
                                                  Idle
```

- **Idle**: voz disponível, processa nada.
- **Active**: nota está sendo tocada (envelope no Attack/Decay/Sustain).
- **Releasing**: NoteOff foi recebido, envelope no Release.
- **Stealing**: voz está sendo roubada — envelope em ForceRelease (3ms fadeout).

A voz volta para Idle quando o envelope amp finalmente sai do release (atinge nível 0).

---

## 3. NoteOn — algoritmo

```
1. Procurar voz já tocando MESMA NOTA → retrigger (preserva fase do LFO non-retrigger)
2. Procurar voz Idle → alocar
3. Voice stealing:
   a. Prefere a voz mais antiga em estado Releasing
   b. Se nenhuma, prefere a voz Active mais antiga
   c. Aplicar ForceSteal() (fadeout 3ms)
   d. Imediatamente NoteOn na voz
```

A propriedade `NoteOnTimestamp` (long monotônico) permite encontrar a "mais antiga" sem comparar wall-clock time.

---

## 4. NoteOff — algoritmo

```
Para cada voz:
   se voice.NoteNumber == noteNumber e voice.State em {Active, Stealing}:
      voice.NoteOff()
      return
```

> Apenas a primeira voz com aquela nota recebe NoteOff. Em prática isso funciona porque NoteOn já evita ter 2 vozes tocando a mesma nota (retrigger).

---

## 5. Voice stealing — detalhe

`VoiceManager.FindStealCandidate()`:

```
1. Procura vozes em Releasing (já estão sumindo, fácil roubar)
   → pega a mais antiga
2. Senão, procura vozes em Active
   → pega a mais antiga
3. Se nenhuma, _voices[0] (fallback)

Aplica victim.ForceSteal(3ms):
  - state = Stealing
  - envAmp.ForceRelease(3ms)
```

Por que 3ms? Tempo suficiente para evitar click audible mas curto o bastante para a nova nota soar imediatamente.

### Estratégias alternativas (não implementadas)
- **Quietest**: rouba a voz com menor amplitude (mais segura, requer scan extra).
- **Protect low note**: preserva a nota mais baixa (ideal para baixos).
- **Same key**: sempre alocar a mesma voz para a mesma nota (consistência).

> Para implementar, adicionar uma propriedade `AllocationStrategy` no `VoiceManager` e branch no `FindStealCandidate`.

---

## 6. Propagação de parâmetros

Cada setter de `VoiceManager` faz um loop:

```csharp
public void SetFilterParameters(...) {
   for (int i = 0; i < _maxVoices; i++)
      _voices[i].Filter1...= ...
}
```

Custo: O(maxVoices), ~16 iterações. Aceitável já que setters não são chamados no audio thread (são chamados pela UI no main thread).

> **Não é thread-safe contra audio thread** em sentido estrito: se o audio thread está lendo `_cutoff` enquanto a UI escreve, pode haver tear. Porém, double writes são atomicos em .NET 64-bit, então só haveria um valor antigo ou novo — nada quebrado.

---

## 7. Como adicionar polifonia variável (UI)

Se quiser que o usuário ajuste a polifonia via UI:

```csharp
// SynthEngine.cs — adicionar
public void SetMaxPolyphony(int max) {
    // Cuidado: realocar é caro. Melhor desativar vozes além de N.
    // ...
}
```

Solução simples: manter pool fixo grande (16-32) e ignorar vozes acima do limite ativo. Solução mais correta requer realocação completa.

---

## 8. Voice Process — o coração

`SynthVoice.Process(double[] outL, double[] outR, int n)`:

```
1. Se Idle, return.
2. Process envelopes (envAmp, envFilter, envFree)
   - Se envAmp terminou, state = Idle, return.
3. Process LFOs
4. Calcula averages do bloco para ModMatrix
5. ModMatrix.SetVoiceSources(...) + ModMatrix.Process()
6. ApplyModulation() — escreve nos parâmetros internos
7. Mix mono:
   - osc1.Process → temp[0..n] (estéreo, mas mixamos pra mono)
   - osc2.Process, osc3.Process, noise.Process
   - tudo em _mixLeft (mono)
8. Apply legacy filter env modulation (Phase 2 código)
9. filter1.Process(_mixLeft, _mixLeft, n)
   - se Serial: filter2.Process também
10. Amp envelope * Velocity → soma em outL/outR como estéreo
```

> Note: pan ainda não é fully implementado per-osc (atualmente o pan é stripped no mix mono). Fixing isto seria um TODO.

---

## 9. Métricas

`VoiceManager.ActiveVoiceCount` — propriedade conveniente para UI. Itera e conta vozes não-Idle.

> Computar isto a cada frame UI (~30fps) é OK. Não chame do audio thread.

---

## 10. Buffers pré-alocados por voz

Cada `SynthVoice` aloca **no construtor**:
```
_tempLeft, _tempRight       [maxBufferSize]
_mixLeft, _mixRight          [maxBufferSize]
_envAmpBuffer, _envFilterBuffer, _envFreeBuffer  [maxBufferSize]
_lfo1Buffer, _lfo2Buffer, _lfo3Buffer            [maxBufferSize]
```

Total por voz: ~10 × 2048 × 8 bytes ≈ 160KB. Com 16 vozes: ~2.5MB. Aceitável.

---

> **Próximo**: `07-UI-Controls.md`.

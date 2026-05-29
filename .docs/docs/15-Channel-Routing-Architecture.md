# 15 — Canal por Oscilador: Routing N-para-N e Channel Strips

## Visão Geral

Este documento consolida a pesquisa sobre routing flexível entre osciladores e
processamento independente por canal (envelope, filtro, LFO por oscilador), e
define a arquitetura-alvo para o OttoSynth.

---

## 1. Como os sintetizadores de referência resolvem esse problema

### 1.1 Serum 2 (Xfer Records) — Routing por Send

**Filosofia:** cada fonte de áudio tem knobs de send individuais para cada destino.
Não há um "patch bay" explícito; o usuário controla _quanto_ de cada fonte vai para cada destino.

**Fontes:** OSC A, OSC B, OSC C, SUB, NOISE — cada uma totalmente independente.

**Destinos disponíveis por fonte:**
| Destino | Descrição |
|---|---|
| Filter 1 | Entra no filtro 1 (pode ser LP/HP/BP/etc.) |
| Filter 2 | Entra no filtro 2 |
| BUS 1 / BUS 2 | Chains de efeito paralelas (Serum 2) |
| MAIN | Vai direto para a chain de efeitos globais |
| NONE | Não entra no áudio (uso como fonte FM/AM apenas) |

**Routing entre filtros:** cada filtro tem botões A/B/S/N indicando quais fontes ele recebe.
Modo série (FIL1 → FIL2) ou paralelo (ambos recebem independentemente).

**Implicação para OttoSynth:** o modelo de "send amount por destino" é mais simples de implementar que um patch bay visual, e cobre 90% dos casos de uso. Cada oscilador teria knobs:
- Send → Filter 1 (0-100%)
- Send → Filter 2 (0-100%)
- Send → Master (0-100%, bypass de filtros)

---

### 1.2 Moog Modular / Semi-Modular — Patch Bay com Normalling

**Filosofia:** toda conexão é explícita (cabo). Conexões _normalled_ (internas/padrão)
existem quando nenhum cabo está plugado; inserir um cabo as quebra.

**Conceitos-chave:**
- **Normal**: sinal padrão que flui sem cabo. Ex: VCO → VCF é o normal no Mother-32.
- **Break**: inserir um cabo naquele jack interrompe o normal e redireciona.
- **CV Bus**: no Moog Modular original, 3 buses CV internos (CV1/CV2/CV3) distribuem o sinal de teclado para todos os módulos sem cabos físicos.
- **Padrão de tensão:** áudio e CV compartilham o mesmo padrão físico (3.5mm TS) — qualquer sinal pode modular qualquer sinal.

**Implicação para OttoSynth:** o conceito de _normalling_ mapeia para "conexões padrão com override opcional". Sem configuração, cada OSC segue um caminho padrão (OSC → Filter → Amp → Out). O usuário pode "inserir um cabo" roteando de forma diferente.

---

### 1.3 Phase Plant (Kilohearts) — Lane Architecture

**Filosofia:** generators agrupados alimentam _effect lanes_ independentes.
É o modelo mais próximo do que o usuário pediu ("cada oscilador como um canal").

**Estrutura:**
```
[Generator Group 1]  →  Effect Lane 1  →  Master
[Generator Group 2]  →  Effect Lane 2  →  Master
[Generator Group 3]  →  Effect Lane 3  →  Master
```

**Características das lanes:**
- Cada lane é uma chain serial de Snapin effects (EQ, filtros, distortion, etc.)
- Cada lane tem toggle **Poly** (se poly = on, a lane processa por voz antes de misturar)
- Lanes podem se alimentar: Lane 1 → Lane 2 → Lane 3, ou qualquer combinação
- Cada generator group rota para uma lane específica via selector

**Moduladores por grupo:** cada group tem seus próprios envelopes e LFOs locais que só afetam aquele group. Moduladores globais também existem.

**Implicação para OttoSynth:** este é o modelo-alvo. "OSC 1 é o Canal 1" com sua própria chain (envelope, filtro, LFO) mapeando diretamente para "Generator Group → Lane".

---

### 1.4 Routing N-para-N entre Osciladores

**Problema atual no OttoSynth:** routing fixo em pares sequenciais (1→2, 2→3).
O usuário quer poder rotear OSC1→OSC3, ou OSC1+OSC2→OSC3, etc.

**Solução por Matriz de Routing:**

```
          CARRIER (quem é modulado)
MODULATOR  OSC1   OSC2   OSC3
  OSC1      —     FM/RM  FM/RM
  OSC2    FM/RM    —     FM/RM
  OSC3    FM/RM  FM/RM    —
```

Cada célula `[modulator][carrier]` pode ser: `Desligado | FM | RingMod` + depth.

**Restrição**: não pode haver ciclos (OSC1 modula OSC2 que modula OSC1).
Para detectar ciclos: **Kahn's Algorithm** (topological sort) sobre as células ativas.
A ordem de processamento resultante do sort determina qual OSC gera primeiro.

---

## 2. Implementação Interna: Grafo de Áudio com Sort Topológico

### 2.1 Estrutura de Dados

Para processamento N-para-N sem ciclos:

```csharp
// Matriz de routing: [modulatorIdx, carrierIdx] → modo + profundidade
struct OscRouteEntry {
    OscRouting Mode;     // Mix / FM / RingMod
    double Depth;        // 0..1
}

OscRouteEntry[3, 3] RoutingMatrix; // 3×3 para 3 OSCs
```

### 2.2 Topological Sort (Kahn's Algorithm)

Aplicado quando a matriz muda (não no audio callback):

```
1. Construir grafo direcionado: edge[A→B] se B é FM/RM modulado por A
2. Calcular in-degree de cada nó
3. Enfileirar todos com in-degree=0
4. Processar fila → ordem de avaliação
5. Se sobrar nós não processados → ciclo detectado → rejeitar a mudança
```

### 2.3 Feedback (auto-oscilação FM)

Um oscilador modulando a si mesmo cria um ciclo. Solução: **unit delay** de 1 sample —
o oscilador FM-modula com o valor do buffer do sample anterior. Isso introduz 1 sample de
latência no feedback, tornando o grafo acíclico por definição.

---

## 3. Arquitetura Proposta: OscChannel Strip

Cada oscilador vira um "canal" com sua própria chain de processamento:

```
┌─────────────────────────────────────────────────────────────────────┐
│  OSC CHANNEL  (×3 por voz)                                          │
│                                                                     │
│  [OSC Source]──►[Amp Envelope]──►[Channel Filter]──►[Panner/Level] │
│       │              │                   │                │         │
│   (Unison)       (per-channel       (per-channel     sends to:      │
│   (Warp)          ADSR, own         SVFilter or     • Master Bus    │
│   (Wavetable)     retrigger)        Moog Ladder)    • FX Bus 1      │
│                                                     • FX Bus 2      │
│  Routing IN: recebe FM/RM de outros canais                          │
│  Routing OUT: envia FM/RM para outros canais                        │
└─────────────────────────────────────────────────────────────────────┘
         │                    │                    │
         ▼                    ▼                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│  MASTER BUS                                                         │
│  [Sum Channels]──►[Global Filter (optional)]──►[FX Chain]──►[Out]  │
└─────────────────────────────────────────────────────────────────────┘
```

### 3.1 Modelo Incremental — O que muda vs. o que fica

| Componente | Hoje | Com Channel Strips |
|---|---|---|
| Amp Envelope | 1 global (ENV1) | 3 por voz (Env1A, Env2A, Env3A) |
| Filter | 2 globais (F1, F2) | 3 filtros de canal + 2 globais (opcionais) |
| LFO | 3 globais | 3 globais (endereçam por canal via mod matrix) |
| OSC routing | Sequencial 1→2→3 | Matriz N×N com topological sort |
| Send Matrix | N/A | Per-channel send levels (→Master, →FX1, →FX2) |
| Mod Matrix targets | Parâmetros globais | Adicionar: Channel1.Cutoff, Channel2.Level, etc. |

---

## 4. Fases de Implementação Recomendadas

### Fase 1 — Matriz N×N de Routing entre OSCs (menor esforço, alto impacto)
- Substituir os dois pares fixos (1→2, 2→3) por `OscRouteEntry[3,3]`
- Implementar topological sort para ordenar processamento
- Atualizar `SynthVoice.Process()` para seguir a ordem calculada
- UI: tabela 3×3 compacta (radio buttons ou combo por célula)

### Fase 2 — Envelope por Canal (médio esforço, alto impacto musical)
- Adicionar `AdsrEnvelope ChampEnv` por `OscChannel` (além do ENV1 global)
- `ChampEnv` controla apenas o nível daquele canal
- ENV1 global pode ser removido ou mantido como "master amp"
- UI: 3 mini-ADSR nos channel strips

### Fase 3 — Filtro por Canal (médio esforço)
- Cada canal tem um `StateVariableFilter` próprio (mais `FilterR` para stereo)
- O canal escolhe: usar o filtro próprio, o filtro global 1, o filtro global 2, ou bypass
- UI: mini filtro no channel strip (modo + cutoff + res)

### Fase 4 — Send Matrix para buses paralelos (alto esforço)
- Adicionar FX Bus 1 e FX Bus 2 ao `SynthEngine`
- Cada canal tem sends (0-100%) para: Master, FX Bus 1, FX Bus 2
- UI: 3 knobs de send por canal

---

## 5. Referências

- [Serum 2 Oscillator Routing](https://xferrecords.com/web-manual/serum-2/routing-an-oscillator-or-filter) — modelo de send por destino
- [Phase Plant Documentation](https://kilohearts.com/docs/phase_plant) — lanes e generator groups
- [Moog Modular Routing](https://amsynths.co.uk/2021/12/20/moog-modular-routing/) — normalled connections
- [Mother-32 User Manual](https://cdn.inmusicbrands.com/Moog/Mother32/Mother_32_Users_Manual.pdf) — semi-modular patch bay
- [VCV Rack Engine Internals](https://community.vcvrack.com/t/how-the-internal-engine-works/6091) — work-queue model
- [Bitwig Grid Signal Types](https://www.bitwig.com/userguide/latest/on_grid_signals/) — typed signal graph
- [Audio Graph Topological Sort](https://scsynth.org/t/question-on-graph-topological-sort/9505) — scsynth approach
- Doc `14-Moog-Research.md` — pesquisa detalhada do circuito Moog

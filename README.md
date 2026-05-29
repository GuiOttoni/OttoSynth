# OttoSynth

**OttoSynth** é um sintetizador wavetable polifônico desenvolvido em C# / .NET 10. Funciona como **plugin VST3** (Ableton Live, FL Studio, Reaper, etc.) e como **aplicativo standalone WPF**, compartilhando um único motor DSP.

A interface é inspirada no Vital e no Serum — estética Matrix/cyberpunk escura, knobs customizados, displays de waveform e espectro.

---

## Funcionalidades

### Motor de Síntese
- **3 osciladores wavetable** — Sine, Saw, Square, Triangle com anti-aliasing por mipmap
- **Wave warping estilo Serum** — Bend, Squeeze, Mirror, Fold, Sync, Phase, Formant e outros
- **Oscilador de ruído branco**
- **Portamento / glide** — modos Off, Always e Legato com interpolação logarítmica
- **16 vozes polifônicas** com voice stealing inteligente

### Filtros (11 modos)
| Modo | Algoritmo |
|---|---|
| LP 12dB, HP 12dB, BP 12dB | Simper TPT SVF (incondicionalmente estável) |
| Notch, AllPass, Peak | Simper TPT SVF |
| Moog 24dB | Modelo ladder de Huovilainen |
| K35 LP, K35 HP | 2 polos inspirado no Korg MS-20 com feedback tanh |
| Comb +, Comb − | IIR ressonante / FIR comb com notches |

Dois filtros com modos de roteamento Serial, Paralelo e Split.

### Modulação
- **3 envelopes ADSR** — Amp, Filtro, Livre (com curvas customizadas de ataque/decay/release)
- **3 LFOs** — Sine, Triangle, Saw Up/Down, Square, Sample & Hold; retriggerable
- **Matriz de modulação** — 32 rotas, 18+ sources (ENV1/2/3, LFO1/2/3, Velocity, Pitch Bend, Mod Wheel, Aftertouch, Macros, etc.), ~25 destinations
- **4 knobs macro** assignáveis
- Suporte a **pedal de sustain** (CC#64), implementação zero-allocation

### Arpeggiador

Converte notas sustentadas em padrões rítmicos em tempo real. Ativado/desativado por toggle na aba **// ARP / SEQ**.

| Parâmetro | Opções |
|---|---|
| Pattern | Up, Down, UpDown, Random, AsPlayed |
| Rate | 1/1, 1/2, 1/4, 1/8, 1/16, 1/32 |
| Octaves | 1 – 4 oitavas |
| Hold | Latch das notas após soltar as teclas |

Quando ativo, intercepta os NoteOn/NoteOff do teclado e MIDI; as notas são geradas diretamente no audio thread (zero-latência).

---

### Step Sequencer

Sequenciador de 32 passos com grid visual. Cada passo possui nota, velocity e flag de tie (legato). Ativado/desativado por toggle.

| Parâmetro | Descrição |
|---|---|
| BPM | 20 – 300 BPM (compartilhado com o arpeggiador) |
| Steps | 4, 8, 12, 16, 24 ou 32 passos |
| Rate | 1/1 a 1/32 por passo |
| Step toggle | Click: ativa/desativa o passo |
| Nota | Scroll do mouse: ±1 semitom |
| Velocity | Shift + Scroll: ±10 |

---

### Rack de Efeitos
11 efeitos em série com controles de Bypass, Mix e roteamento de canal (L / R / Stereo):

| Efeito | Parâmetros |
|---|---|
| Distortion | Drive, Output Gain, Bit Depth; modos: Soft, Hard, Tube, Fold, Bit |
| Delay | Tempo L/R, Feedback, Damping, Ping-Pong |
| Reverb | Size, Decay, Damping, Pre-delay, Width |
| Chorus | Rate, Depth, Feedback |
| Phaser | Rate, Depth, Feedback, Stages |
| Flanger | Rate, Depth, Feedback |
| EQ 3 Bandas | Freq + gain Low/Mid/High, Mid Q |
| Compressor | Threshold, Ratio, Attack, Release, Knee, Makeup Gain |
| Tremolo | Rate, Depth, Mix; modo stereo (auto-pan) |
| BitCrusher | Bit Depth (1–16 bits), Sample Rate Divisor (decimação) |
| StereoWidener | Width (0 = mono, 1 = original, 2 = extra-wide via M-S encoding) |

### Sistema de Presets
- **50 presets de fábrica** em categorias (Leads, Pads, Basses, Keys, etc.)
- **Presets de usuário** salvos como arquivos `.otto` (JSON) com Save/Export/Import
- Compatível com arquivos legados `.ottopreset`
- O preset captura o estado completo: osciladores, filtros, envelopes, LFOs, matriz de modulação, efeitos e glide

---

## Arquitetura

```
OttoSynth/
├── src/
│   ├── OttoSynth.Core        # Motor DSP — sem dependências externas (apenas BCL)
│   ├── OttoSynth.UI          # Controles WPF customizados (SynthKnob, WaveformDisplay, etc.)
│   ├── OttoSynth.Standalone  # App WPF + NAudio (áudio/MIDI)
│   └── OttoSynth.Plugin      # VST3 via AudioPlugSharp
└── tests/
    └── OttoSynth.Core.Tests  # 72 testes xUnit
```

**Princípios de design:**
- **Audio thread zero-allocation** — todos os buffers pré-alocados; sem pressão no GC durante `ProcessAudio`
- **Separação UI ↔ Áudio** — escritas de parâmetros são campos `double` atômicos; sem eventos disparados pelo audio thread
- **DSP interno em `double`** — convertido para `float` apenas na fronteira NAudio/VST3
- **Componentes plugáveis** — cada unidade DSP (`WavetableOscillator`, `StateVariableFilter`, `AdsrEnvelope`, `LfoGenerator`) é testável de forma independente

---

## Versão

`v1.0.0-beta.2` — veja as [releases](https://github.com/GuiOttoni/OttoSynth/releases) para o instalador compilado.

---

## Requisitos do usuário (standalone .exe)

- **OS:** Windows 10/11 (64-bit)
- **.NET Runtime:** **Não necessário** — o executável é self-contained
- **Áudio:** Qualquer dispositivo WDM (Windows Driver Model)
- **MIDI:** Opcional — qualquer dispositivo MIDI WDM

## Requisitos para compilar (desenvolvedores)

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows 10/11 x64

---

## Build e Execução

```powershell
# Restaurar pacotes
dotnet restore OttoSynth.slnx

# Rodar o app standalone (modo dev)
dotnet run --project src/OttoSynth.Standalone -c Release

# Rodar os testes
dotnet test tests/OttoSynth.Core.Tests

# Build completo em Release
dotnet build OttoSynth.slnx -c Release

# ── PUBLICAR como executável self-contained (para distribuição) ──────
dotnet publish src/OttoSynth.Standalone/OttoSynth.Standalone.csproj `
    -p:PublishProfile=win-x64 -c Release
# Saída: src/OttoSynth.Standalone/publish/win-x64/OttoSynth.exe
```

### Plugin VST3 (manual)

1. Build do `OttoSynth.Plugin` em Release
2. Crie a estrutura de bundle exigida pelo Steinberg spec:
   ```
   C:\Program Files\Common Files\VST3\OttoSynth.vst3\Contents\x86_64-win\
   ```
3. Copie `AudioPlugSharpVst3.dll` para esse diretório com o nome `OttoSynth.vst3`
4. Copie todos os demais `*.dll` e `*.json` do mesmo diretório de build para o mesmo destino

> **Dica:** o instalador gerado pelo CI já cuida de todos esses passos. Use-o para instalações em produção.

### Gerar o Instalador (Inno Setup)

```powershell
# 1. Publicar o standalone
dotnet publish src/OttoSynth.Standalone/OttoSynth.Standalone.csproj `
    -c Release -r win-x64 --self-contained -p:PublishSingleFile=true `
    -p:PublishReadyToRun=true --output artifacts/standalone

# 2. Buildar o plugin VST3
dotnet build src/OttoSynth.Plugin/OttoSynth.Plugin.csproj -c Release

# 3. Compilar o instalador (requer Inno Setup 6 instalado)
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\OttoSynth.iss
# Saída: installer\Output\OttoSynth-1.0.0-beta.2-Setup.exe
```

O instalador permite escolher entre:
- **Standalone** — instala em `C:\Program Files\OttoSynth\`, cria atalho no Menu Iniciar
- **VST3** — instala como bundle `C:\Program Files\Common Files\VST3\OttoSynth.vst3\`

---

## Versionamento

O projeto segue **Semantic Versioning 2.0** (`MAJOR.MINOR.PATCH[-label.N]`).  
A versão canônica é o arquivo `version.txt` na raiz do repositório.

### Conventional Commits → bump automático

O CI analisa as mensagens de commit desde a última tag e determina o tipo de bump automaticamente:

| Prefixo do commit | Em pre-release | Em versão estável |
|---|---|---|
| `feat:`, `fix:`, `perf:`, `refactor:` | → `prerelease` (ex: `beta.2 → beta.3`) | — |
| `fix:`, `perf:`, `refactor:` | — | → `patch` (ex: `1.0.0 → 1.0.1`) |
| `feat:` | — | → `minor` (ex: `1.0.0 → 1.1.0`) |
| `feat!:` ou `BREAKING CHANGE` | → `prerelease` | → `major` (ex: `1.0.0 → 2.0.0`) |
| `chore:`, `docs:`, `ci:`, `style:`, `test:` | sem bump | sem bump |

> Commits sem bump não geram release nem tag — apenas build e testes.

### Exemplos de mensagens válidas

```
feat: add unison detune parameter
fix: prevent voice stealing during sustain hold
perf: reduce allocations in ProcessAudio hot path
refactor: extract filter coefficient calc
chore: update CI runner version   ← não gera release
```

### Bump manual via GitHub Actions

Acesse **Actions → CI / Release → Run workflow** e selecione:

| Input `bump` | Efeito |
|---|---|
| `prerelease` | `1.0.0 → 1.0.0-beta.1` ou `1.0.0-beta.1 → 1.0.0-beta.2` |
| `patch` | `1.0.0 → 1.0.1` |
| `minor` | `1.0.0 → 1.1.0` |
| `major` | `1.0.0 → 2.0.0` |
| `release` | `1.0.0-beta.2 → 1.0.0` (promove para estável) |

O campo `label` controla o sufixo do pre-release (padrão: `beta`; opções: `rc`, `alpha`).

### Bump manual local

```powershell
./scripts/Bump-Version.ps1 prerelease          # beta.N → beta.N+1
./scripts/Bump-Version.ps1 prerelease -Label rc # → rc.1
./scripts/Bump-Version.ps1 release             # remove sufixo
./scripts/Bump-Version.ps1 patch               # 1.0.0 → 1.0.1
```

---

## CI / CD (GitHub Actions)

O workflow `.github/workflows/ci-release.yml` executa em todo push ou PR para `main`:

1. Analisa os commits desde a última tag e determina o bump (ver seção **Versionamento** acima)
2. Atualiza `version.txt` e faz push do commit `chore: release X.Y.Z [skip ci]`
3. Restaura pacotes e roda os testes
4. Publica o standalone (single-file self-contained)
5. Builda o plugin VST3
6. Compila o instalador Inno Setup *(somente se houver bump)*
7. Faz upload do `.exe` como artefato do build *(somente se houver bump)*
8. Cria uma GitHub Release automática com o instalador anexado *(somente em push para `main` com bump)*

PRs para `main` executam somente os passos de build e teste — sem bump, sem release.

---

## Arquivos de Preset (.otto)

Os presets de usuário ficam em `%APPDATA%\OttoSynth\Presets\` como arquivos `.otto` (JSON padrão).

- **Save** — captura o estado atual do motor em um arquivo `.otto` nomeado
- **Import** — carrega qualquer arquivo `.otto` ou `.ottopreset` do disco
- **Export** — salva em um caminho customizado para compartilhamento

---

## Status do Projeto

| Fase | Status | Descrição |
|---|---|---|
| Fase 1 — Fundação DSP | ✅ Completa | Motor básico, polifonia, MIDI |
| Fase 2 — Síntese Completa | ✅ Completa | 3 OSCs + warp estilo Serum, 2 filtros, 3 envelopes, 3 LFOs |
| Fase 3 — Matriz de Modulação | ✅ Completa | 32 rotas, 18+ sources, ~25 destinations |
| Fase 4 — UI WPF | ✅ Completa | Controles customizados + layout Matrix/cyberpunk |
| Fase 5 — Plugin VST3 | ✅ Completa | Entry point + mapeamento de parâmetros |
| Fase 6 — Rack de Efeitos | ✅ Completa | 11 efeitos (Reverb, Delay, Chorus, Phaser, Flanger, Dist, EQ, Comp, Tremolo, BitCrusher, Widener) |
| Fase 7 — Sistema de Presets | ✅ Completa | JSON + 70+ presets de fábrica por categoria + formato .otto |
| Fase 8 — Polimento | ✅ Completa | Moog Ladder, DC Blocker, testes adicionais |
| Fase 9 — Filtros & UX | ✅ Completa | 11 modos de filtro, input manual de knob, portamento, sustain |
| Fase 10 — Rack de Efeitos | ✅ Completa | 11 efeitos, canal L/R/ST por efeito, rack redimensionável |
| Fase 11 — Arpeggiador & Sequencer | ✅ Completa | Arp (5 padrões, 4 oitavas, hold) + Sequencer 32 passos com playhead |
| Fase 12 — UI Completa | 🔄 Planejada | Editor de efeitos, Filter 2 UI, editor de Mod Matrix, Unison |
| Fase 13 — MIDI & Playability | 🔄 Planejada | MIDI Learn, Expression, curvas de velocity |
| Fase 14 — Expansão de Síntese | 🔄 Planejada | LFO sync, filtro Formant, biblioteca de wavetables |

---

## Stack de Tecnologia

| Camada | Tecnologia |
|---|---|
| Linguagem | C# 13 / .NET 10 |
| UI | WPF (Windows Presentation Foundation) |
| Saída de áudio | NAudio (WaveOut, ~50ms de latência) |
| Entrada MIDI | NAudio.Midi |
| Host VST3 | AudioPlugSharp |
| Testes | xUnit |
| Documentação | Docusaurus 3 |

---

## Licença

Projeto privado — todos os direitos reservados.

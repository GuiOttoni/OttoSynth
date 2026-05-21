---
sidebar_position: 10
title: Como Manter
---

# OttoSynth — Guia de Manutenção

:::info
O que mudar, onde mudar, e como evitar quebrar o sintetizador.
:::

## 1. Setup do ambiente

### Requisitos
- **Windows 10/11** (64-bit)
- **.NET 8 SDK**
- **Visual Studio 2022** (recomendado) ou **VS Code** com extensão C# Dev Kit
- (Opcional, para empacotamento VST3) **VST3 SDK** da Steinberg

### Primeiro build
```powershell
cd F:\Projetos\OttoSynth
dotnet restore OttoSynth.slnx
dotnet build OttoSynth.slnx -c Debug
dotnet test tests/OttoSynth.Core.Tests
dotnet run --project src/OttoSynth.Standalone
```

Tudo deve compilar **sem erros, sem warnings críticos**, e os 72 testes devem passar.

---

## 2. Workflow de mudança recomendado

### 2.1 Mudança em DSP (Core)
1. Abra o componente afetado em `src/OttoSynth.Core/DSP/.../X.cs`.
2. Modifique.
3. Escreva ou atualize testes em `tests/OttoSynth.Core.Tests/DSP/`.
4. Rode `dotnet test`.
5. Rode o `Standalone` e teste auralmente.

### 2.2 Mudança em UI
1. Edite `src/OttoSynth.UI/Controls/X.cs` (controle) ou `src/OttoSynth.Standalone/MainWindow.xaml` (layout).
2. F5 (rodar o Standalone do Visual Studio).
3. Visual testing.

### 2.3 Mudança em VST3 plugin
1. Edite `src/OttoSynth.Plugin/OttoSynthPlugin.cs`.
2. `dotnet build src/OttoSynth.Plugin -c Release`.
3. Copie DLLs para o diretório VST3.
4. Carregue na DAW e teste.

---

## 3. Performance — regras de ouro

### **Regra 1**: zero alocação no audio thread
**Audio thread** = qualquer código chamado direta ou indiretamente por:
- `SynthEngine.ProcessAudio` (Standalone)
- `OttoSynthPlugin.Process` (VST3)
- `SynthVoice.Process`
- `EffectsChain.Process` e cada `IEffect.Process`

**Não faça**:
- `new T()` (qualquer alocação no heap)
- `array.Add(x)` em `List<T>` sem capacidade reservada
- `string s = a + b` (concatenação)
- `someEnumerable.Where(...)` (LINQ produz iteradores no heap)
- `Math.Sin` se for chamado milhões de vezes (use `MathUtils.FastSin`)

**Faça**:
- Use buffers pré-alocados (campos privados inicializados no construtor).
- Use `Span<T>` para passar slices sem copy.
- Use `[MethodImpl(MethodImplOptions.AggressiveInlining)]` em hot helpers.
- Use `stackalloc` para small temp buffers.

### **Regra 2**: float vs double
- Internamente, **double** (64-bit). Acumula menos erro em filtros e cadeias longas.
- Na fronteira VST3/NAudio: **float** (32-bit), conversão única.
- Não converta de double→float→double no meio do processo (precisão perdida).

### **Regra 3**: medindo
- Use `BenchmarkDotNet` para benchmarks rigorosos.
- Visual Studio Profiler para perfil de CPU.
- O target é < 25% CPU com 16 vozes + fx chain em 256@44100.

---

## 4. Debug do audio engine

### Sem som? Diagnóstico
1. Verificar `engine.VoiceManager.ActiveVoiceCount > 0` após `NoteOn`.
2. Verificar `voice.EnvAmp.CurrentValue > 0`.
3. Verificar que o filtro não está fechado: `voice.Filter1.Cutoff > 100`.
4. Verificar que master volume > 0.
5. Verificar que oscilador está ativo: `voice.Osc1Enabled && voice.Osc1Level > 0`.

### Click / pop em transições
- Causa típica: voice stealing sem fadeout.
- Verificar que `ForceSteal` está sendo chamado e ForceRelease executa.
- Aumentar fadeout (3ms → 5ms) em `SynthVoice.ForceSteal`.

### Distorção / clipping inesperado
- Verificar `SoftClip` no final de `ProcessAudio`.
- Verificar `MasterVolume` está sendo aplicado corretamente.
- Verificar amount de modulação não está somando para fora dos limites.

### Output com offset DC
- Verificar que `DcBlocker` está ativo (deveria estar — é integrado no SynthEngine).
- Se DCblocker ativo mas ainda há offset: bug em algum efeito não-linear.

### Voz "travada"
- Estado nunca volta para Idle: envelope amp não termina release.
- Verificar `_envAmp.SustainLevel < 1.0` (sustain==1 + release==infinite trava).
- Verificar `ReleaseTime > 0`.

---

## 5. Adicionando funcionalidade — recipes

### Adicionar uma nova wavetable
1. `BasicWavetables.cs`: método `public static double[][] GenerateMine(...)`.
2. `SynthEngine.LoadDefaultWavetables`: `_wavetables["Mine"] = BasicWavetables.GenerateMine();`.
3. `MainWindow.xaml.cs` PopulateCombos: adicione `"Mine"` aos 3 wavetable combos.

### Adicionar um novo filtro tipo
1. Crie `DSP/Filters/MyFilter.cs`. Conforme interface implícita (`Process`, `SetSampleRate`, `Reset`).
2. `SynthVoice`: substitua ou complemente `StateVariableFilter` por `MyFilter`.
3. Atualize `SynthVoice.SetFilter*` para configurar o novo filtro.

### Adicionar um novo efeito
Ver `05-Effects-Rack.md` section 11.

### Adicionar um novo source de modulação
Ver `04-Modulation-System.md` section 3.

### Adicionar um novo destination de modulação
Ver `04-Modulation-System.md` section 4.

### Adicionar um parâmetro VST3 expostos para automação
1. `OttoSynthPlugin.Initialize`: `AddParam(NewId, "Name", min, max, default)`.
2. `OttoSynthPlugin.ApplyParameter`: `case NewId: ... break;`.
3. Adicione `public const int NewId = ...;` em `OttoParameterId`.

### Adicionar um novo controle WPF
Ver `07-UI-Controls.md` section 10.

---

## 6. Testando alterações

### Tests automáticos (existem 72)
```powershell
dotnet test tests/OttoSynth.Core.Tests
```

Categorias:
- `WavetableOscillatorTests` (5)
- `AdsrEnvelopeTests` (5)
- `StateVariableFilterTests` (5)
- `NoiseOscillatorTests` (3)
- `LfoGeneratorTests` (4)
- `MoogLadderTests` (3)
- `VoiceManagerTests` (3)
- `ModMatrixTests` (12)
- `EffectsTests` (9)
- `PresetManagerTests` (5)

### Tests auditivos
Sempre teste auralmente, especialmente:
- **Sustain notes** (várias notas seguradas — verificar polifonia)
- **Rapid retriggers** (envelope rápido — verificar clicks)
- **Extreme parameter values** (resonance 0.99, cutoff 20Hz, etc.)
- **Fast modulation** (LFO 30Hz num parâmetro audível)
- **All effects engaged** (CPU peak, distortion accumulation)

### Buffer size sweep
DAWs usam vários tamanhos. Teste com **64, 128, 256, 512, 1024** samples.

### Sample rate sweep
Teste com **44100, 48000, 88200, 96000**. Em 96k, alguns reverbs ficam audivelmente diferentes (use `_sampleRate` em coeficientes).

---

## 7. Versionamento

### Bumping
- **Patch (1.0.x)**: fix bug, sem mudar API ou preset format.
- **Minor (1.x.0)**: features novas, presets antigos ainda carregam.
- **Major (x.0.0)**: API ou preset format quebra. Adicione migração.

Onde mudar:
- `OttoSynth.Plugin/OttoSynthPlugin.cs`: `PluginVersion = "1.0.0"`.
- `PresetData.Version` (inicialização default).
- AssemblyVersion em `.csproj` se necessário.

---

## 8. Pitfalls comuns

| Sintoma | Causa provável | Fix |
|---|---|---|
| Voz fica tocando depois de NoteOff | `Sustain == 1.0 && Release == 0` | Garantir `Release > 0.001` |
| Click ao mudar wavetable | Reset incorreto da fase | `osc.ResetPhase()` em SetWavetable se necessário |
| Filter "engasga" em modulation rápida | Cutoff variando muito rápido | Suavize com one-pole filter no setter |
| Reverb com aliasing | FFT size pequeno | Trocar pra 1024 ou implementar oversampling |
| GC pause audível | Alocação no audio thread | `dotnet-gctrace` ou `Profiler` para encontrar |
| Mod matrix sem efeito | Source ou destination = None | Verificar enum valores e `IsValid` |

---

## 9. Estado nullable / warnings

Todos os projetos têm `<Nullable>enable</Nullable>`. O Core compila sem warnings; remaining warnings em UI/Plugin são tratáveis caso a caso.

Verifique antes de commit:
```powershell
dotnet build OttoSynth.slnx -c Release -warnaserror
```

---

## 10. Conventions e style

- **Indent**: 4 espaços (não tabs).
- **Braces**: K&R-ish (`{` na próxima linha em métodos, mesma linha em controle de fluxo).
- **Documentation**: XML doc comments em **inglês** em todo membro `public`.
- **File-scoped namespaces** (`namespace X.Y;` sem chaves).
- **using directives** sorted: System.* primeiro, depois alfabeticamente.

---

## 11. Roadmap de melhorias possíveis

| Feature | Esforço | Onde mexer |
|---|---|---|
| Unison engine integrado ao SynthVoice | ~4h | `SynthVoice`, `UnisonEngine` |
| GUI WPF embedded no VST3 | ~8h | `OttoSynthPlugin`, `AudioPlugSharpWPF` |
| Oversampling 2× na distortion | ~3h | `Distortion.cs`, FIR filter design |
| Wavetable editor | ~12h | Novo controle WPF + UI panel |
| MIDI sync para LFOs | ~4h | `LfoGenerator` (tempo do host) |
| Drag-and-drop ModMatrix | ~6h | `ModMatrixGrid` |
| Spectrum tilt / log scale fix | ~1h | `SpectrumAnalyzer.OnRender` |
| Compressor sidechain input | ~3h | `Compressor.cs` + UI |
| Tema light | ~2h | `Themes/LightTheme.xaml` |
| Cross-platform (Mac/Linux) | ~weeks | Migrar UI WPF→Avalonia, plugin→NPlug |

---

## 12. Recursos externos

- **VST3 SDK**: https://steinbergmedia.github.io/vst3_doc/
- **AudioPlugSharp**: https://github.com/mikeoliphant/AudioPlugSharp
- **Vital (open-source)**: https://github.com/mtytel/vital (referência de DSP)
- **DAFX book**: "Digital Audio Effects" by Udo Zölzer
- **RBJ EQ Cookbook**: https://www.w3.org/TR/audio-eq-cookbook/
- **Vadim Zavalishin's "The Art of VA Filter Design"**: PDF gratuito online

---

> **Próximo**: `11-Arquivos-Por-Pasta.md` para lista exhaustiva de arquivos.

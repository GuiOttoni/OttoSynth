---
sidebar_position: 9
title: VST3 Plugin
---

# OttoSynth — VST3 Plugin

:::info
Como o wrapper VST3 (`OttoSynth.Plugin`) funciona e como empacotar/instalar.
:::

## 1. Stack

```
DAW (Ableton, FL Studio, Reaper, ...)
   │
   ▼
[OttoSynth.PluginBridge.vst3]   ← bridge nativo (C++/CLI) do AudioPlugSharp
   │
   ▼
[OttoSynth.Plugin.dll]          ← nossa classe OttoSynthPlugin : AudioPluginBase
   │
   ▼
[OttoSynth.Core.dll]            ← motor DSP
```

O AudioPlugSharp fornece:
- `AudioPlugSharpVst.vst3` — bridge nativo C++ implementando a interface VST3 SDK
- `AudioPlugSharp.dll` — base classes managed (`AudioPluginBase`, `AudioIOPort*`, etc.)

Nossa DLL `OttoSynth.Plugin` herda de `AudioPluginBase` e é carregada pelo bridge.

---

## 2. Class principal — OttoSynthPlugin

`src/OttoSynth.Plugin/OttoSynthPlugin.cs`

```csharp
public class OttoSynthPlugin : AudioPluginBase {
    private readonly SynthEngine _engine;
    
    public OttoSynthPlugin() {
        _engine = new SynthEngine(maxVoices: 16, maxBufferSize: 2048);
        Company = "OttoSynth";
        PluginName = "OttoSynth";
        PluginCategory = "Instrument|Synth";
        PluginVersion = "1.0.0";
        PluginID = 0x0A7C8ED1C2464B7AUL;
    }
    
    public override void Initialize() {
        base.Initialize();
        // I/O ports
        OutputPorts = new AudioIOPort[] {
            new AudioIOPortManaged("Stereo Output", EAudioChannelConfiguration.Stereo)
        };
        // Parameters
        AddParam(OttoParameterId.MasterVolume, "Master Volume", 0, 1, 0.8);
        AddParam(OttoParameterId.FilterCutoff, "Filter Cutoff", 20, 20000, 20000, rangePower: 4.0);
        // ... ~16 parâmetros total
    }
    
    public override void InitializeProcessing() {
        base.InitializeProcessing();
        _engine.Initialize(Host.SampleRate, (int)Host.MaxAudioBufferSize);
    }
    
    public override void HandleNoteOn(int channel, int noteNumber, float velocity, int sampleOffset) {
        _engine.ProcessMidiEvent(MidiEvent.NoteOn((byte)noteNumber, (byte)(velocity * 127f)));
    }
    
    public override void HandleNoteOff(int channel, int noteNumber, float velocity, int sampleOffset) {
        _engine.ProcessMidiEvent(MidiEvent.NoteOff((byte)noteNumber, (byte)(velocity * 127f)));
    }
    
    public override void HandleParameterChange(AudioPluginParameter p, double v, int offset) {
        base.HandleParameterChange(p, v, offset);
        if (int.TryParse(p.ID, out int id)) ApplyParameter(id, v);
    }
    
    public override void Process() {
        base.Process();
        var output = OutputPorts[0] as AudioIOPortManaged;
        var buffers = output!.GetAudioBuffers();
        _engine.ProcessAudio(buffers[0], buffers[1], (int)Host.CurrentAudioBufferSize);
    }
}
```

---

## 3. Parameter mapping

Cada parâmetro VST3 tem:
- **ID** (string, numérico) — corresponde a `OttoParameterId.X` constante
- **Name** (display)
- **MinValue, MaxValue** — range em valor real
- **DefaultValue**
- **RangePower** — exponente para mapeamento não-linear (e.g., filter cutoff usa 4.0 → percepção log)

O VST3 host comunica valores em `[0..1]` normalizado. AudioPlugSharp já converte para o range real antes do `HandleParameterChange`. Nossa lógica em `ApplyParameter(id, value)`:

```csharp
switch (id) {
    case OttoParameterId.MasterVolume: _engine.MasterVolume = v; break;
    case OttoParameterId.FilterCutoff:
        _engine.SetFilter(1, FilterMode.LowPass, v, ...);
        break;
    // ...
}
```

### Lista atual de parâmetros (16)

| ID | Name | Range |
|---|---|---|
| 1 | Master Volume | 0..1 |
| 10 | Filter Cutoff | 20..20000 Hz (rangePower=4) |
| 11 | Filter Resonance | 0..1 |
| 12 | Filter Drive | 0..1 |
| 20 | Attack | 0.001..10s (power=3) |
| 21 | Decay | 0.001..10s |
| 22 | Sustain | 0..1 |
| 23 | Release | 0.001..10s |
| 30-33 | OSC1/2/3 Level + Noise Level | 0..1 |
| 40-43 | Macro 1-4 | 0..1 |

> Para expor mais parâmetros, adicione no `Initialize()` e no `ApplyParameter()`.

---

## 4. UI

Atualmente `HasUserInterface = false` — o plugin não expõe GUI customizada (a DAW mostra default knobs/sliders).

Para expor a UI WPF do `OttoSynth.UI`, precisaria:
1. Mudar para `true`.
2. Implementar `InitializeEditor`, `ShowEditor`, `HideEditor`, `ResizeEditor`.
3. Usar `AudioPlugSharpWPF` para criar um Window WPF anexado ao parent HWND fornecido pelo host.

Está pronto na lista de TODOs (Phase 5.2 deliverable).

---

## 5. Empacotando como VST3

Após `dotnet build src/OttoSynth.Plugin -c Release`:

### Arquivos necessários
- `OttoSynth.Plugin.dll`, `OttoSynth.Plugin.runtimeconfig.json`, `OttoSynth.Plugin.deps.json` (do output)
- `OttoSynth.Core.dll`, `OttoSynth.UI.dll`
- `AudioPlugSharp.dll`, `AudioPlugSharpWPF.dll` (do package NuGet)
- **Bridge**: `AudioPlugSharpVst.vst3` (copy do package nativo, **renomeado** para `OttoSynth.PluginBridge.vst3`)
- `wpf.runtimeconfig.json` (do package, renomeado para `OttoSynth.PluginBridge.runtimeconfig.json`)
- `Ijwhost.dll` (do package — C++/CLI bridge)

### Local de instalação (Windows)
`C:\Program Files\Common Files\VST3\OttoSynth\`

Estrutura recomendada:
```
C:\Program Files\Common Files\VST3\OttoSynth\
├── OttoSynth.PluginBridge.vst3
├── OttoSynth.PluginBridge.runtimeconfig.json
├── Ijwhost.dll
├── OttoSynth.Plugin.dll
├── OttoSynth.Core.dll
├── OttoSynth.UI.dll
├── AudioPlugSharp.dll
└── AudioPlugSharpWPF.dll
```

### Automatizando com post-build
Adicione ao `OttoSynth.Plugin.csproj`:

```xml
<Target Name="DeployVst3" AfterTargets="Build">
  <PropertyGroup>
    <Vst3OutDir>$(OutputPath)vst3\</Vst3OutDir>
  </PropertyGroup>
  <ItemGroup>
    <BridgeFiles Include="$(USERPROFILE)\.nuget\packages\audioplugsharp\0.7.9\build\AudioPlugSharpVst.vst3" />
    <BridgeFiles Include="$(USERPROFILE)\.nuget\packages\audioplugsharp\0.7.9\build\wpf.runtimeconfig.json" />
    <BridgeFiles Include="$(USERPROFILE)\.nuget\packages\audioplugsharp\0.7.9\build\Ijwhost.dll" />
  </ItemGroup>
  <Copy SourceFiles="@(BridgeFiles)" DestinationFolder="$(Vst3OutDir)" />
  <!-- Renomear bridge -->
  <Move SourceFiles="$(Vst3OutDir)\AudioPlugSharpVst.vst3" 
        DestinationFiles="$(Vst3OutDir)\OttoSynth.PluginBridge.vst3" />
  ...
</Target>
```

(Verifique caminhos exatos no package que você baixou.)

---

## 6. Validando o plugin

Steinberg fornece o `VST3PluginTestHost.exe` no SDK. Após instalar:

```
VST3PluginTestHost.exe "C:\Program Files\Common Files\VST3\OttoSynth\OttoSynth.PluginBridge.vst3"
```

Verifica:
- Carregamento sem erros
- Lista de parâmetros válida
- Processo de áudio sem leaks
- MIDI input handling

---

## 7. Limitações conhecidas

1. **AudioPlugSharp 0.7.9** é a versão disponível no nosso ambiente. Versões mais recentes podem ter API diferente.
2. **Windows only** — `OttoSynth.UI` e `OttoSynth.Plugin` usam WPF (`net10.0-windows`). Para Mac/Linux, precisaria substituir por SkiaSharp/MAUI/Avalonia.
3. **Sample-accurate parameter changes** não estão implementados — o `sampleOffset` é ignorado em `HandleParameterChange`. Para automação smooth em rampa, seria necessário interpolar internamente.
4. **Sem GUI VST3** — usuário vê knobs default da DAW (a versão futura embute a WPF UI).
5. **Sem MIDI Out** — só recebemos MIDI, não enviamos.
6. **Presets via DAW** — atualmente não há integração entre VST3 program changes e o sistema interno de presets. O save state do host só persiste valores dos parâmetros expostos.

---

## 8. Como adicionar GUI VST3

Esboço (TODO):

```csharp
// OttoSynthPlugin.cs
public override bool HasUserInterface => true;
public override uint EditorWidth => 1200;
public override uint EditorHeight => 800;

public override void InitializeEditor() {
    // Usar AudioPlugSharpWPF.WpfHost
    var window = new MainPluginWindow(_engine);
    Editor = new WpfPluginEditor(window);
}
```

Onde `MainPluginWindow` é uma versão "embeddable" da `MainWindow.xaml` do Standalone — sem `<Window>` de top-level, embutindo o root grid num `UserControl`.

---

## 9. Bypass / latência

Atualmente o plugin **não declara latência** (`AudioPluginBase.Latency = 0`). Isso é correto para um sintetizador sem oversampling.

Se você adicionar **oversampling** no path principal (Phase 8 deliverable parcial), declare a latência adicional:

```csharp
public override uint Latency => 32;  // amostras adicionadas por filters anti-alias
```

A DAW vai compensar automaticamente.

---

> **Próximo**: `10-Como-Manter.md` para guia de manutenção.

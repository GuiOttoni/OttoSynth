---
sidebar_position: 8
title: Preset System
---

# OttoSynth — Preset System

:::info
Formato `.ottopreset`, save/load, factory presets.
:::

## 1. Formato

Os presets são salvos como **JSON puro** (não comprimido) com extensão `.ottopreset`. Diretório default: `%APPDATA%\OttoSynth\Presets\<Category>\<Name>.ottopreset`.

JSON foi escolhido porque:
- Editável manualmente (debug, criação de patches programáticos)
- Versionável em Git
- Trivialmente serializável com System.Text.Json (sem dependências extras)
- Estrutura auto-descritiva

---

## 2. PresetData — estrutura

`src/OttoSynth.Core/Preset/PresetData.cs`

Classe POCO com todos os parâmetros do sintetizador:

```csharp
class PresetData {
    // Metadata
    string Name, Author, Category, Description, Version, CreatedDate;
    List<string> Tags;
    
    // Globals
    double MasterVolume, PitchBendRange;
    
    // 3 oscillators
    OscillatorData Osc1, Osc2, Osc3;
    NoiseData Noise;
    
    // 2 filters
    FilterData Filter1, Filter2;
    string FilterRouting;  // "Serial", "Parallel", "Split"
    double FilterEnvAmount;
    
    // 3 envelopes
    EnvelopeData EnvAmp, EnvFilter, EnvFree;
    
    // 3 LFOs
    LfoData Lfo1, Lfo2, Lfo3;
    
    // Modulation
    List<ModRouteData> ModRoutes;
    double[] Macros;        // 4 macro values
    
    // Effects
    List<EffectData> Effects;
}
```

Cada sub-classe (`OscillatorData`, `FilterData`, etc.) tem só os parâmetros relevantes. `EffectData` é genérico:

```csharp
class EffectData {
    string Type;     // "Reverb", "Delay", etc.
    bool Bypass;
    double Mix;
    Dictionary<string,double> Parameters;
    Dictionary<string,string> StringParameters;
}
```

Isso permite que efeitos novos sejam adicionados sem mudar PresetData — só adicionar parâmetros no dicionário.

---

## 3. PresetManager

`src/OttoSynth.Core/Preset/PresetManager.cs`

### API principal

```csharp
var mgr = new PresetManager();

// Save / Load por arquivo
mgr.Save(preset, "C:/path/preset.ottopreset");
PresetData p = mgr.Load("C:/path/preset.ottopreset");

// Capturar estado atual do engine
PresetData snapshot = mgr.Capture(engine, name: "My Patch");

// Aplicar um preset (substitui todo o estado)
mgr.Apply(preset, engine);

// Save no diretório padrão (cria pasta por categoria)
string path = mgr.SaveToDefault(preset);

// Scan diretório
List<string> files = mgr.Scan();   // ou mgr.Scan(customDir)

// Round-trip via JSON string
string json = mgr.ToJson(preset);
PresetData restored = mgr.LoadFromJson(json);
```

### Capture
Lê o estado de `engine.VoiceManager.Voices[0]` (voz template — todas as vozes têm os mesmos parâmetros). Captura:
- Wavetable, level, tune, pan dos 3 oscs
- Filter1/2 settings + routing
- 3 envelopes (com curvas)
- 3 LFOs (shape, rate, depth)
- Mod routes
- Macros
- Effects (tipo + parâmetros via reflection-like switch)

### Apply
Inverso da capture. Limpa effects chain e mod routes existentes, reconstrói tudo do preset.

> Apply é destrutivo: substitui tudo o que estava antes. Para "merge", capture o atual, modifique fields, e re-aplique.

---

## 4. EffectData genérico — pattern de uso

Cada efeito sabe como serializar/deserializar a si mesmo via os dicionários:

```csharp
// CaptureEffect (switch por tipo)
case Distortion d:
    data.Parameters["Drive"] = d.Drive;
    data.Parameters["OutputGain"] = d.OutputGain;
    data.StringParameters["Type"] = d.Type.ToString();
    break;

// CreateEffect (também switch por nome)
case "Distortion":
    var fx = new Distortion();
    if (d.Parameters.TryGetValue("Drive", out var v)) fx.Drive = v;
    ...
```

Para adicionar um efeito novo ao sistema de presets:
1. Adicione um case em `PresetManager.CaptureEffect()`.
2. Adicione um case em `PresetManager.CreateEffect()`.
3. Pronto — round-trip funciona.

---

## 5. FactoryPresets

`src/OttoSynth.Core/Preset/FactoryPresets.cs`

50+ presets fábrica em 8 categorias:

| Categoria | Quantidade | Exemplos |
|---|---|---|
| Init | 1 | Init |
| Bass | 8 | Sub Bass, Reese, Wobble, Acid, FM Bass |
| Lead | 8 | Supersaw, Trance Lead, Pluck Lead, Detuned Lead |
| Pad | 8 | Warm Pad, Evolving Pad, Choir Pad, Shimmer Pad |
| Pluck | 6 | Bell, Marimba, Kalimba, Pizzicato |
| Keys | 5 | Electric Piano, Organ, Clav, Rhodes, Wurli |
| FX | 5 | Riser, Sweep, Noise Texture, Impact, Drone |
| Strings | 5 | Analog Strings, Cinematic Strings |
| Ambient | 5 | Atmosphere, Ethereal, Texture |

```csharp
IReadOnlyList<PresetData> all = FactoryPresets.All();
PresetData init = FactoryPresets.Init();
```

### Como adicionar um factory preset

Edite `FactoryPresets.cs`:

```csharp
public static IReadOnlyList<PresetData> All() {
    return new List<PresetData> {
        // ... existing ...
        BuildMyCustom("My Lead", "Square", cutoff: 3000, resonance: 0.4),
    };
}

private static PresetData BuildMyCustom(string name, string wt, double cutoff, double resonance) {
    var p = new PresetData { Name = name, Category = "Lead" };
    p.Osc1.Wavetable = wt;
    p.Osc1.Enabled = true;
    p.Osc1.Level = 0.8;
    p.Filter1.Mode = "LowPass";
    p.Filter1.Cutoff = cutoff;
    p.Filter1.Resonance = resonance;
    // ... configure envelopes, mod routes, effects ...
    return p;
}
```

---

## 6. JSON anatomy — exemplo

```json
{
  "Name": "Sub Bass",
  "Author": "Factory",
  "Category": "Bass",
  "Tags": [],
  "Description": "",
  "Version": "1.0",
  "CreatedDate": "2026-05-20",
  "MasterVolume": 0.8,
  "PitchBendRange": 2.0,
  "Osc1": {
    "Wavetable": "Saw",
    "Level": 0.85,
    "Enabled": true,
    "Pan": 0,
    "CoarseTune": -12,
    "FineTune": 0,
    "WavetablePosition": 0,
    "UnisonVoices": 1
  },
  "Osc2": { ... },
  "Osc3": { ... },
  "Noise": { "Level": 0, "Enabled": false, "Type": "White" },
  "Filter1": {
    "Mode": "LowPass",
    "Cutoff": 250,
    "Resonance": 0.0,
    "Drive": 0,
    "Is24dB": true
  },
  "EnvAmp": { "Attack": 0.001, "Decay": 0.2, "Sustain": 0.85, "Release": 0.1 },
  "ModRoutes": [
    { "Source": "Velocity", "Destination": "Filter1Cutoff", "Amount": 0.4, "Active": true }
  ],
  "Macros": [0, 0, 0, 0],
  "Effects": [
    {
      "Type": "Reverb",
      "Bypass": false,
      "Mix": 0.2,
      "Parameters": { "Size": 0.4, "Decay": 0.5, "Damping": 0.3 }
    }
  ]
}
```

---

## 7. Versionamento

Cada PresetData tem `Version: "1.0"`. Se o formato mudar (e.g., adicionar campos obrigatórios), incremente esse versionamento e adicione migração no `PresetManager.Load`:

```csharp
public PresetData Load(string filePath) {
    var json = File.ReadAllText(filePath);
    var preset = JsonSerializer.Deserialize<PresetData>(json, JsonOpts)!;
    MigrateIfNeeded(preset);
    return preset;
}

private void MigrateIfNeeded(PresetData p) {
    if (p.Version == "1.0") {
        // Migrate to 1.1: e.g., set NewField default
        p.Version = "1.1";
    }
}
```

---

## 8. Diretório padrão

Default: `%APPDATA%\OttoSynth\Presets\<Category>\<Name>.ottopreset`

Para customizar:
```csharp
var mgr = new PresetManager(@"C:\MyPresets");
```

`EnsureDirectory()` cria a pasta se não existir.

---

## 9. Testes

`tests/OttoSynth.Core.Tests/Preset/PresetManagerTests.cs` — 5 testes:
- Round-trip JSON preserva todos os campos
- Capture e Apply round-tripa estado do engine
- Apply com effects reconstrói a chain
- FactoryPresets.All() retorna ≥50 presets
- Save/Load file round-trip

Rode: `dotnet test --filter "FullyQualifiedName~Preset"`.

---

> **Próximo**: `09-VST3-Plugin.md`.

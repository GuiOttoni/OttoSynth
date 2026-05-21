---
sidebar_position: 7
title: UI Controls
---

# OttoSynth — UI Controls

:::info
Cada controle WPF customizado, suas propriedades e como usá-lo.
:::

## 1. Convenções gerais

Todos os controles estão em `src/OttoSynth.UI/Controls/`. São derivados de `System.Windows.Controls.Control`, com `OnRender` customizado para máxima performance (WriteableBitmap ou DrawingContext direto).

Eles:
- Usam **DependencyProperty** para binding two-way com o ViewModel.
- Disparam `InvalidateVisual()` quando o valor muda.
- Renderizam tudo no `OnRender` (sem ControlTemplate XAML — mais leve).

A **paleta** é definida em `Themes/DarkTheme.xaml` (ResourceDictionary), com chaves nomeadas:
- `BackgroundPrimaryBrush` (#0D0D0D)
- `AccentPrimaryBrush` (#00D4FF) — neon cyan
- `AccentSecondaryBrush` (#7B2FBE) — neon purple
- `AccentTertiaryBrush` (#E94560) — neon red
- `WaveformBrush` (#00FF88) — verde fluo
- ...

Para mudar cores, edite o XAML — todos os controles seguem.

---

## 2. SynthKnob

Knob rotativo, principal controle do sintetizador.

### Propriedades
| Property | Tipo | Default | Descrição |
|---|---|---|---|
| `Value` | double | 0.5 | Valor atual. Two-way binding. |
| `Minimum` | double | 0 | Limite inferior |
| `Maximum` | double | 1 | Limite superior |
| `DefaultValue` | double | 0.5 | Valor restaurado em double-click |
| `IsBipolar` | bool | false | Se true, knob "preenche" do centro |
| `Label` | string | "" | Texto acima do knob |
| `Unit` | string | "" | Sufixo após o valor (e.g. " Hz") |
| `ValueFormat` | string | "F2" | Formato de número (e.g. "F0", "P0") |
| `AccentBrush` | Brush | cyan | Cor do arco ativo |

### Interação
| Ação | Efeito |
|---|---|
| **Drag vertical** | Muda value (sensibilidade normal) |
| **Shift + drag** | Modo fino (5× mais lento) |
| **Mouse wheel** | Incrementa/decrementa |
| **Double-click** | Abre popup de entrada manual |
| **Ctrl + double-click** | Reseta para `DefaultValue` |

#### Popup de entrada manual
Ao dar double-click no knob, um campo de texto flutante aparece centralizado sobre o controle com tema Matrix (borda verde fosforescente, fundo escuro). O usuário digita o valor e confirma:
- **Enter / Return**: aplica o valor e fecha.
- **Escape**: descarta e fecha (valor anterior mantido).
- Clicar fora do popup também fecha (StaysOpen = false), aplicando o valor digitado.

O valor é parseado como `double` usando `InvariantCulture` (ponto decimal, não vírgula).

### Renderização
- Anel externo cinza (track).
- Arco colorido (de -225° a +45°, no espaço WPF) representa o valor.
- Indicator line aponta para o valor atual.
- Centro: disco gradient.
- Texto: label acima, value abaixo.

### Como usar no XAML
```xml
<ctl:SynthKnob Label="CUTOFF" 
               Minimum="20" Maximum="20000" 
               Value="{Binding Cutoff, Mode=TwoWay}"
               DefaultValue="20000" 
               ValueFormat="F0" Unit=" Hz"
               ValueChanged="OnCutoffChanged"/>
```

---

## 3. WaveformDisplay

Mostra um array de samples como linha (osciloscópio).

### Propriedades
| Property | Tipo | Descrição |
|---|---|---|
| `Samples` | double[] | Array a renderizar. Atualizar dispara redraw. |
| `WaveformBrush` | Brush | Cor da linha |
| `GridBrush` | Brush | Cor das linhas de grade |

### Performance
Renderiza com `StreamGeometry` (geometria otimizada para curva). Tipicamente 512 amostras a 30fps custa < 1ms.

### Como usar
```csharp
// No DispatcherTimer da UI:
var samples = new double[512];
_waveProvider.GetLastBuffer(samples);
waveformDisplay.Samples = samples;  // dispara InvalidateVisual
```

---

## 4. SpectrumAnalyzer

FFT display em barras logarítmicas (20Hz - 20kHz).

### Propriedades
| Property | Descrição |
|---|---|
| `Samples` | double[] (≥ FFT size = 512) |
| `SampleRate` | sample rate atual |
| `BarBrush` | cor das barras |

### Algoritmo
1. Hann window
2. FFT in-place (Cooley-Tukey radix-2, implementação interna)
3. Magnitudes em dB (-90..0)
4. Smoothing exponencial (75% memória)
5. Mapeia 64 barras em escala log de freq

### Tamanho FFT
512 samples. Resolução de freq: 44100/512 ≈ 86Hz por bin. Razoável para um analisador visual.

---

## 5. EnvelopeEditor

Visualização gráfica de uma envelope ADSR.

### Propriedades
- `Attack`, `Decay`, `Sustain`, `Release` (double)
- `LineBrush`

### Como usar
```xml
<ctl:EnvelopeEditor 
    Attack="{Binding Value, ElementName=AttackKnob}"
    Decay="{Binding Value, ElementName=DecayKnob}"
    Sustain="{Binding Value, ElementName=SustainKnob}"
    Release="{Binding Value, ElementName=ReleaseKnob}"/>
```

Os 4 segmentos são desenhados como linha + dots nos pontos de transição.

> Não interativo (futuro: drag pontos para mudar ADSR diretamente).

---

## 6. LfoDisplay

Mostra a forma de onda do LFO (2 ciclos).

### Propriedades
- `Shape` (LfoShape enum)
- `LineBrush`

### LfoShape
- Sine, Triangle, SawUp, SawDown, Square, SampleHold

---

## 7. PianoKeyboard

Teclado virtual com white keys e black keys clicáveis.

### Propriedades
- `StartNote` (int, MIDI note number)
- `EndNote` (int)

### Eventos
- `NoteOn` (`EventHandler<int>`): dispara com o número da nota MIDI quando o usuário clica.
- `NoteOff`: dispara quando solta ou sai da tecla.

### Interação
- Click → NoteOn
- Release / mouse-leave → NoteOff
- Drag arrastando entre teclas → automatic NoteOff/NoteOn (glissando do mouse)

### Como destacar uma nota externamente
```csharp
keyboard.SetNoteActive(60, true);   // C4 fica colorido
```
Útil para mostrar visualmente notas tocadas via MIDI hardware.

---

## 8. ModMatrixGrid

Listagem visual das rotas de modulação.

### Propriedades
- `Routes` (`IList<Route>`)
  - Cada `Route`: `Source` (string), `Destination` (string), `Amount` (double)

### Renderização
Linhas com "Source → Destination" e uma barra bidirectional (verde se +, vermelho se -).

> Atualmente é **somente leitura**. Futuro: drag-and-drop entre source pickers e destination pickers.

---

## 9. EffectSlot

Card visual para um efeito na cadeia.

### Propriedades
- `EffectName` (string)
- `IsBypassed` (bool, two-way)
- `Mix` (double, two-way)

### Renderização
- Card retangular arredondado
- Border colorido (cyan se ativo, cinza se bypassed)
- Texto do nome
- Barra horizontal de Mix abaixo

### Como usar
```csharp
var slot = new EffectSlot {
    EffectName = fx.Name,
    IsBypassed = fx.Bypass,
    Mix = fx.Mix
};
slot.MouseLeftButtonDown += (s,e) => { fx.Bypass = !fx.Bypass; };
EffectsPanel.Children.Add(slot);
```

---

## 10. Como adicionar um controle novo

1. Criar classe em `OttoSynth.UI/Controls/MyControl.cs`:
```csharp
public class MyControl : Control {
    public static readonly DependencyProperty FooProperty =
        DependencyProperty.Register(nameof(Foo), typeof(double), typeof(MyControl),
        new PropertyMetadata(0.0, (d, _) => ((MyControl)d).InvalidateVisual()));
    
    public double Foo { get => (double)GetValue(FooProperty); set => SetValue(FooProperty, value); }
    
    protected override void OnRender(DrawingContext dc) {
        base.OnRender(dc);
        // ... usar dc.DrawXxx ...
    }
}
```

2. Usar no XAML:
```xml
xmlns:ctl="clr-namespace:OttoSynth.UI.Controls;assembly=OttoSynth.UI"
...
<ctl:MyControl Foo="{Binding ...}" />
```

### Dicas
- Use `StreamGeometry` para curvas (otimizado).
- Freeze brushes/pens compartilhados para evitar realloc.
- Cache `FormattedText` se reutilizar a mesma string.
- Limite redraws via `Throttle` se o valor muda muito (não relevante para knob por mouse).

---

## 11. MainWindow.xaml — layout principal

Em `OttoSynth.Standalone/MainWindow.xaml`. Layout em **grid 5 rows × 2 cols**:

```
Row 0: HEADER (logo, preset selector, MIDI device)
Row 1: MAIN CONTENT
  Col 0:  OSC1+2+3 (top), Filter (middle), LFO1+2+3 (bottom)
  Col 1:  Waveform/Spectrum (top), Envelope (middle), ModMatrix + Macros (bottom)
Row 2: Effects rack (horizontal slots)
Row 3: Piano keyboard
Row 4: Status bar
```

Edite este XAML para mudar layout. Code-behind (`MainWindow.xaml.cs`) faz wiring entre UI e engine.

---

## 12. Theme: como criar um light theme

1. Duplique `Themes/DarkTheme.xaml` como `LightTheme.xaml`.
2. Substitua cores: background claro, accent darker.
3. Carregue dinamicamente:
```csharp
// Substituir merged dictionary
var rd = new ResourceDictionary { Source = new Uri("/OttoSynth.UI;component/Themes/LightTheme.xaml", UriKind.Relative) };
Resources.MergedDictionaries.Clear();
Resources.MergedDictionaries.Add(rd);
```

---

> **Próximo**: `08-Preset-System.md`.

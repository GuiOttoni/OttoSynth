using System.Windows.Threading;
using OttoSynth.Core;
using OttoSynth.Core.Preset;

namespace OttoSynth.UI.ViewModels;

public class MasterViewModel : ViewModelBase
{
    private readonly SynthEngine _engine;
    private bool _loading;

    private double _masterVolume = 0.8;
    public double MasterVolume { get => _masterVolume; set => SetField(ref _masterVolume, value); }

    private double _macro1 = 0;
    public double Macro1 { get => _macro1; set => SetField(ref _macro1, value); }

    private double _macro2 = 0;
    public double Macro2 { get => _macro2; set => SetField(ref _macro2, value); }

    private double _macro3 = 0;
    public double Macro3 { get => _macro3; set => SetField(ref _macro3, value); }

    private double _macro4 = 0;
    public double Macro4 { get => _macro4; set => SetField(ref _macro4, value); }

    private string _macro1Cc = "---";
    public string Macro1Cc { get => _macro1Cc; private set => SetField(ref _macro1Cc, value); }

    private string _macro2Cc = "---";
    public string Macro2Cc { get => _macro2Cc; private set => SetField(ref _macro2Cc, value); }

    private string _macro3Cc = "---";
    public string Macro3Cc { get => _macro3Cc; private set => SetField(ref _macro3Cc, value); }

    private string _macro4Cc = "---";
    public string Macro4Cc { get => _macro4Cc; private set => SetField(ref _macro4Cc, value); }

    private bool _macro1Learning;
    public bool Macro1Learning
    {
        get => _macro1Learning;
        private set { SetField(ref _macro1Learning, value); OnPropertyChanged(nameof(Macro1LearnText)); }
    }

    private bool _macro2Learning;
    public bool Macro2Learning
    {
        get => _macro2Learning;
        private set { SetField(ref _macro2Learning, value); OnPropertyChanged(nameof(Macro2LearnText)); }
    }

    private bool _macro3Learning;
    public bool Macro3Learning
    {
        get => _macro3Learning;
        private set { SetField(ref _macro3Learning, value); OnPropertyChanged(nameof(Macro3LearnText)); }
    }

    private bool _macro4Learning;
    public bool Macro4Learning
    {
        get => _macro4Learning;
        private set { SetField(ref _macro4Learning, value); OnPropertyChanged(nameof(Macro4LearnText)); }
    }

    public string Macro1LearnText => _macro1Learning ? "● WAIT" : "LEARN";
    public string Macro2LearnText => _macro2Learning ? "● WAIT" : "LEARN";
    public string Macro3LearnText => _macro3Learning ? "● WAIT" : "LEARN";
    public string Macro4LearnText => _macro4Learning ? "● WAIT" : "LEARN";

    private readonly bool[] _macroLearning = new bool[4];

    public MasterViewModel(SynthEngine engine)
    {
        _engine = engine;

        PropertyChanged += (_, e) =>
        {
            if (_loading) return;
            switch (e.PropertyName)
            {
                case nameof(MasterVolume): _engine.MasterVolume = MasterVolume; break;
                case nameof(Macro1): _engine.SetMacro(0, Macro1); break;
                case nameof(Macro2): _engine.SetMacro(1, Macro2); break;
                case nameof(Macro3): _engine.SetMacro(2, Macro3); break;
                case nameof(Macro4): _engine.SetMacro(3, Macro4); break;
            }
        };
    }

    public void ToggleLearn(int macroIndex)
    {
        if (_engine.IsLearningCc && _macroLearning[macroIndex])
        {
            _engine.CancelLearn();
            SetLearningState(macroIndex, false);
            return;
        }

        _engine.CancelLearn();
        for (int i = 0; i < 4; i++) SetLearningState(i, false);

        SetLearningState(macroIndex, true);
        var dispatcher = Dispatcher.CurrentDispatcher;
        _engine.LearnMacroCc(macroIndex, (idx, cc) =>
        {
            dispatcher.BeginInvoke(() =>
            {
                SetLearningState(idx, false);
                UpdateCcLabel(idx, cc);
            });
        });
    }

    public void UnmapMacroCc(int macroIndex)
    {
        _engine.MapMacroCc(macroIndex, 255);
        UpdateCcLabel(macroIndex, 255);
    }

    public void ApplyPreset(PresetData p)
    {
        _loading = true;
        try
        {
            MasterVolume = p.MasterVolume;
            Macro1 = p.Macros.Length > 0 ? p.Macros[0] : 0;
            Macro2 = p.Macros.Length > 1 ? p.Macros[1] : 0;
            Macro3 = p.Macros.Length > 2 ? p.Macros[2] : 0;
            Macro4 = p.Macros.Length > 3 ? p.Macros[3] : 0;

            if (p.MacroCcNumbers != null)
                for (int i = 0; i < Math.Min(4, p.MacroCcNumbers.Length); i++)
                    UpdateCcLabel(i, (byte)p.MacroCcNumbers[i]);
        }
        finally
        {
            _loading = false;
            _engine.MasterVolume = MasterVolume;
            _engine.SetMacro(0, Macro1);
            _engine.SetMacro(1, Macro2);
            _engine.SetMacro(2, Macro3);
            _engine.SetMacro(3, Macro4);
        }
    }

    private void SetLearningState(int idx, bool value)
    {
        _macroLearning[idx] = value;
        switch (idx)
        {
            case 0: Macro1Learning = value; break;
            case 1: Macro2Learning = value; break;
            case 2: Macro3Learning = value; break;
            case 3: Macro4Learning = value; break;
        }
    }

    private void UpdateCcLabel(int idx, byte cc)
    {
        string label = cc == 255 ? "---" : $"CC{cc}";
        switch (idx)
        {
            case 0: Macro1Cc = label; break;
            case 1: Macro2Cc = label; break;
            case 2: Macro3Cc = label; break;
            case 3: Macro4Cc = label; break;
        }
    }
}

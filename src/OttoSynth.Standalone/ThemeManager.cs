using System;
using System.Linq;
using System.Windows;

namespace OttoSynth.Standalone;

public static class ThemeManager
{
    public static readonly string[] ThemeNames = ["Matrix", "Minimal", "Aero"];

    private static string _current = "Matrix";
    public static string Current => _current;

    public static void Apply(string name)
    {
        string uri = name switch
        {
            "Minimal" => "/OttoSynth.UI;component/Themes/ThemeMinimal.xaml",
            "Aero"    => "/OttoSynth.UI;component/Themes/ThemeAero.xaml",
            _         => "/OttoSynth.UI;component/Themes/ThemeMatrix.xaml",
        };

        var dicts = Application.Current.Resources.MergedDictionaries;
        var old = dicts.FirstOrDefault(d =>
            d.Source != null && d.Source.OriginalString.Contains("/Themes/Theme"));
        if (old != null) dicts.Remove(old);

        dicts.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) });
        _current = name;
    }
}

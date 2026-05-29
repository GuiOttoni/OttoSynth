using System.Globalization;
using System.Windows.Data;

namespace OttoSynth.UI.Converters;

[ValueConversion(typeof(bool), typeof(string))]
public sealed class BoolToStarConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "★" : "☆";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is "★";
}

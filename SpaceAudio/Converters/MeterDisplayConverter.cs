using System.Globalization;
using System.Windows.Data;

namespace SpaceAudio.Converters;

[ValueConversion(typeof(double), typeof(string))]
public sealed class MeterDisplayConverter : IValueConverter
{
    public static readonly MeterDisplayConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double d ? $"{d:F1} m" : "0.0 m";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

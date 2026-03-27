using System.Globalization;
using System.Windows.Data;

namespace SpaceAudio.Converters;

[ValueConversion(typeof(double), typeof(string))]
public sealed class DecibelDisplayConverter : IValueConverter
{
    public static readonly DecibelDisplayConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double d ? $"{d:F1} dB" : "0.0 dB";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

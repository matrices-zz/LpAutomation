using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LpAutomation.Desktop.Converters;

public sealed class HeatDeltaToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var s = value as string ?? "";

        if (string.IsNullOrWhiteSpace(s))
            return new SolidColorBrush(Color.FromRgb(180, 180, 180)); // neutral gray

        // RISK UP = RED
        if (s.StartsWith("+", StringComparison.Ordinal))
            return new SolidColorBrush(Color.FromRgb(255, 120, 120)); // red (heat increasing)

        // RISK DOWN = GREEN
        if (s.StartsWith("-", StringComparison.Ordinal))
            return new SolidColorBrush(Color.FromRgb(120, 255, 140)); // green (cooling)

        return new SolidColorBrush(Color.FromRgb(180, 180, 180));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
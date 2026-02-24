using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace LpAutomation.Desktop.Avalonia.Converters;

public sealed class StatusKindToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value?.ToString() ?? "Neutral";

        return key switch
        {
            "Running" => new SolidColorBrush(Color.Parse("#90CAF9")), // blue-ish
            "Success" => new SolidColorBrush(Color.Parse("#81C784")), // green-ish
            "Error" => new SolidColorBrush(Color.Parse("#EF9A9A")),   // red-ish
            _ => new SolidColorBrush(Color.Parse("#BDBDBD"))          // neutral
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

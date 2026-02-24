using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LpAutomation.Desktop.Avalonia.Converters;

public sealed class StatusKindToLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value?.ToString() ?? "Neutral";
        return key switch
        {
            "Running" => "RUNNING",
            "Success" => "SUCCESS",
            "Error" => "ERROR",
            _ => "IDLE"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace LpAutomation.Desktop.Avalonia.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Wrapping in parentheses disambiguates the precedence for the compiler
        return (value as string switch
        {
            "Success" => Brushes.LimeGreen,
            "Error" => Brushes.OrangeRed,
            "Running" => Brushes.DodgerBlue,
            _ => Brushes.Gray
        });
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
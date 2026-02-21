using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LpAutomation.Desktop.Converters;

public sealed class HeatToBrushConverter : IValueConverter
{
    // Heat thresholds (keep aligned with server policy if you want later)
    private const int Cool = 40;
    private const int Hot = 70;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Default: neutral-ish translucent background
        if (value is null) return new SolidColorBrush(Color.FromArgb(28, 255, 255, 255));

        int heat;
        try
        {
            heat = System.Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return new SolidColorBrush(Color.FromArgb(28, 255, 255, 255));
        }

        // NOTE: Keep alpha low so it doesn’t scream. Dark theme-friendly.
        if (heat <= Cool)
            return new SolidColorBrush(Color.FromArgb(40, 120, 255, 140)); // calm green-ish
        if (heat >= Hot)
            return new SolidColorBrush(Color.FromArgb(45, 255, 120, 120)); // hot red-ish

        return new SolidColorBrush(Color.FromArgb(40, 255, 220, 120));     // mid amber-ish
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
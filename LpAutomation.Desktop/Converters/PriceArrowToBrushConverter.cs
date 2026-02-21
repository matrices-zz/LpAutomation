using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LpAutomation.Desktop.Converters;

public sealed class PriceArrowToBrushConverter : IValueConverter
{
    // Price semantics:
    //   up    => green  (price rising)
    //   down  => red    (price falling)
    //   mixed => amber  (timeframes disagree)
    //   flat  => gray

    private static readonly Brush UpBrush = MakeBrush(120, 255, 140);      // green
    private static readonly Brush DownBrush = MakeBrush(255, 120, 120);    // red
    private static readonly Brush MixedBrush = MakeBrush(255, 220, 120);   // amber
    private static readonly Brush NeutralBrush = MakeBrush(180, 180, 180); // gray

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var raw = (value as string)?.Trim();
        if (string.IsNullOrEmpty(raw))
            return NeutralBrush;

        var c = raw[0];

        return c switch
        {
            '▲' or '↑' => UpBrush,
            '▼' or '↓' => DownBrush,
            '↕' or '↔' => MixedBrush,
            '→' or '—' or '-' => NeutralBrush,
            _ => NeutralBrush
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    private static Brush MakeBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
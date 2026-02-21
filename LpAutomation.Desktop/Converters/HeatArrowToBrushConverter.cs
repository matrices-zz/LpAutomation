using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LpAutomation.Desktop.Converters;

public sealed class HeatArrowToBrushConverter : IValueConverter
{
    // RISK/HEAT semantics (LP-friendly):
    //   up    => red   (risk rising / heat rising)
    //   down  => green (risk falling / cooling)
    //   mixed => amber (chop/unclear)
    //   flat  => neutral gray

    private static readonly Brush UpBrush = MakeBrush(255, 120, 120);      // red
    private static readonly Brush DownBrush = MakeBrush(120, 255, 140);    // green
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
            '▲' => UpBrush,
            '↑' => UpBrush,

            '▼' => DownBrush,
            '↓' => DownBrush,

            '↕' => MixedBrush,
            '↔' => MixedBrush,

            '→' => NeutralBrush,
            '—' => NeutralBrush,
            '-' => NeutralBrush,

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
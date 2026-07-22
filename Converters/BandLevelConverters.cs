using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EQAPO_Configurator.Converters;

/// <summary>
/// Converts a band's dB level and a reference width into a bar width (0-1 normalized).
/// BandLevelDb: -80 to 0 dB → 0% to 100% of the available width.
/// </summary>
public class BandLevelToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not double levelDb || values[1] is not double totalWidth)
            return 0.0;

        double normalized = Math.Clamp((levelDb + 80) / 80, 0, 1);
        return normalized * totalWidth;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts a band's dB level to a color: green (quiet) → yellow (mid) → red (loud).
/// -80 dB = transparent, -50 = green, -30 = yellow, -10 = red.
/// </summary>
public class BandLevelToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush TransparentBrush = new(Colors.Transparent);
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(0, 200, 80));
    private static readonly SolidColorBrush YellowBrush = new(Color.FromRgb(255, 200, 0));
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(255, 80, 60));

    static BandLevelToColorConverter()
    {
        TransparentBrush.Freeze();
        GreenBrush.Freeze();
        YellowBrush.Freeze();
        RedBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double levelDb || levelDb < -70)
            return TransparentBrush;

        if (levelDb < -40) return GreenBrush;
        if (levelDb < -20) return YellowBrush;
        return RedBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

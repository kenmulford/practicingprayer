using System.Globalization;
using Microsoft.Maui.Controls;

namespace PrayerApp.Converters;

public class BoolToMutedColorConverter : IValueConverter
{
    // Normal text color when not answered; muted gray when answered
    private static readonly Color _normalColor = Color.FromArgb("#1f1f1f");  // OffBlack
    private static readonly Color _mutedColor  = Color.FromArgb("#919191");  // Gray400

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? _mutedColor : _normalColor;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

using System.Globalization;
using Microsoft.Maui.Controls;

namespace PrayerApp.Converters;

public class BoolToMutedColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

        // Answered (muted): Gray400 light / Gray500 dark
        // Active (normal): OffBlack light / White dark
        string key = value is true
            ? (isDark ? "Gray500" : "Gray400")
            : (isDark ? "White" : "OffBlack");

        if (Application.Current?.Resources.TryGetValue(key, out var res) == true && res is Color color)
            return color;

        // Fallback — mirrors Colors.xaml definitions in case resources aren't loaded yet
        if (value is true)
            return isDark ? Colors.Gray : Color.FromArgb("#919191");
        return isDark ? Colors.White : Color.FromArgb("#1f1f1f");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

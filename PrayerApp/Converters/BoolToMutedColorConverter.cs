using System.Globalization;
using Microsoft.Maui.Controls;

namespace PrayerApp.Converters;

public class BoolToMutedColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Resolve from resource dictionary so changes to Colors.xaml propagate here automatically
        var key = value is true ? "Gray400" : "OffBlack";
        if (Application.Current?.Resources.TryGetValue(key, out var res) == true && res is Color color)
            return color;

        // Fallback — mirrors Colors.xaml definitions in case resources aren't loaded yet
        return value is true ? Color.FromArgb("#919191") : Color.FromArgb("#1f1f1f");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

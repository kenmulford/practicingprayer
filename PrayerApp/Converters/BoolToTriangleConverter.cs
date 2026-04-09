using System.Globalization;

namespace PrayerApp.Converters;

/// <summary>
/// Converts a boolean to a triangle direction indicator for collapsible section headers.
/// true (expanded) → "▼", false (collapsed) → "▶"
/// </summary>
public class BoolToTriangleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "▽" : "▷";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

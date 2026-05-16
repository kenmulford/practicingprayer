using System.Globalization;
using Microsoft.Maui.Controls;

namespace PrayerApp.Converters;

public class BoolToMutedColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        bool isAnswered = value is true;

        // Answered (muted): Gray400 light / Gray500 dark
        // Active (normal):  OffBlack light / White dark
        string key = GetResourceKey(isAnswered, isDark);

        if (Application.Current?.Resources.TryGetValue(key, out var res) == true && res is Color color)
            return color;

        // Fallback fires when Application.Current.Resources hasn't loaded yet
        // (test host, early startup). Must mirror Colors.xaml exactly.
        return GetFallbackColor(isAnswered, isDark);
    }

    // Resource key for the primary lookup path. Kept internal to enable
    // test coverage of all four input combinations without spinning up MAUI.
    internal static string GetResourceKey(bool isAnswered, bool isDark)
        => isAnswered
            ? (isDark ? "Gray500" : "Gray400")
            : (isDark ? "White" : "OffBlack");

    // Fallback color mirrors the named tokens above. Values must match
    // Resources/Styles/Colors.xaml exactly — divergence here produces a silent
    // visual bug when the resource dictionary hasn't loaded.
    internal static Color GetFallbackColor(bool isAnswered, bool isDark)
        => isAnswered
            ? (isDark ? Color.FromArgb("#6E6E6E") /* mirrors Gray500 */
                      : Color.FromArgb("#717171") /* mirrors Gray400 */)
            : (isDark ? Colors.White               /* mirrors White */
                      : Color.FromArgb("#1f1f1f") /* mirrors OffBlack */);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

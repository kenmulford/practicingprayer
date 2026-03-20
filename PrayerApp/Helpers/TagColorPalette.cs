using Microsoft.Maui.Controls;

namespace PrayerApp.Helpers
{
    /// <summary>
    /// Maps the 8 fixed tag color swatches to their light/dark mode Color values.
    /// The stored hex in PrayerTag.Color is always the light-mode value.
    /// </summary>
    public static class TagColorPalette
    {
        // Each entry: (LightHex, DarkHex, Label)
        public static readonly (string Light, string Dark, string Label)[] Swatches =
        {
            ("#B84040", "#D46060", "Red"),
            ("#B35A20", "#CC7040", "Orange"),
            ("#7A4020", "#A65C34", "Brown"),
            ("#1E7870", "#3AA898", "Teal"),
            ("#2E5A9A", "#507ACC", "Blue"),
            ("#663C8C", "#9460C0", "Purple"),
            ("#8C3860", "#B85A8C", "Pink"),
            ("#505050", "#848484", "Gray"),
        };

        /// <summary>
        /// Returns the theme-appropriate Color for the stored hex value.
        /// Falls back to the stored hex itself if not found in the palette.
        /// </summary>
        public static Color Resolve(string? storedHex)
        {
            if (string.IsNullOrEmpty(storedHex))
                return Color.FromArgb("#505050");

            bool isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

            foreach (var (light, dark, _) in Swatches)
            {
                if (string.Equals(light, storedHex, StringComparison.OrdinalIgnoreCase))
                    return Color.FromArgb(isDark ? dark : light);
            }

            // Not in palette — use the stored value directly
            return Color.FromArgb(storedHex);
        }

        /// <summary>
        /// Returns the dark-mode hex variant for a given light hex, or null if not in the palette.
        /// Used by TagDetailViewModel to pair user-saved colors with their dark variant.
        /// </summary>
        public static string? GetDarkVariant(string lightHex)
        {
            foreach (var (light, dark, _) in Swatches)
            {
                if (string.Equals(light, lightHex, StringComparison.OrdinalIgnoreCase))
                    return dark;
            }
            return null;
        }

        /// <summary>Returns White — all palette colors support white text.</summary>
        public static Color TextColor => Colors.White;
    }
}

using PrayerApp.Services;

namespace PrayerApp.Platforms.iOS;

public class ColorPickerService : IColorPickerService
{
    public async Task<string?> PickColorAsync()
    {
        var color = await NativeColorPicker.PickColorAsync();
        if (color is null) return null;

        // Convert MAUI Color to hex string
        var r = (int)(color.Red * 255);
        var g = (int)(color.Green * 255);
        var b = (int)(color.Blue * 255);
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}

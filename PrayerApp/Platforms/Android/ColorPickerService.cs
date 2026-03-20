using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using PrayerApp.Services;
using PrayerApp.Views.Tags;

namespace PrayerApp.Platforms.Android;

public class ColorPickerService : IColorPickerService
{
    public async Task<string?> PickColorAsync()
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null) return null;

        var popup = new ColorPickerPopup();
        await page.ShowPopupAsync(popup, null, CancellationToken.None);

        return popup.SelectedHex;
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using PrayerApp.Helpers;
using PrayerApp.ViewModels;

namespace PrayerApp.Services;

public static class BoxServiceExtensions
{
    /// <summary>
    /// Shared box-picker rows: Loose Cards first, then user-created boxes
    /// (non-system). Used by Confirm Import and card-edit pickers.
    /// </summary>
    public static async Task<List<RealBoxPickerItem>> GetBoxPickerItemsAsync(this IBoxService service)
    {
        var boxes = await service.GetBoxesAsync();
        var result = new List<RealBoxPickerItem>
        {
            new(0, BoxStrings.Unorganized),
        };
        result.AddRange(
            boxes.Where(b => !b.IsSystem).Select(b => new RealBoxPickerItem(b.Id, b.Name)));
        return result;
    }
}

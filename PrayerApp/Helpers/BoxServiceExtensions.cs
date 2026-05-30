using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Helpers;

public static class BoxServiceExtensions
{
    /// <summary>
    /// Builds the standard card-edit/import box picker list: Loose Cards first,
    /// followed by user-created boxes (system boxes excluded).
    /// </summary>
    public static async Task<List<RealBoxPickerItem>> GetBoxPickerItemsAsync(this IBoxService service)
    {
        var boxes = await service.GetBoxesAsync();
        var result = new List<RealBoxPickerItem> { new(0, BoxStrings.Unorganized) };
        result.AddRange(boxes.Where(b => !b.IsSystem).Select(b => new RealBoxPickerItem(b.Id, b.Name)));
        return result;
    }
}

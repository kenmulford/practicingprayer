using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PrayerApp.Models;

namespace PrayerApp.Services;

public class CategoryService : ICategoryService
{
    private IReadOnlyList<PrayerCategory>? _cache;

    public async Task<IReadOnlyList<PrayerCategory>> GetCategoriesAsync()
    {
        if (_cache is not null)
            return _cache;

        var list = await PrayerCategory.LoadAllAsync();

        var readOnly = new ReadOnlyCollection<PrayerCategory>(list.ToList());
        _cache = readOnly;
        return _cache;
    }
}
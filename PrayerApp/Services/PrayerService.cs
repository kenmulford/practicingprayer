using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using PrayerApp.Models;

namespace PrayerApp.Services;

public class PrayerService : IPrayerService
{
    private Dictionary<int, List<Prayer>>? _cardCache;
    private List<Prayer>? _allCache;

    public async Task<IReadOnlyList<Prayer>> GetPrayersByCardAsync(int prayerCardId)
    {
        if (_cardCache is not null && _cardCache.TryGetValue(prayerCardId, out var cached))
            return cached;

        var list = await Prayer.LoadByCardIdAsync(prayerCardId);

        _cardCache ??= new Dictionary<int, List<Prayer>>();
        _cardCache[prayerCardId] = list;
        return list;
    }

    public async Task<IReadOnlyList<Prayer>> GetAllPrayersAsync()
    {
        if (_allCache is not null)
            return _allCache;

        var list = await Prayer.LoadAllAsync();
        _allCache = list;
        return _allCache;
    }

    public async Task<IReadOnlyList<Prayer>> GetAllActivePrayersAsync()
    {
        var all = await GetAllPrayersAsync();
        return all.Where(p => !p.IsAnswered).ToList().AsReadOnly();
    }

    public async Task<Prayer> SavePrayerAsync(Prayer prayer)
    {
        await prayer.SaveAsync();
        _cardCache = null;
        _allCache = null;
        return prayer;
    }

    public async Task DeletePrayerAsync(Prayer prayer)
    {
        await prayer.DeleteAsync();
        _cardCache = null;
        _allCache = null;
    }

    public void InvalidateCache()
    {
        _cardCache = null;
        _allCache = null;
    }
}

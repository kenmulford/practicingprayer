using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PrayerApp.Models;

namespace PrayerApp.Services;

public class PrayerService : IPrayerService
{
    private readonly IDBService _dbService;
    private Dictionary<int, List<Prayer>>? _cardCache;
    private List<Prayer>? _allCache;

    public PrayerService(IDBService dbService)
    {
        _dbService = dbService;
    }

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
        await _dbService.DeleteInteractionsByPrayerIdAsync(prayer.Id);
        await _dbService.DeleteJunctionRowsByRequestIdAsync(prayer.Id);
        await prayer.DeleteAsync();
        _cardCache = null;
        _allCache = null;
    }

    public async Task<IReadOnlyList<Prayer>> GetOverduePrayersAsync(int dayThreshold = 30)
    {
        var allActive = await GetAllActivePrayersAsync();
        var latestInteractions = await _dbService.GetLatestInteractionByPrayerAsync();
        var latestByPrayer = latestInteractions.ToDictionary(r => r.PrayerId, r => r.LatestInteractionAt);

        var cutoff = DateTime.Now.AddDays(-dayThreshold);

        return allActive
            .Where(p => latestByPrayer.TryGetValue(p.Id, out var latest) && latest < cutoff)
            .OrderBy(p => latestByPrayer.TryGetValue(p.Id, out var latest) ? latest : DateTime.MinValue)
            .ToList()
            .AsReadOnly();
    }

    public async Task<int> GetActivePrayerCountByCardAsync(int cardId)
    {
        var prayers = await GetPrayersByCardAsync(cardId);
        return prayers.Count(p => !p.IsAnswered);
    }

    public async Task<DateTime?> GetLastInteractionDateAsync()
    {
        return await _dbService.GetMaxInteractionDateAsync();
    }

    public async Task<IReadOnlyList<Prayer>> GetAnsweredOnThisDateAsync()
    {
        var today = DateTime.Now;
        var all = await GetAllPrayersAsync();
        return all
            .Where(p => p.IsAnswered
                     && p.AnsweredAt is { } at
                     && at.Month == today.Month
                     && at.Day == today.Day
                     && at.Year < today.Year)
            .ToList()
            .AsReadOnly();
    }

    public void InvalidateCache()
    {
        _cardCache = null;
        _allCache = null;
    }
}

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

    /// <summary>
    /// In-flight load task. When N concurrent callers arrive while the cache is empty,
    /// they all await the same task instead of each kicking off their own DB query.
    /// Cleared once the task completes.
    /// </summary>
    private Task<List<Prayer>>? _allLoadTask;

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

        // Coalesce concurrent callers onto a single in-flight task. ?? = is non-atomic
        // but on MAUI's single SynchronizationContext (UI thread for VM fire-and-forget
        // continuations), the read+assign happens before any caller's await yields,
        // so concurrent callers see the same task.
        var task = _allLoadTask ??= Prayer.LoadAllAsync();
        try
        {
            var list = await task;
            _allCache = list;
            return list;
        }
        finally
        {
            if (_allLoadTask == task) _allLoadTask = null;
        }
    }

    public async Task<IReadOnlyList<Prayer>> GetAllActivePrayersAsync()
    {
        var all = await GetAllPrayersAsync();
        return all.Where(p => !p.IsAnswered).ToList().AsReadOnly();
    }

    public async Task<Prayer> SavePrayerAsync(Prayer prayer)
    {
        await prayer.SaveAsync();
        InvalidateCache();
        return prayer;
    }

    public async Task DeletePrayerAsync(Prayer prayer)
    {
        await _dbService.DeleteInteractionsByPrayerIdAsync(prayer.Id);
        await _dbService.DeleteJunctionRowsByRequestIdAsync(prayer.Id);
        await prayer.DeleteAsync();
        InvalidateCache();
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
        // Route through the all-prayers cache so a batch of N per-card calls collapses
        // to one DB read (or zero, on cache hit). _allCache and _cardCache are invalidated
        // together by InvalidateCache / SavePrayerAsync / DeletePrayerAsync, so this read
        // sees the same freshness as the per-card slice would.
        var all = await GetAllPrayersAsync();
        return all.Count(p => p.PrayerCardId == cardId && !p.IsAnswered);
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
        // Don't cancel an in-flight load — its callers expect a result. The next
        // GetAllPrayersAsync call will start a fresh task because _allCache is null.
        _allLoadTask = null;
    }
}

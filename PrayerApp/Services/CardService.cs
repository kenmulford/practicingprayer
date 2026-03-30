using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PrayerApp.Models;

namespace PrayerApp.Services;

public class CardService : ICardService
{
    public const string QuickAddTitle = "Quick Add";
    public const string SharedWithMeTitle = "Shared with me";

    private IReadOnlyList<PrayerCard>? _cache;

    public async Task<IReadOnlyList<PrayerCard>> GetCardsAsync()
    {
        if (_cache is not null)
            return _cache;

        var list = await PrayerCard.LoadAllAsync();

        var readOnly = new ReadOnlyCollection<PrayerCard>(list.ToList());
        _cache = readOnly;
        return _cache;
    }

    public Task<PrayerCard> GetOrCreateQuickAddCardAsync()
        => GetOrCreateSystemCardAsync(QuickAddTitle);

    public Task<PrayerCard> GetOrCreateSharedCardAsync()
        => GetOrCreateSystemCardAsync(SharedWithMeTitle);

    private async Task<PrayerCard> GetOrCreateSystemCardAsync(string title)
    {
        var cards = await GetCardsAsync();
        var existing = cards.FirstOrDefault(c => c.IsSystem && c.Title == title);
        if (existing is not null)
            return existing;

        var card = new PrayerCard { Title = title, IsSystem = true };
        await card.SaveAsync();
        _cache = null;
        return card;
    }

    public async Task<PrayerCard> SaveCardAsync(PrayerCard card)
    {
        await card.SaveAsync();
        _cache = null;
        return card;
    }

    public async Task DeleteCardAsync(PrayerCard card)
    {
        await card.DeleteAsync();
        _cache = null;
    }

    public void InvalidateCache()
    {
        _cache = null;
    }
}

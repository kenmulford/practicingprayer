using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PrayerApp.Models;

namespace PrayerApp.Services;

public class CardService : ICardService
{
    public const string QuickAddTitle = PrayerCard.TitleQuickAdd;
    public const string SharedWithMeTitle = PrayerCard.TitleSharedWithMe;

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
        => GetOrCreateSystemCardAsync(QuickAddTitle, PrayerCard.SystemKeyQuickAdd);

    public Task<PrayerCard> GetOrCreateSharedCardAsync()
        => GetOrCreateSystemCardAsync(SharedWithMeTitle, PrayerCard.SystemKeySharedWithMe);

    private async Task<PrayerCard> GetOrCreateSystemCardAsync(string title, string systemKey)
    {
        var cards = await GetCardsAsync();
        var existing = cards.FirstOrDefault(c => c.IsSystem && c.Title == title);
        if (existing is not null)
            return existing;

        // Look up the System box so new system cards land in the right collection
        var boxes = await CardBox.LoadAllAsync();
        var sysBox = boxes.FirstOrDefault(b => b.SystemKey == CardBox.SystemKeySystem);

        var card = new PrayerCard
        {
            Title = title,
            IsSystem = true,
            SystemKey = systemKey,
            BoxId = sysBox?.Id ?? 0
        };
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

    public async Task AssignBoxAsync(PrayerCard card, int boxId)
    {
        card.BoxId = boxId;
        await card.SaveAsync();
        _cache = null;
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

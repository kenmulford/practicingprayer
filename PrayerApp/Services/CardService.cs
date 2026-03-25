using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PrayerApp.Models;

namespace PrayerApp.Services;

public class CardService : ICardService
{
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

    public async Task<PrayerCard> GetOrCreateQuickAddCardAsync()
    {
        var cards = await GetCardsAsync();
        var systemCard = cards.FirstOrDefault(c => c.IsSystem);
        if (systemCard is not null)
            return systemCard;

        var newCard = new PrayerCard { Title = "Quick Add", IsSystem = true };
        await newCard.SaveAsync();
        _cache = null;
        return newCard;
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

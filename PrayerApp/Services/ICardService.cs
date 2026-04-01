using System.Collections.Generic;
using System.Threading.Tasks;

using PrayerApp.Models;

namespace PrayerApp.Services;

public interface ICardService
{
    Task<IReadOnlyList<PrayerCard>> GetCardsAsync();
    Task<PrayerCard> GetOrCreateQuickAddCardAsync();
    Task<PrayerCard> GetOrCreateSharedCardAsync();
    Task<PrayerCard> SaveCardAsync(PrayerCard card);
    Task DeleteCardAsync(PrayerCard card);
    void InvalidateCache();
}

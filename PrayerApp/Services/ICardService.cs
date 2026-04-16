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

    /// <summary>
    /// Assigns a card to a box. Use boxId = 0 for Unboxed.
    /// To check if archived: card.BoxId == Settings.ArchivedFolderId.
    /// Prayer request status is never affected.
    /// </summary>
    Task AssignBoxAsync(PrayerCard card, int boxId);

    void InvalidateCache();
}

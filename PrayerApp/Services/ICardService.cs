using System.Collections.Generic;
using System.Threading.Tasks;

using PrayerApp.Models;

namespace PrayerApp.Services;

public interface ICardService
{
    Task<IReadOnlyList<PrayerCard>> GetCardsAsync();
    Task<PrayerCard> GetOrCreateQuickAddCardAsync();
    Task<PrayerCard> GetOrCreateSharedCardAsync();
    /// <summary>
    /// Saves a card. Set <paramref name="publishMessage"/> = false from bulk-import
    /// callers (e.g. <see cref="ViewModels.ConfirmImportViewModel"/>) so per-entity messages
    /// don't fan out alongside the import's <c>BulkChangedMessage</c>.
    /// </summary>
    Task<PrayerCard> SaveCardAsync(PrayerCard card, bool publishMessage = true);

    /// <summary>
    /// Deletes a card. Set <paramref name="publishMessage"/> = false from cascade callers
    /// (e.g. <see cref="IBoxService.DeleteBoxAsync"/>) so per-entity messages don't fan out
    /// alongside the cascade's <c>BulkChangedMessage</c>.
    /// </summary>
    Task DeleteCardAsync(PrayerCard card, bool publishMessage = true);

    /// <summary>
    /// Assigns a card to a box. Use boxId = 0 for Unboxed.
    /// To check if archived: card.BoxId == Settings.ArchivedFolderId.
    /// Prayer request status is never affected.
    /// </summary>
    Task AssignBoxAsync(PrayerCard card, int boxId);

    void InvalidateCache();
}

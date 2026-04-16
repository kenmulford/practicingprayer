using System.Collections.Generic;
using System.Threading.Tasks;

using PrayerApp.Models;

namespace PrayerApp.Services;

public interface IBoxService
{
    /// <summary>Returns all boxes sorted by SortOrder then Name.</summary>
    Task<IReadOnlyList<CardBox>> GetBoxesAsync();

    /// <summary>Returns a system box by key ("system" or "archived"), or null if not found.</summary>
    Task<CardBox?> GetSystemBoxAsync(string systemKey);

    Task<CardBox> SaveBoxAsync(CardBox box);

    /// <summary>
    /// Deletes a box. If <paramref name="deleteCards"/> is false, cards are unassigned (moved to Unboxed).
    /// If true, all cards in the box and their prayer requests are cascade-deleted.
    /// </summary>
    Task DeleteBoxAsync(int boxId, bool deleteCards);

    /// <summary>Ensures System and Archived boxes exist. Called at app startup as a resilience fallback.</summary>
    Task SeedSystemBoxesAsync();

    void InvalidateCache();
}

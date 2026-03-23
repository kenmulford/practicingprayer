using PrayerApp.Models;

namespace PrayerApp.Services;

public interface IPrayerService
{
    Task<IReadOnlyList<Prayer>> GetPrayersByCardAsync(int prayerCardId);
    Task<IReadOnlyList<Prayer>> GetAllPrayersAsync();
    Task<IReadOnlyList<Prayer>> GetAllActivePrayersAsync();
    /// <summary>Active prayers prayed at least once but not in the past <paramref name="dayThreshold"/> days. Oldest first.</summary>
    Task<IReadOnlyList<Prayer>> GetOverduePrayersAsync(int dayThreshold = 30);
    /// <summary>Returns the most recent <see cref="PrayerInteraction.InteractionAt"/> across all rows, or null if none exist.</summary>
    Task<DateTime?> GetLastInteractionDateAsync();
    /// <summary>Count of active (not answered) prayers on a specific card.</summary>
    Task<int> GetActivePrayerCountByCardAsync(int cardId);
    Task<Prayer> SavePrayerAsync(Prayer prayer);
    Task DeletePrayerAsync(Prayer prayer);
    void InvalidateCache();
}

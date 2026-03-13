using PrayerApp.Models;

namespace PrayerApp.Services;

public interface IPrayerService
{
    Task<IReadOnlyList<Prayer>> GetPrayersByCardAsync(int prayerCardId);
    Task<IReadOnlyList<Prayer>> GetAllPrayersAsync();
    Task<IReadOnlyList<Prayer>> GetAllActivePrayersAsync();
    /// <summary>Active prayers not prayed in the past <paramref name="dayThreshold"/> days (or never). Most-neglected first.</summary>
    Task<IReadOnlyList<Prayer>> GetOverduePrayersAsync(int dayThreshold = 30);
    Task<Prayer> SavePrayerAsync(Prayer prayer);
    Task DeletePrayerAsync(Prayer prayer);
    void InvalidateCache();
}

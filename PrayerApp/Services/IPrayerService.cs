using PrayerApp.Models;

namespace PrayerApp.Services;

public interface IPrayerService
{
    Task<IReadOnlyList<Prayer>> GetPrayersByCardAsync(int prayerCardId);
    Task<IReadOnlyList<Prayer>> GetAllPrayersAsync();
    Task<Prayer> SavePrayerAsync(Prayer prayer);
    Task DeletePrayerAsync(Prayer prayer);
    void InvalidateCache();
}

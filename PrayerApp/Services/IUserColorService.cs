using PrayerApp.Models;

namespace PrayerApp.Services;

public interface IUserColorService
{
    Task<IReadOnlyList<UserColor>> GetColorsAsync();
    Task<UserColor> SaveColorAsync(string hexValue);
    Task DeleteColorAsync(int id);
    Task SeedDefaultsAsync();
}

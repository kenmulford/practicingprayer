using PrayerApp.Models;

namespace PrayerApp.Services;

public interface IUserColorService
{
    Task<IReadOnlyList<UserColor>> GetColorsAsync();
    Task<UserColor> SaveColorAsync(string hexValue);
    /// <summary>Deletes a custom color and reassigns any tags using it to the first default color.</summary>
    Task DeleteColorAsync(int id);
    Task SeedDefaultsAsync();
    /// <summary>Returns the hex value of the first default palette color.</summary>
    string GetFirstDefaultHex();
}

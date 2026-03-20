using PrayerApp.Helpers;
using PrayerApp.Models;

namespace PrayerApp.Services;

public class UserColorService : IUserColorService
{
    private readonly IDBService _dbService;

    public UserColorService(IDBService dbService)
    {
        _dbService = dbService ?? throw new ArgumentNullException(nameof(dbService));
    }

    public async Task<IReadOnlyList<UserColor>> GetColorsAsync()
    {
        var all = await _dbService.GetAllAsync<UserColor>();
        return all.OrderBy(c => c.CreatedAt).ToList().AsReadOnly();
    }

    public async Task<UserColor> SaveColorAsync(string hexValue)
    {
        // Normalize to uppercase
        hexValue = hexValue.ToUpperInvariant();

        // Avoid duplicates
        var existing = (await _dbService.GetAllAsync<UserColor>())
            .FirstOrDefault(c => string.Equals(c.HexValue, hexValue, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing;

        var color = new UserColor { HexValue = hexValue };
        await _dbService.InsertAsync(color);
        return color;
    }

    public async Task DeleteColorAsync(int id)
    {
        var color = await _dbService.GetByIdAsync<UserColor>(id);
        if (color is not null)
            await _dbService.DeleteAsync(color);
    }

    public async Task SeedDefaultsAsync()
    {
        var existing = await _dbService.GetAllAsync<UserColor>();
        if (existing.Count > 0) return; // Already seeded

        foreach (var (light, _, _) in TagColorPalette.Swatches)
        {
            await _dbService.InsertAsync(new UserColor { HexValue = light.ToUpperInvariant() });
        }
    }
}

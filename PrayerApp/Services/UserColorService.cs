using PrayerApp.Models;

namespace PrayerApp.Services;

public class UserColorService : IUserColorService
{
    private readonly IDBService _dbService;

    /// <summary>
    /// Default palette hex values (light-mode). Matches TagColorPalette.Swatches order.
    /// Kept here so UserColorService has no MAUI dependency and is unit-testable.
    /// </summary>
    internal static readonly string[] DefaultPaletteHexValues =
    {
        "#B84040", "#B35A20", "#7A4020", "#1E7870",
        "#2E5A9A", "#663C8C", "#8C3860", "#505050",
    };

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
        if (color is null || color.IsDefault) return; // protect defaults
        await _dbService.DeleteAsync(color);
    }

    public string GetFirstDefaultHex() => DefaultPaletteHexValues[0];

    public async Task SeedDefaultsAsync()
    {
        var existing = await _dbService.GetAllAsync<UserColor>();
        if (existing.Count > 0) return; // Already seeded

        foreach (var hex in DefaultPaletteHexValues)
        {
            await _dbService.InsertAsync(new UserColor { HexValue = hex, IsDefault = true });
        }
    }
}

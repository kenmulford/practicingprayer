using SQLite;

namespace PrayerApp.Models;

[Table("UserColor")]
public class UserColor
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string HexValue { get; set; } = string.Empty;

    /// <summary>True for the 8 seeded palette colors — protected from deletion.</summary>
    [Column("IsDefault")]
    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

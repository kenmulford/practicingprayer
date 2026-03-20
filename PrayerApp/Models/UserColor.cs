using SQLite;

namespace PrayerApp.Models;

[Table("UserColor")]
public class UserColor
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string HexValue { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

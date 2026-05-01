namespace PrayerApp.Models;

public sealed record ParseResult(
    IReadOnlyList<ParsedPrayer> Prayers,
    string SuggestedCardTitle);

public sealed record ParsedPrayer(string Title, string? Details);

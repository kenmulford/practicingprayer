namespace PrayerApp.Helpers;

/// <summary>
/// Pure-C# text helpers with no MAUI dependencies — safe to call from services,
/// view-models, and platform code.
/// </summary>
public static class TextNormalization
{
    /// <summary>Replace smart/curly quotes with ASCII equivalents so URLs and text stay clean.</summary>
    public static string? NormalizeQuotes(string? text)
    {
        if (text is null) return null;
        return text
            .Replace('‘', '\'')
            .Replace('’', '\'')
            .Replace('“', '"')
            .Replace('”', '"');
    }
}

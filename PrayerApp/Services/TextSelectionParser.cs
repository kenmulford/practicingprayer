using System.Text.RegularExpressions;
using PrayerApp.Helpers;
using PrayerApp.Models;

namespace PrayerApp.Services;

public class TextSelectionParser : ITextSelectionParser
{
    private static readonly Regex LineSplitter =
        new(@"\r\n|\r|\n", RegexOptions.Compiled);

    private static readonly Regex MarkerStripper =
        new(@"^\s*(?:\d+[\.\)]|[-*•])\s*", RegexOptions.Compiled);

    private static readonly Regex InternalWhitespace =
        new(@"\s+", RegexOptions.Compiled);

    private readonly Func<DateTime> _now;

    public TextSelectionParser() : this(() => DateTime.Now) { }

    public TextSelectionParser(Func<DateTime> now)
    {
        _now = now;
    }

    public ParseResult Parse(string rawText)
    {
        var suggestedTitle = $"Imported {_now():MMM d}";
        var normalized = TextNormalization.NormalizeQuotes(rawText) ?? string.Empty;
        var lines = LineSplitter.Split(normalized);

        var entries = new List<ParsedPrayer>(lines.Length);
        foreach (var line in lines)
        {
            var content = CollapseLine(MarkerStripper.Replace(line, string.Empty));
            if (content.Length == 0) continue;
            entries.Add(BuildPrayer(content));
        }

        if (entries.Count == 0 && !string.IsNullOrWhiteSpace(normalized))
        {
            entries.Add(BuildPrayer(CollapseLine(normalized)));
        }

        return new ParseResult(entries.AsReadOnly(), suggestedTitle);
    }

    private static string CollapseLine(string text) =>
        InternalWhitespace.Replace(text, " ").Trim();

    private static ParsedPrayer BuildPrayer(string text) =>
        text.Length <= Prayer.TitleMaxLength
            ? new ParsedPrayer(text, null)
            : new ParsedPrayer(text[..Prayer.TitleMaxLength], text[Prayer.TitleMaxLength..]);
}

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

    // Includes `-` per architecture doc rule 6, but plain hyphens appear in
    // compound words ("father-in-law") and dates ("2026-05-01") — those will
    // currently false-split. Tighten to ` - ` (space-flanked) if real imports
    // surface the issue.
    private static readonly char[] ClauseDelimiters = { ',', ';', ':', '—', '-' };

    // Rule 6: split on first clause delimiter, or take first 5 words if >6.
    // Rule 7 (100-char title cap) still applies on top.
    private static ParsedPrayer BuildPrayer(string text)
    {
        var delimiterIdx = text.IndexOfAny(ClauseDelimiters);
        if (delimiterIdx > 0)
            return new ParsedPrayer(CapTitle(text[..delimiterIdx].TrimEnd()), text);

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 6)
            return new ParsedPrayer(CapTitle(string.Join(' ', words.Take(5))), text);

        return text.Length <= Prayer.TitleMaxLength
            ? new ParsedPrayer(text, null)
            : new ParsedPrayer(CapTitle(text), text[Prayer.TitleMaxLength..]);
    }

    private static string CapTitle(string title) =>
        title.Length <= Prayer.TitleMaxLength ? title : title[..Prayer.TitleMaxLength];
}

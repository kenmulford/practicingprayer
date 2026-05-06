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
        var defaultTitle = $"Imported {_now():MMM d}";
        var normalized = TextNormalization.NormalizeQuotes(rawText) ?? string.Empty;
        var lines = LineSplitter.Split(normalized);

        var (headerTitle, linesToSkip) = ExtractHeaderTitle(lines);
        var suggestedTitle = headerTitle ?? defaultTitle;

        var entries = new List<ParsedPrayer>(lines.Length);
        for (int i = linesToSkip; i < lines.Length; i++)
        {
            var content = CollapseLine(MarkerStripper.Replace(lines[i], string.Empty));
            if (content.Length == 0) continue;
            entries.Add(BuildPrayer(content));
        }

        if (entries.Count == 0 && !string.IsNullOrWhiteSpace(normalized))
        {
            entries.Add(BuildPrayer(CollapseLine(normalized)));
        }

        return new ParseResult(entries.AsReadOnly(), suggestedTitle);
    }

    // Header-detection thresholds. Tuned for the corporate-prayer-document
    // shape ("CORPORATE PRAYER" / "MUSSONS OUTREACH" → "Pray for...");
    // adjust if real imports surface false positives or negatives.
    private const int MinHeaderLetters = 3;
    private const int MaxHeaderLength = 60;
    private const double UppercaseRatioThreshold = 0.8;

    /// <summary>
    /// Scans the leading lines for an ALL-CAPS topic header. Multiple
    /// consecutive headers collapse to the LAST one — when a corporate
    /// prayer document has both a section header and a topic header
    /// ("CORPORATE PRAYER" / "MUSSONS OUTREACH"), the topic line wins.
    /// A content line (non-blank, non-header) must follow the header
    /// candidate(s); without that signal an ALL-CAPS list
    /// ("MOM\nDAD\nSISTER") would lose its first item. Returns the
    /// title-cased header (or null) plus the count of leading lines to
    /// skip during prayer extraction.
    /// </summary>
    private static (string? Title, int LinesToSkip) ExtractHeaderTitle(string[] lines)
    {
        int headerEnd = -1;
        string? headerTrimmed = null;
        bool sawContent = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.Length == 0) continue;

            if (IsHeaderLine(trimmed))
            {
                headerEnd = i;
                headerTrimmed = trimmed;
            }
            else
            {
                sawContent = true;
                break;
            }
        }

        return headerEnd < 0 || !sawContent
            ? (null, 0)
            : (TitleCase(headerTrimmed!), headerEnd + 1);
    }

    private static bool IsHeaderLine(string trimmed)
    {
        if (trimmed.Length < MinHeaderLetters || trimmed.Length > MaxHeaderLength) return false;
        if (MarkerStripper.IsMatch(trimmed)) return false;

        int letters = 0, upper = 0;
        foreach (var c in trimmed)
        {
            if (char.IsLetter(c))
            {
                letters++;
                if (char.IsUpper(c)) upper++;
            }
        }
        return letters >= MinHeaderLetters
            && (double)upper / letters >= UppercaseRatioThreshold;
    }

    // Tokens of <= this length in an ALL-CAPS header are treated as
    // acronyms and preserved as-is (NPM, FBC, USA). 4+ overlaps with
    // common short words (BABY, GIFT) and would mis-preserve them.
    private const int MaxAcronymLength = 3;

    private static string TitleCase(string s)
    {
        var titled = System.Globalization.CultureInfo.InvariantCulture.TextInfo
            .ToTitleCase(s.ToLowerInvariant());

        var sourceTokens = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var titledTokens = titled.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (sourceTokens.Length != titledTokens.Length) return titled;

        for (int i = 0; i < sourceTokens.Length; i++)
        {
            if (sourceTokens[i].Length <= MaxAcronymLength && IsAllUpperLetters(sourceTokens[i]))
                titledTokens[i] = sourceTokens[i];
        }
        return string.Join(' ', titledTokens);
    }

    private static bool IsAllUpperLetters(string token)
    {
        if (token.Length == 0) return false;
        foreach (var c in token)
            if (!char.IsLetter(c) || !char.IsUpper(c)) return false;
        return true;
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

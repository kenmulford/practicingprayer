using PrayerApp.Services;

namespace PrayerApp.Tests.Services;

public class TextSelectionParserTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 1);
    private readonly TextSelectionParser _sut = new(() => FixedNow);

    // ── Numbered / bulleted marker stripping ──────────────────────────

    [Fact]
    public void Parse_NumberedList_StripsNumberMarkers()
    {
        var result = _sut.Parse("1. Mom\n2. Dad\n3. Sister");

        Assert.Equal(3, result.Prayers.Count);
        Assert.Equal("Mom", result.Prayers[0].Title);
        Assert.Equal("Dad", result.Prayers[1].Title);
        Assert.Equal("Sister", result.Prayers[2].Title);
    }

    [Fact]
    public void Parse_NumberedListWithCloseParen_StripsMarkers()
    {
        var result = _sut.Parse("1) Mom\n2) Dad");

        Assert.Equal(2, result.Prayers.Count);
        Assert.Equal("Mom", result.Prayers[0].Title);
    }

    [Fact]
    public void Parse_BulletedList_StripsBulletMarkers()
    {
        var result = _sut.Parse("- Mom\n- Dad\n* Sister\n• Brother");

        Assert.Equal(4, result.Prayers.Count);
        Assert.Equal("Mom", result.Prayers[0].Title);
        Assert.Equal("Sister", result.Prayers[2].Title);
        Assert.Equal("Brother", result.Prayers[3].Title);
    }

    [Fact]
    public void Parse_MixedMarkers_StripsBoth()
    {
        var result = _sut.Parse("1. Alpha\n- Beta\n2. Gamma");

        Assert.Equal(3, result.Prayers.Count);
        Assert.Equal("Alpha", result.Prayers[0].Title);
        Assert.Equal("Beta", result.Prayers[1].Title);
        Assert.Equal("Gamma", result.Prayers[2].Title);
    }

    // ── Line-ending normalization ──────────────────────────

    [Fact]
    public void Parse_CrLfLineEndings_SplitsCorrectly()
    {
        var result = _sut.Parse("1. Mom\r\n2. Dad");

        Assert.Equal(2, result.Prayers.Count);
        Assert.Equal("Mom", result.Prayers[0].Title);
        Assert.Equal("Dad", result.Prayers[1].Title);
    }

    [Fact]
    public void Parse_CrOnlyLineEndings_SplitsCorrectly()
    {
        // iOS Mail/Notes occasionally emit \r-only line endings on plain-text export.
        var result = _sut.Parse("1. Mom\r2. Dad");

        Assert.Equal(2, result.Prayers.Count);
        Assert.Equal("Mom", result.Prayers[0].Title);
        Assert.Equal("Dad", result.Prayers[1].Title);
    }

    // ── Quote / whitespace normalization ──────────────────────────

    [Fact]
    public void Parse_SmartQuotes_NormalizedToAscii()
    {
        var result = _sut.Parse("1. Mom’s surgery\n2. “Dad”");

        Assert.Equal("Mom's surgery", result.Prayers[0].Title);
        Assert.Equal("\"Dad\"", result.Prayers[1].Title);
    }

    [Fact]
    public void Parse_TrimsAndCollapsesWhitespace()
    {
        var result = _sut.Parse("1.   Mom    surgery   \n2.\tDad job");

        Assert.Equal("Mom surgery", result.Prayers[0].Title);
        Assert.Equal("Dad job", result.Prayers[1].Title);
    }

    // ── Fallback to single paragraph ──────────────────────────

    [Fact]
    public void Parse_NoMarkers_LongLine_SynthesizesTitle()
    {
        // Single-paragraph fallback meets rule 6: 9 words, no delimiter →
        // first 5 words become the title, full line is the details.
        var input = "Please pray for my family during this difficult time.";

        var result = _sut.Parse(input);

        Assert.Single(result.Prayers);
        Assert.Equal("Please pray for my family", result.Prayers[0].Title);
        Assert.Equal(input, result.Prayers[0].Details);
    }

    [Fact]
    public void Parse_OnlyMarkersNoContent_FallsBackToSingleParagraph()
    {
        // After stripping markers, no entries survive — input becomes the title.
        var result = _sut.Parse("1.\n2.\n3.");

        Assert.Single(result.Prayers);
    }

    // ── Title cap at 100 chars ──────────────────────────

    [Fact]
    public void Parse_TitleOver100Chars_OverflowsIntoDetails()
    {
        var longBody = new string('a', 150);

        var result = _sut.Parse("1. " + longBody);

        Assert.Single(result.Prayers);
        Assert.Equal(100, result.Prayers[0].Title.Length);
        Assert.Equal(50, result.Prayers[0].Details!.Length);
    }

    [Fact]
    public void Parse_TitleExactly100Chars_NoOverflow()
    {
        var body = new string('a', 100);

        var result = _sut.Parse("1. " + body);

        Assert.Equal(100, result.Prayers[0].Title.Length);
        Assert.Null(result.Prayers[0].Details);
    }

    // ── SuggestedCardTitle ──────────────────────────

    [Fact]
    public void Parse_SuggestedCardTitle_FormatsAsImportedMonthDay()
    {
        var result = _sut.Parse("1. Mom");

        Assert.Equal("Imported May 1", result.SuggestedCardTitle);
    }

    // ── Rule 6: long-line title synthesis (2026-05-01) ──────────────────────────
    // Per architecture doc 02-architecture.md rule 6: lines with a clause delimiter
    // (, ; : — -) OR more than 6 space-separated words push the full line into
    // Details and synthesize a Title — first part before the delimiter, or first
    // 5 words when no delimiter is present. Rule 7 (100-char title cap) still
    // applies on top.

    [Fact]
    public void Parse_ShortLineNoDelimiter_KeepsFullLineAsTitle()
    {
        // 6 words is the boundary — rule 6 fires only on >6.
        var result = _sut.Parse("1. Pray for Mom's surgery on Friday");

        Assert.Single(result.Prayers);
        Assert.Equal("Pray for Mom's surgery on Friday", result.Prayers[0].Title);
        Assert.Null(result.Prayers[0].Details);
    }

    [Fact]
    public void Parse_LongLineNoDelimiter_TakesFirstFiveWordsAsTitle()
    {
        var result = _sut.Parse("1. Pray for Mom's surgery on Friday this week");

        Assert.Single(result.Prayers);
        Assert.Equal("Pray for Mom's surgery on", result.Prayers[0].Title);
        Assert.Equal("Pray for Mom's surgery on Friday this week", result.Prayers[0].Details);
    }

    [Fact]
    public void Parse_LineWithComma_SplitsAtComma()
    {
        var result = _sut.Parse("1. Sis is graduating from college this weekend, please pray");

        Assert.Single(result.Prayers);
        Assert.Equal("Sis is graduating from college this weekend", result.Prayers[0].Title);
        Assert.Equal("Sis is graduating from college this weekend, please pray", result.Prayers[0].Details);
    }

    [Fact]
    public void Parse_LineWithColon_SplitsAtColon()
    {
        var result = _sut.Parse("1. Aunt Susan: hip replacement next month");

        Assert.Single(result.Prayers);
        Assert.Equal("Aunt Susan", result.Prayers[0].Title);
        Assert.Equal("Aunt Susan: hip replacement next month", result.Prayers[0].Details);
    }

    [Fact]
    public void Parse_LineWithEmDash_SplitsAtEmDash()
    {
        var result = _sut.Parse("1. Mom — surgery on Friday");

        Assert.Single(result.Prayers);
        Assert.Equal("Mom", result.Prayers[0].Title);
        Assert.Equal("Mom — surgery on Friday", result.Prayers[0].Details);
    }

    [Fact]
    public void Parse_VeryShortLine_KeepsAsTitle()
    {
        var result = _sut.Parse("1. Quick prayer");

        Assert.Single(result.Prayers);
        Assert.Equal("Quick prayer", result.Prayers[0].Title);
        Assert.Null(result.Prayers[0].Details);
    }
}

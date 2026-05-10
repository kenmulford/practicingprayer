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

    // ── ALL-CAPS header detection ──────────────────────────
    // Item 4 (next-punch-list): when the imported text starts with an
    // ALL-CAPS topic line followed by content, the parser uses that line
    // as the suggested card title (title-cased) and excludes it from the
    // prayer list. Multiple consecutive headers at the top collapse to
    // the LAST one (the most specific topic — "MUSSONS OUTREACH" beats
    // "CORPORATE PRAYER").

    [Fact]
    public void Parse_AllCapsHeaderFollowedByContent_BecomesCardTitleAndIsExcluded()
    {
        var input = "MISSIONS OUTREACH\nPray for the missionaries.";

        var result = _sut.Parse(input);

        Assert.Equal("Missions Outreach", result.SuggestedCardTitle);
        Assert.Single(result.Prayers);
        Assert.Equal("Pray for the missionaries.", result.Prayers[0].Title);
    }

    [Fact]
    public void Parse_TwoConsecutiveAllCapsHeaders_UsesLastAndExcludesBoth()
    {
        var input = "CORPORATE PRAYER\nMISSIONS OUTREACH\n  Pray for our Campus Outreach staff.";

        var result = _sut.Parse(input);

        Assert.Equal("Missions Outreach", result.SuggestedCardTitle);
        Assert.Single(result.Prayers);
        Assert.StartsWith("Pray for our Campus", result.Prayers[0].Title);
    }

    [Fact]
    public void Parse_NoHeader_FallsBackToImportedMonthDay()
    {
        // Mixed-case from line 1 means no header.
        var input = "Mom needs prayer.\nDad's surgery on Friday.";

        var result = _sut.Parse(input);

        Assert.Equal("Imported May 1", result.SuggestedCardTitle);
        Assert.Equal(2, result.Prayers.Count);
    }

    [Fact]
    public void Parse_AllCapsListWithNoFollowUpContent_DoesNotTreatAsHeader()
    {
        // "MOM\nDAD\nSISTER" is a flat ALL-CAPS list, not a header + content.
        // Without a content line below, none of these get consumed as a title.
        var input = "MOM\nDAD\nSISTER";

        var result = _sut.Parse(input);

        Assert.Equal("Imported May 1", result.SuggestedCardTitle);
        Assert.Equal(3, result.Prayers.Count);
        Assert.Equal("MOM", result.Prayers[0].Title);
        Assert.Equal("DAD", result.Prayers[1].Title);
        Assert.Equal("SISTER", result.Prayers[2].Title);
    }

    [Fact]
    public void Parse_HeaderWithListMarker_DoesNotTreatAsHeader()
    {
        // "1. MISSIONS OUTREACH" is a marker-prefixed list item, not a header.
        var input = "1. MISSIONS OUTREACH\nPray for staff.";

        var result = _sut.Parse(input);

        Assert.Equal("Imported May 1", result.SuggestedCardTitle);
        Assert.Equal(2, result.Prayers.Count);
        Assert.Equal("MISSIONS OUTREACH", result.Prayers[0].Title);
    }

    [Fact]
    public void Parse_VeryShortAllCapsLine_DoesNotTreatAsHeader()
    {
        // "OK" is too short to be a meaningful header (< 3 letters).
        var input = "OK\nPray for us.";

        var result = _sut.Parse(input);

        Assert.Equal("Imported May 1", result.SuggestedCardTitle);
        Assert.Equal(2, result.Prayers.Count);
    }

    [Fact]
    public void Parse_LongAllCapsLine_DoesNotTreatAsHeader()
    {
        // > 60 chars is sentence-shaped, not header-shaped.
        var input = "PLEASE PRAY FOR ALL OUR MISSIONARIES SERVING IN MANY DIFFERENT COUNTRIES THIS WEEK\nDetails follow.";

        var result = _sut.Parse(input);

        Assert.Equal("Imported May 1", result.SuggestedCardTitle);
        Assert.Equal(2, result.Prayers.Count);
    }

    [Fact]
    public void Parse_HeaderWithBlankLineBeforeContent_StillDetects()
    {
        var input = "MISSIONS OUTREACH\n\nPray for staff.";

        var result = _sut.Parse(input);

        Assert.Equal("Missions Outreach", result.SuggestedCardTitle);
        Assert.Single(result.Prayers);
        Assert.Equal("Pray for staff.", result.Prayers[0].Title);
    }

    [Fact]
    public void Parse_HeaderWithEmbeddedDigits_StillDetects()
    {
        // A few non-letter chars don't disqualify a header — the threshold
        // is on letter case ratio, not on character class purity.
        var input = "WEEK 2 PRAYERS\nPray for the team.";

        var result = _sut.Parse(input);

        Assert.Equal("Week 2 Prayers", result.SuggestedCardTitle);
        Assert.Single(result.Prayers);
    }

    [Fact]
    public void Parse_OnlyHeaderNoContent_FallsBackAndKeepsLineAsPrayer()
    {
        // Single line that looks like a header but has nothing below.
        var input = "MISSIONS OUTREACH";

        var result = _sut.Parse(input);

        Assert.Equal("Imported May 1", result.SuggestedCardTitle);
        Assert.Single(result.Prayers);
        Assert.Equal("MISSIONS OUTREACH", result.Prayers[0].Title);
    }

    [Fact]
    public void Parse_HeaderWithAcronym_PreservesShortUppercaseTokens()
    {
        // Tokens of <=3 chars in an ALL-CAPS header are almost always
        // acronyms (NPM, FBC, USA), not short words. Title-casing them
        // would produce "Npm Prayer Night" — wrong for any real document.
        var input = "NPM PRAYER NIGHT\nPray for the team.";

        var result = _sut.Parse(input);

        Assert.Equal("NPM Prayer Night", result.SuggestedCardTitle);
    }

    [Fact]
    public void Parse_HeaderWithMultipleAcronyms_PreservesAll()
    {
        var input = "FBC NPM RETREAT\nDetails below.";

        var result = _sut.Parse(input);

        Assert.Equal("FBC NPM Retreat", result.SuggestedCardTitle);
    }

    [Fact]
    public void Parse_HeaderWithFourCharToken_TitleCasesIt()
    {
        // 4-char tokens are usually full words ("BABY", "GIFT"), not
        // acronyms — the threshold is 3. Users with longer acronyms
        // (USPS, NASA) will see them title-cased and can edit.
        var input = "BABY DEDICATION\nPray for the family.";

        var result = _sut.Parse(input);

        Assert.Equal("Baby Dedication", result.SuggestedCardTitle);
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

    // ── Blank-line-separated blocks (2026-05-10, option d) ──────────────────────────
    // When the imported text uses blank lines to separate prayer blocks, each block
    // is treated as a single prayer if line 2 is "sentence-shaped" (has a clause
    // delimiter or > 6 space-separated words after marker stripping). Otherwise, the
    // block falls back to per-line behavior — preserving the ALL-CAPS list shape
    // (MOM/DAD/SISTER) while folding name + details blocks (Frank / wife is due...).

    [Fact]
    public void Parse_BlankLineSeparatedNameAndDetails_ProducesOnePrayerPerBlock()
    {
        var input =
            "Jim\nLooking for a new job, praying for interviews this week.\n\n" +
            "Frank\nWife is due this week with their third child.\n\n" +
            "John\nRecovering from surgery, please pray for healing.";

        var result = _sut.Parse(input);

        Assert.Equal(3, result.Prayers.Count);
        Assert.Equal("Jim", result.Prayers[0].Title);
        Assert.Equal("Looking for a new job, praying for interviews this week.", result.Prayers[0].Details);
        Assert.Equal("Frank", result.Prayers[1].Title);
        Assert.Equal("Wife is due this week with their third child.", result.Prayers[1].Details);
        Assert.Equal("John", result.Prayers[2].Title);
        Assert.Equal("Recovering from surgery, please pray for healing.", result.Prayers[2].Details);
    }

    [Fact]
    public void Parse_BlankLineBlocks_PreservesParagraphBreaksInDetails()
    {
        // 3-line block, line 2 is sentence-shaped (>6 words) → fold whole block.
        // Lines 2 and 3 join with \n so the paragraph break survives in Details.
        var input = "Frank\nWife is due this week with their third child.\nThey are nervous and tired.";

        var result = _sut.Parse(input);

        Assert.Single(result.Prayers);
        Assert.Equal("Frank", result.Prayers[0].Title);
        Assert.Equal(
            "Wife is due this week with their third child.\nThey are nervous and tired.",
            result.Prayers[0].Details);
    }

    [Fact]
    public void Parse_AllCapsList_NoBlankLines_StillProducesOnePerLine()
    {
        // Regression: existing test (lines 202–215) — single block of three short lines,
        // line 2 = "DAD" (1 word, no delimiter) → NOT sentence-shaped → per-line fallback.
        var input = "MOM\nDAD\nSISTER";

        var result = _sut.Parse(input);

        Assert.Equal(3, result.Prayers.Count);
        Assert.Equal("MOM", result.Prayers[0].Title);
        Assert.Equal("DAD", result.Prayers[1].Title);
        Assert.Equal("SISTER", result.Prayers[2].Title);
    }

    [Fact]
    public void Parse_TwoLineBlock_ShortSecondLine_DoesNotFold()
    {
        // 2-line block, line 2 = "Dad" (1 word, no delimiter) → NOT sentence-shaped.
        // Should split per-line, not fold into "Mom" + details="Dad".
        var input = "Mom\nDad";

        var result = _sut.Parse(input);

        Assert.Equal(2, result.Prayers.Count);
        Assert.Equal("Mom", result.Prayers[0].Title);
        Assert.Equal("Dad", result.Prayers[1].Title);
    }

    [Fact]
    public void Parse_TwoLineBlock_LongSecondLine_Folds()
    {
        // Line 2 has 9 words, no delimiter — > 6 word threshold → sentence-shaped → fold.
        var input = "Frank\nWife is due this week with their third child.";

        var result = _sut.Parse(input);

        Assert.Single(result.Prayers);
        Assert.Equal("Frank", result.Prayers[0].Title);
        Assert.Equal("Wife is due this week with their third child.", result.Prayers[0].Details);
    }

    [Fact]
    public void Parse_TwoLineBlock_SecondLineWithClauseDelimiter_Folds()
    {
        // Line 2 has only 5 words but contains a comma → sentence-shaped → fold.
        var input = "Frank\nWife is due, third child";

        var result = _sut.Parse(input);

        Assert.Single(result.Prayers);
        Assert.Equal("Frank", result.Prayers[0].Title);
        Assert.Equal("Wife is due, third child", result.Prayers[0].Details);
    }

    [Fact]
    public void Parse_AllCapsHeaderInFirstBlock_StillExtractedToSuggestedCardTitle()
    {
        // Regression: header extraction must still run on the first block, even when
        // the input uses blank-line block separation. "MISSIONS OUTREACH" → card title;
        // remainder of first block ("Pray for staff.") is the only prayer.
        var input = "MISSIONS OUTREACH\n\nPray for staff.";

        var result = _sut.Parse(input);

        Assert.Equal("Missions Outreach", result.SuggestedCardTitle);
        Assert.Single(result.Prayers);
        Assert.Equal("Pray for staff.", result.Prayers[0].Title);
    }

    [Fact]
    public void Parse_TrailingBlankLines_Ignored()
    {
        var input = "Jim\nDetails about Jim's situation, lots of words here.\n\n\n";

        var result = _sut.Parse(input);

        Assert.Single(result.Prayers);
        Assert.Equal("Jim", result.Prayers[0].Title);
        Assert.Equal("Details about Jim's situation, lots of words here.", result.Prayers[0].Details);
    }

    [Fact]
    public void Parse_LeadingBlankLines_Ignored()
    {
        var input = "\n\nJim\nDetails about Jim's situation, lots of words here.";

        var result = _sut.Parse(input);

        Assert.Single(result.Prayers);
        Assert.Equal("Jim", result.Prayers[0].Title);
        Assert.Equal("Details about Jim's situation, lots of words here.", result.Prayers[0].Details);
    }
}

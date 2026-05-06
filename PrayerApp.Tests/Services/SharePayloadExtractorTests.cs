using PrayerApp.Shared;
using Xunit;

namespace PrayerApp.Tests.Services;

/// <summary>
/// Unit tests for the share-extension payload classifier. The iOS share
/// extension narrows NSItemProvider.LoadItem's NSObject result into one of
/// {NSString, NSAttributedString, NSData} on the iOS side and passes the
/// extracted strings/bytes to <see cref="SharePayloadExtractor.Classify"/>.
/// This split keeps Foundation out of the net10.0 test target while still
/// covering the bug class that landed in build 95: rich-text sources (Notes,
/// Pages, Word, Mail) deliver the selection as NSAttributedString, the prior
/// implementation only handled NSString, and the share modal silently
/// dismissed without a breadcrumb.
/// </summary>
public class SharePayloadExtractorTests
{
    [Fact]
    public void Classify_PlainString_ReturnsTextAndNoOutcome()
    {
        var (text, outcome) = SharePayloadExtractor.Classify(
            plainString: "hello",
            attributedString: null,
            rawBytes: null);

        Assert.Equal("hello", text);
        Assert.Null(outcome);
    }

    [Fact]
    public void Classify_AttributedString_ReturnsTextAndNoOutcome()
    {
        // Apple Notes / Pages / Word / Mail deliver this shape.
        var (text, outcome) = SharePayloadExtractor.Classify(
            plainString: null,
            attributedString: "from Notes",
            rawBytes: null);

        Assert.Equal("from Notes", text);
        Assert.Null(outcome);
    }

    [Fact]
    public void Classify_RawBytes_DecodesUtf8()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("café — 中文");
        var (text, outcome) = SharePayloadExtractor.Classify(
            plainString: null,
            attributedString: null,
            rawBytes: bytes);

        Assert.Equal("café — 中文", text);
        Assert.Null(outcome);
    }

    [Fact]
    public void Classify_AllNull_ReturnsUnsupportedType()
    {
        // Source returned NSDictionary, NSURL, or some other unsupported type.
        var (text, outcome) = SharePayloadExtractor.Classify(
            plainString: null,
            attributedString: null,
            rawBytes: null);

        Assert.Null(text);
        Assert.Equal(BreadcrumbOutcome.UnsupportedType, outcome);
    }

    [Fact]
    public void Classify_EmptyPlainString_ReturnsEmptyText()
    {
        var (text, outcome) = SharePayloadExtractor.Classify(
            plainString: "",
            attributedString: null,
            rawBytes: null);

        Assert.Null(text);
        Assert.Equal(BreadcrumbOutcome.EmptyText, outcome);
    }

    [Fact]
    public void Classify_WhitespaceOnly_ReturnsEmptyText()
    {
        var (text, outcome) = SharePayloadExtractor.Classify(
            plainString: "   \n\t",
            attributedString: null,
            rawBytes: null);

        Assert.Null(text);
        Assert.Equal(BreadcrumbOutcome.EmptyText, outcome);
    }

    [Fact]
    public void Classify_PlainTakesPrecedenceOverAttributed()
    {
        // Defensive: shouldn't happen in production (caller picks one shape)
        // but the precedence order documents intent.
        var (text, _) = SharePayloadExtractor.Classify(
            plainString: "plain wins",
            attributedString: "attributed loses",
            rawBytes: null);

        Assert.Equal("plain wins", text);
    }

    [Fact]
    public void Classify_AttributedTakesPrecedenceOverRawBytes()
    {
        var (text, _) = SharePayloadExtractor.Classify(
            plainString: null,
            attributedString: "attributed wins",
            rawBytes: System.Text.Encoding.UTF8.GetBytes("bytes lose"));

        Assert.Equal("attributed wins", text);
    }

    [Fact]
    public void Classify_EmptyByteArray_ReturnsEmptyText()
    {
        var (text, outcome) = SharePayloadExtractor.Classify(
            plainString: null,
            attributedString: null,
            rawBytes: Array.Empty<byte>());

        Assert.Null(text);
        Assert.Equal(BreadcrumbOutcome.EmptyText, outcome);
    }
}

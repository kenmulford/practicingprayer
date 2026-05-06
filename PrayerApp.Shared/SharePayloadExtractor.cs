namespace PrayerApp.Shared;

/// <summary>
/// Classifies a share-extension payload after the iOS-side type narrowing.
///
/// The iOS share extension's <c>NSItemProvider.LoadItem</c> may hand back
/// any of <c>NSString</c>, <c>NSAttributedString</c>, or <c>NSData</c>
/// (raw UTF-8) when asked for <c>public.plain-text</c>, depending on the
/// source app: typed-into-Notes returns <c>NSAttributedString</c>, Safari
/// page-text-selection commonly returns <c>NSString</c>, some PDF/Files
/// share paths return <c>NSData</c>. iOS does not auto-coerce between
/// these — the prior implementation cast only to <c>NSString</c> and
/// silently dismissed the share modal whenever a rich-text source
/// delivered <c>NSAttributedString</c> (build-95 user report).
///
/// The iOS-side caller (<c>ShareViewController</c>) inspects <c>loaded</c>,
/// produces ONE of the three string/byte shapes, passes them here, and
/// receives back either the extracted text (for <see cref="WriteToAppGroup"/>)
/// or a <see cref="BreadcrumbOutcome"/> describing the upstream failure
/// (for forensic logging — without this, "shared but nothing happened"
/// reports leave zero diagnostic surface).
///
/// Pure-managed by design — keeps Foundation out of the unit-test target.
/// </summary>
public static class SharePayloadExtractor
{
    /// <summary>
    /// Returns <c>(text, null)</c> on success or <c>(null, outcome)</c>
    /// when the payload couldn't be turned into usable plain text.
    /// Precedence when multiple shapes are non-null:
    /// <c>plainString</c> &gt; <c>attributedString</c> &gt; <c>rawBytes</c>.
    /// All-null inputs report <see cref="BreadcrumbOutcome.UnsupportedType"/>;
    /// extracted-but-empty/whitespace text reports
    /// <see cref="BreadcrumbOutcome.EmptyText"/>.
    /// </summary>
    public static (string? Text, BreadcrumbOutcome? Outcome) Classify(
        string? plainString,
        string? attributedString,
        byte[]? rawBytes)
    {
        if (plainString is null && attributedString is null && rawBytes is null)
            return (null, BreadcrumbOutcome.UnsupportedType);

        var text = plainString
            ?? attributedString
            ?? (rawBytes is not null ? System.Text.Encoding.UTF8.GetString(rawBytes) : null);

        if (string.IsNullOrWhiteSpace(text))
            return (null, BreadcrumbOutcome.EmptyText);

        return (text, null);
    }
}

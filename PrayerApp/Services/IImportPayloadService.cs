using PrayerApp.Models;

namespace PrayerApp.Services;

/// <summary>
/// In-memory hand-off slot(s) from a platform-side import dispatcher to the
/// modal page that consumes the inbound import. Two independent one-shot
/// channels — raw text (share-sheet / context-menu, parsed via
/// <see cref="ITextSelectionParser"/>) and structured (deep-link /
/// .prayercard, already-parsed <see cref="ParseResult"/> bypassing the
/// parser). Channels are independent: writing one does not clobber the
/// other; reading one does not drain the other.
/// </summary>
public interface IImportPayloadService
{
    void StagePayload(string raw);
    string? ConsumePayload();

    void StageStructured(ParseResult data);
    ParseResult? ConsumeStructured();
}

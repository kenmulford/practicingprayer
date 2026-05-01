namespace PrayerApp.Services;

/// <summary>
/// In-memory hand-off slot from a platform-side import dispatcher (Android intent,
/// iOS App Group reader) to the modal page that consumes the raw selection.
/// One-shot: <see cref="ConsumePayload"/> returns and clears.
/// </summary>
public interface IImportPayloadService
{
    void StagePayload(string raw);
    string? ConsumePayload();
}

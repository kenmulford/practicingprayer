using PrayerApp.Models;

namespace PrayerApp.Services;

public class ImportPayloadService : IImportPayloadService
{
    private readonly object _gate = new();
    private string? _pending;
    private ParseResult? _pendingStructured;

    public void StagePayload(string raw)
    {
        lock (_gate) _pending = raw;
    }

    public string? ConsumePayload() => Drain(ref _pending);

    public void StageStructured(ParseResult data)
    {
        lock (_gate) _pendingStructured = data;
    }

    public ParseResult? ConsumeStructured() => Drain(ref _pendingStructured);

    private T? Drain<T>(ref T? slot) where T : class
    {
        lock (_gate)
        {
            var value = slot;
            slot = null;
            return value;
        }
    }
}

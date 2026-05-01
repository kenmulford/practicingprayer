namespace PrayerApp.Services;

public class ImportPayloadService : IImportPayloadService
{
    private readonly object _gate = new();
    private string? _pending;

    public void StagePayload(string raw)
    {
        lock (_gate) _pending = raw;
    }

    public string? ConsumePayload()
    {
        lock (_gate)
        {
            var value = _pending;
            _pending = null;
            return value;
        }
    }
}

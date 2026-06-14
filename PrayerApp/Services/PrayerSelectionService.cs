namespace PrayerApp.Services;

/// <summary>
/// In-memory implementation of <see cref="IPrayerSelectionService"/>. The held
/// set is replaced wholesale on each <see cref="Set"/> and cleared on
/// <see cref="Consume"/>. Registered as a singleton in <c>MauiProgram</c>.
/// </summary>
public sealed class PrayerSelectionService : IPrayerSelectionService
{
    private IReadOnlyList<int> _selection = Array.Empty<int>();

    public void Set(IEnumerable<int> prayerIds)
        => _selection = prayerIds?.ToList() ?? new List<int>();

    public IReadOnlyList<int> Consume()
    {
        var current = _selection;
        _selection = Array.Empty<int>();
        return current;
    }
}

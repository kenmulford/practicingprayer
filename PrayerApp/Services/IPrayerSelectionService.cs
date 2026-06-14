namespace PrayerApp.Services;

/// <summary>
/// Single-use, single-instance hand-off for a set of prayer request IDs across one
/// navigation hop. Producers (the "Choose cards" modal, the per-card "Pray" chip)
/// call <see cref="Set"/>; the Prayer Time <c>scope=selection</c> branch calls
/// <see cref="Consume"/> exactly once to read and clear the held set.
///
/// Registered as a singleton so the same instance bridges Set→navigate→Consume; the
/// held set itself is single-use — <see cref="Consume"/> clears it on read so a stale
/// set is never re-applied on a later, unrelated navigation. Issue #5 will be a second
/// consumer of this same service.
/// </summary>
public interface IPrayerSelectionService
{
    /// <summary>Stores the active prayer ID set for the next consumer.</summary>
    void Set(IEnumerable<int> prayerIds);

    /// <summary>
    /// Returns the stored set and clears it. A second consecutive call returns
    /// an empty list — the set is single-use by design.
    /// </summary>
    IReadOnlyList<int> Consume();
}

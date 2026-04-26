namespace PrayerApp.Helpers;

/// <summary>
/// Coalesces a burst of concurrent async work into one in-flight execution plus
/// at most one follow-up that captures triggers fired during the in-flight read.
/// All callers await the same Task — coalesced ones don't return early, they
/// observe the actual completion of the work, which keeps wrapper-level state
/// (e.g. an IsLoading overlay toggle) correctly scoped to the entire burst.
///
/// Single-threaded UI dispatcher contract: this type assumes calls are serialized
/// on the dispatcher (MAUI ViewModels, messenger broadcasts, OnAppearing). It is NOT
/// thread-safe for concurrent threads observing the bool fields.
/// </summary>
public sealed class SingleFlightGate
{
    private Task? _inFlight;
    private bool _pending;

    /// <summary>
    /// Runs <paramref name="work"/> with single-flight + coalesce-pending semantics:
    /// • If nothing is in-flight, starts <paramref name="work"/> and returns its Task.
    /// • If work is in-flight, flags pending and returns the in-flight Task — the
    ///   in-flight execution will run <paramref name="work"/> one more time before completing.
    /// • Multiple triggers during one in-flight execution coalesce into a single follow-up.
    /// • Pending is cleared in the finally block so an exception in <paramref name="work"/>
    ///   doesn't leave a stale pending flag for the next caller.
    /// </summary>
    public Task RunAsync(Func<Task> work)
    {
        if (_inFlight is { IsCompleted: false } existing)
        {
            _pending = true;
            return existing;
        }
        _inFlight = ExecuteAsync(work);
        return _inFlight;
    }

    private async Task ExecuteAsync(Func<Task> work)
    {
        try
        {
            do
            {
                _pending = false;
                await work();
            }
            while (_pending);
        }
        finally
        {
            _pending = false;
            _inFlight = null;
        }
    }
}

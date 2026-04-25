namespace PrayerApp.Helpers;

public static class DispatcherExtensions
{
    /// <summary>
    /// Yields the dispatcher twice so the Android UI thread queue drains an in-flight
    /// layout pass before the next operation. The first tick processes the immediate
    /// queue (e.g. a CollectionView ItemsSource swap or a Shell push animation step);
    /// the second covers any follow-on layout work that the first tick scheduled.
    /// Use before <c>CollectionView.ScrollTo</c> on a freshly-rebuilt grouped source,
    /// or before <c>Entry.Focus()</c> after a Shell push (BUG-70 family).
    /// </summary>
    public static async Task DrainLayoutPassAsync(this IDispatcher dispatcher)
    {
        await dispatcher.DispatchAsync(() => Task.CompletedTask);
        await dispatcher.DispatchAsync(() => Task.CompletedTask);
    }
}

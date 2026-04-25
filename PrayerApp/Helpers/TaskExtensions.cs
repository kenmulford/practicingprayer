using PrayerApp.Services;

namespace PrayerApp.Helpers;

public static class TaskExtensions
{
    /// <summary>
    /// Fire-and-forget with error logging. Exceptions are caught and written to
    /// <see cref="IDiagnosticLog"/> instead of being silently swallowed.
    /// </summary>
    public static async void SafeFireAndForget(this Task task, IDiagnosticLog? log = null)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            var diagnosticLog = log ?? Diagnostics.ResolveLog();
            diagnosticLog?.Log("SafeFireAndForget", ex);
        }
    }
}

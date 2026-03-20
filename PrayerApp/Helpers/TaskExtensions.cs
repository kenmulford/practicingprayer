using PrayerApp.Services;

namespace PrayerApp.Helpers;

public static class TaskExtensions
{
    /// <summary>
    /// Resolves <see cref="IDiagnosticLog"/> from the MAUI service provider.
    /// Null in non-MAUI contexts (unit tests). Override via the <c>log</c> parameter.
    /// </summary>
    private static IDiagnosticLog? ResolveDiagnosticLog()
    {
#if ANDROID || IOS
        return IPlatformApplication.Current?.Services?.GetService<IDiagnosticLog>();
#else
        return null;
#endif
    }

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
            var diagnosticLog = log ?? ResolveDiagnosticLog();
            diagnosticLog?.Log("SafeFireAndForget", ex);
        }
    }
}

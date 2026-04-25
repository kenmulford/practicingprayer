using PrayerApp.Services;

namespace PrayerApp.Helpers;

public static class Diagnostics
{
    /// <summary>
    /// Resolves <see cref="IDiagnosticLog"/> from the MAUI service provider.
    /// Null in non-MAUI contexts (unit tests, desktop test runner).
    /// Use in catch blocks where injecting <see cref="IDiagnosticLog"/> would be
    /// disproportionate (View code-behind, transient defensive logging).
    /// </summary>
    public static IDiagnosticLog? ResolveLog()
    {
#if ANDROID || IOS
        return IPlatformApplication.Current?.Services?.GetService<IDiagnosticLog>();
#else
        return null;
#endif
    }
}

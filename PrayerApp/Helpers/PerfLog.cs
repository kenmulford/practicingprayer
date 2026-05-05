using System.Diagnostics;

namespace PrayerApp.Helpers;

/// <summary>
/// Monotonic timestamped logger for systematic perf tracing across layer boundaries.
/// All entries share the same stopwatch (started at first access) so deltas are
/// directly comparable across VM / Page / Service callsites.
/// Filter Logcat with <c>adb logcat | grep PERF:</c> on Android.
/// On iOS, also routes through <see cref="PrayerApp.Services.IDiagnosticLog"/> so
/// entries land in <c>diagnostics.log</c> in the app's Documents directory —
/// reliable capture independent of syslog/network forwarding flakiness.
/// Diagnostic-only — remove call sites once a trace is captured.
/// </summary>
public static class PerfLog
{
    private static readonly Stopwatch _sw = Stopwatch.StartNew();

    public static void Log(string label)
    {
        var line = $"t={_sw.ElapsedMilliseconds}ms {label}";
        Debug.WriteLine($"PERF: {line}");
        Diagnostics.ResolveLog()?.Log("PERF", line);
    }
}

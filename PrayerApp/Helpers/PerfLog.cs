using System.Diagnostics;

namespace PrayerApp.Helpers;

/// <summary>
/// Monotonic timestamped logger for systematic perf tracing across layer boundaries.
/// All entries share the same stopwatch (started at first access) so deltas are
/// directly comparable across VM / Page / Service callsites.
/// Filter Logcat with <c>adb logcat | grep PERF:</c> on Android.
/// Diagnostic-only — remove call sites once a trace is captured.
/// </summary>
public static class PerfLog
{
    private static readonly Stopwatch _sw = Stopwatch.StartNew();

    public static void Log(string label)
        => Debug.WriteLine($"PERF: t={_sw.ElapsedMilliseconds}ms {label}");
}

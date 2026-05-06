using System.Globalization;

namespace PrayerApp.Shared;

public enum BreadcrumbOutcome
{
    Ok,
    Empty,
    ParseFail,
    IoFail,
    WriteOk,
    HostWakeOk,
    HostWakeFail,
    // Share-extension upstream-failure outcomes (added when build-95 fallout
    // exposed the silent-dismiss path on rich-text sources). Without these,
    // any failure before WriteToAppGroup leaves zero forensic surface.
    LoadItemError,
    NoAttachment,
    UnsupportedType,
    EmptyText,
    // Payload exceeded the 256 KB byte cap — either the NSData length pre-check
    // (multi-MB share) or the post-NFC byte-count check (rare; some Hangul
    // expand under FormC normalization).
    Oversized,
    // Catch-all for unexpected exceptions in the share-extension pipeline.
    // Breadcrumb token is ASCII-only by contract; the exception detail goes
    // to Debug.WriteLine and is not preserved here.
    PipelineError,
}

/// <summary>
/// Privacy-safe forensics log shared between the iOS Share Extension and
/// the main app.
///
/// Append uses BOTH a static lock AND FileMode.Append (O_APPEND on POSIX).
/// The lock guards same-process concurrent calls — empirically required:
/// without it, the concurrent-writers TDD test lost 1 line in 200 because
/// .NET's FileStream open+write+close cycle, despite passing FileMode.Append
/// to the kernel, can race when two threads in the same process interleave
/// their open() syscalls. O_APPEND covers cross-process atomicity (extension
/// process vs main-app process) where the lock cannot reach.
///
/// Truncation is main-app-only (single-writer phase) per the Slice 3c design.
///
/// Format: &lt;UTC ISO-8601 timestamp&gt; &lt;byte count or '-'&gt; &lt;outcome&gt;
/// One entry per line. Newest is LAST. ASCII only. No raw user text — ever.
/// </summary>
public static class AppGroupBreadcrumbLog
{
    private static readonly object _appendGate = new();

    public static void Append(string containerPath, BreadcrumbOutcome outcome, int byteCount)
    {
        try
        {
            var logPath = Path.Combine(containerPath, AppGroupConstants.LogFileName);
            var ts = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            var byteField = byteCount < 0 ? "-" : byteCount.ToString(CultureInfo.InvariantCulture);
            var line = $"{ts} {byteField} {OutcomeToken(outcome)}\n";
            var bytes = System.Text.Encoding.ASCII.GetBytes(line);
            lock (_appendGate)
            {
                using var stream = new FileStream(
                    logPath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite);
                stream.Write(bytes, 0, bytes.Length);
            }
        }
        catch
        {
            // Best-effort forensics — failing to log must never break the
            // import path. Silent on failure.
        }
    }

    public static void Truncate(string containerPath)
    {
        // Microsecond race window: between ReadAllLines and File.Move below,
        // a concurrent extension Append could land on the pre-Move inode and
        // be lost. Forensics-only impact, not blocking.
        try
        {
            var logPath = Path.Combine(containerPath, AppGroupConstants.LogFileName);
            if (!File.Exists(logPath)) return;

            var lines = File.ReadAllLines(logPath);
            if (lines.Length <= AppGroupConstants.MaxLogLines) return;

            var trimmed = lines[^AppGroupConstants.MaxLogLines..];
            var tmp = logPath + ".tmp";
            File.WriteAllLines(tmp, trimmed);
            File.Move(tmp, logPath, overwrite: true);
        }
        catch
        {
            // Truncation is best-effort. Failure leaves the log slightly
            // longer than MaxLogLines; next foreground retries.
        }
    }

    private static string OutcomeToken(BreadcrumbOutcome outcome) => outcome switch
    {
        BreadcrumbOutcome.Ok              => "ok",
        BreadcrumbOutcome.Empty           => "empty",
        BreadcrumbOutcome.ParseFail       => "parse-fail",
        BreadcrumbOutcome.IoFail          => "io-fail",
        BreadcrumbOutcome.WriteOk         => "write-ok",
        BreadcrumbOutcome.HostWakeOk      => "host-wake-ok",
        BreadcrumbOutcome.HostWakeFail    => "host-wake-fail",
        BreadcrumbOutcome.LoadItemError   => "load-item-error",
        BreadcrumbOutcome.NoAttachment    => "no-attachment",
        BreadcrumbOutcome.UnsupportedType => "unsupported-type",
        BreadcrumbOutcome.EmptyText       => "empty-text",
        BreadcrumbOutcome.Oversized       => "oversized",
        BreadcrumbOutcome.PipelineError   => "pipeline-error",
        _                                  => $"unknown-{(int)outcome}",
    };
}

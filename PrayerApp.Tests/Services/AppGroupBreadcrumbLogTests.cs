using PrayerApp.Shared;
using Xunit;

namespace PrayerApp.Tests.Services;

public class AppGroupBreadcrumbLogTests : IDisposable
{
    private readonly string _tempDir;

    public AppGroupBreadcrumbLogTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"breadcrumb-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private string LogPath => Path.Combine(_tempDir, AppGroupConstants.LogFileName);

    [Fact]
    public void Append_FirstEntry_CreatesFileWithSingleLine()
    {
        AppGroupBreadcrumbLog.Append(_tempDir, BreadcrumbOutcome.Ok, byteCount: 172);

        Assert.True(File.Exists(LogPath));
        var lines = File.ReadAllLines(LogPath);
        Assert.Single(lines);
        // Format: <ISO-8601 UTC> <byte-count or '-'> <outcome>
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z 172 ok$", lines[0]);
    }

    [Fact]
    public void Append_MultipleEntries_AppendsNewestLast()
    {
        AppGroupBreadcrumbLog.Append(_tempDir, BreadcrumbOutcome.WriteOk, byteCount: 100);
        Thread.Sleep(2);  // ensure distinct timestamps
        AppGroupBreadcrumbLog.Append(_tempDir, BreadcrumbOutcome.Ok, byteCount: 200);

        var lines = File.ReadAllLines(LogPath);
        Assert.Equal(2, lines.Length);
        Assert.Contains("write-ok", lines[0]);   // first written = first in file (newest LAST)
        Assert.Contains("ok", lines[1]);
    }

    [Fact]
    public void Append_IoFailOutcome_UsesDashForByteCount()
    {
        AppGroupBreadcrumbLog.Append(_tempDir, BreadcrumbOutcome.IoFail, byteCount: -1);

        var lines = File.ReadAllLines(LogPath);
        Assert.Matches(@"^\S+ - io-fail$", lines[0]);
    }

    [Theory]
    [InlineData(BreadcrumbOutcome.LoadItemError, "load-item-error")]
    [InlineData(BreadcrumbOutcome.NoAttachment, "no-attachment")]
    [InlineData(BreadcrumbOutcome.UnsupportedType, "unsupported-type")]
    [InlineData(BreadcrumbOutcome.EmptyText, "empty-text")]
    [InlineData(BreadcrumbOutcome.Oversized, "oversized")]
    [InlineData(BreadcrumbOutcome.PipelineError, "pipeline-error")]
    public void Append_ShareExtensionUpstreamOutcomes_WriteExpectedTokens(
        BreadcrumbOutcome outcome, string expectedToken)
    {
        // The share-extension upstream-failure breadcrumbs (added when build-95
        // fallout exposed the silent-dismiss path on rich-text sources) need
        // stable forensic tokens — log readers grep these strings.
        AppGroupBreadcrumbLog.Append(_tempDir, outcome, byteCount: -1);

        var lines = File.ReadAllLines(LogPath);
        Assert.Matches($@"^\S+ - {expectedToken}$", lines[0]);
    }

    [Fact]
    public async Task Append_ConcurrentWriters_NoLostUpdates()
    {
        // Validates POSIX O_APPEND atomicity claim. Two threads each write 100
        // entries; expect 200 lines total with no corruption.
        var tasks = new[]
        {
            Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                    AppGroupBreadcrumbLog.Append(_tempDir, BreadcrumbOutcome.WriteOk, i);
            }),
            Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                    AppGroupBreadcrumbLog.Append(_tempDir, BreadcrumbOutcome.Ok, i);
            }),
        };
        await Task.WhenAll(tasks);

        var lines = File.ReadAllLines(LogPath);
        Assert.Equal(200, lines.Length);
        Assert.All(lines, l => Assert.Matches(@"^\S+ \d+ (write-ok|ok)$", l));
    }

    [Fact]
    public void Truncate_FewerThanMaxLines_LeavesFileUnchanged()
    {
        for (int i = 0; i < 10; i++)
            AppGroupBreadcrumbLog.Append(_tempDir, BreadcrumbOutcome.Ok, i);

        AppGroupBreadcrumbLog.Truncate(_tempDir);

        var lines = File.ReadAllLines(LogPath);
        Assert.Equal(10, lines.Length);
    }

    [Fact]
    public void Truncate_OverMaxLines_KeepsTrailingMaxLinesInOrder()
    {
        for (int i = 0; i < 250; i++)
            AppGroupBreadcrumbLog.Append(_tempDir, BreadcrumbOutcome.Ok, i);

        AppGroupBreadcrumbLog.Truncate(_tempDir);

        var lines = File.ReadAllLines(LogPath);
        Assert.Equal(AppGroupConstants.MaxLogLines, lines.Length);
        // Trailing lines preserved -> last entry's byteCount == 249
        Assert.Contains(" 249 ok", lines[^1]);
        // First retained entry is byteCount == 50 (250 - 200)
        Assert.Contains(" 50 ok", lines[0]);
    }

    [Fact]
    public void Truncate_NoFile_DoesNothing()
    {
        // Should not throw if the log file doesn't exist yet
        AppGroupBreadcrumbLog.Truncate(_tempDir);
        Assert.False(File.Exists(LogPath));
    }
}

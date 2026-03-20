using PrayerApp.Services;

namespace PrayerApp.Tests.Services;

public class DiagnosticLogTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DiagnosticLog _log;

    public DiagnosticLogTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"DiagnosticLogTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _log = new DiagnosticLog(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Log_CreatesFileAndWritesEntry()
    {
        var ex = new InvalidOperationException("test error");

        _log.Log("TestCategory", ex);

        var content = File.ReadAllText(_log.GetLogPath());
        Assert.Contains("[TestCategory]", content);
        Assert.Contains("InvalidOperationException", content);
        Assert.Contains("test error", content);
        Assert.Contains("---", content);
    }

    [Fact]
    public void Log_AppendsMultipleEntries()
    {
        _log.Log("First", new Exception("one"));
        _log.Log("Second", new Exception("two"));

        var content = File.ReadAllText(_log.GetLogPath());
        Assert.Contains("[First]", content);
        Assert.Contains("[Second]", content);
    }

    [Fact]
    public void GetLogPath_ReturnsPathInProvidedDirectory()
    {
        Assert.StartsWith(_tempDir, _log.GetLogPath());
        Assert.EndsWith("diagnostics.log", _log.GetLogPath());
    }

    [Fact]
    public void Trim_DoesNothingWhenFileDoesNotExist()
    {
        // Should not throw
        _log.Trim();
    }

    [Fact]
    public void Trim_KeepsEntriesWhenUnderLimit()
    {
        for (int i = 0; i < 5; i++)
            _log.Log("Entry", new Exception($"error {i}"));

        _log.Trim();

        var content = File.ReadAllText(_log.GetLogPath());
        // All 5 entries should remain (under the 100 limit)
        var count = content.Split("---\n", StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.Equal(5, count);
    }

    [Fact]
    public void Trim_RemovesOldestEntriesBeyondLimit()
    {
        // Write 105 entries
        for (int i = 0; i < 105; i++)
            _log.Log("Entry", new Exception($"error {i}"));

        _log.Trim();

        var content = File.ReadAllText(_log.GetLogPath());
        var entries = content.Split("---\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(100, entries.Length);
        // Oldest entries (0-4) should be gone, newest (5-104) should remain
        Assert.DoesNotContain("error 0\n", content);
        Assert.DoesNotContain("error 4\n", content);
        Assert.Contains("error 5\n", content);
        Assert.Contains("error 104\n", content);
    }
}

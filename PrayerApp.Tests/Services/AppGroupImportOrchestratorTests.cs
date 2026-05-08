using System.Text.Json;
using Microsoft.Maui.Controls;
using NSubstitute;
using PrayerApp.Services;
using PrayerApp.Shared;
using Xunit;

namespace PrayerApp.Tests.Services;

public class AppGroupImportOrchestratorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IAppGroupContainerProvider _provider = Substitute.For<IAppGroupContainerProvider>();
    private readonly IImportPayloadService _payloadService = Substitute.For<IImportPayloadService>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly Page _fakePage = Substitute.For<Page>();

    public AppGroupImportOrchestratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"orchestrator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _provider.ResolveContainerPath().Returns(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private string PayloadPath => Path.Combine(_tempDir, AppGroupConstants.PayloadFileName);
    private string LogPath => Path.Combine(_tempDir, AppGroupConstants.LogFileName);

    private AppGroupImportOrchestrator NewOrchestrator() =>
        new(_provider, _payloadService, _navigation, () => _fakePage);

    private static string SerializePayload(string raw) =>
        JsonSerializer.Serialize(
            new ImportPayload(raw, DateTime.UtcNow.ToString("o")),
            ImportPayloadJsonContext.Default.ImportPayload);

    [Fact]
    public async Task NoFile_DoesNothing()
    {
        var orchestrator = NewOrchestrator();

        await orchestrator.CheckPendingAsync();

        _payloadService.DidNotReceiveWithAnyArgs().StagePayload(default!);
        await _navigation.DidNotReceiveWithAnyArgs().PushModalWithNavigationBarAsync(default!);
        Assert.False(File.Exists(LogPath));  // no breadcrumb when no file
    }

    [Fact]
    public async Task HappyPath_StagesPushesDeletes()
    {
        File.WriteAllText(PayloadPath, SerializePayload("Pray for Mom"));
        var orchestrator = NewOrchestrator();

        await orchestrator.CheckPendingAsync();

        _payloadService.Received(1).StagePayload("Pray for Mom");
        await _navigation.Received(1).PushModalWithNavigationBarAsync(_fakePage);
        Assert.False(File.Exists(PayloadPath));
        Assert.Contains(" ok", File.ReadAllText(LogPath));
    }

    [Fact]
    public async Task ParseFailure_DeletesFileAndLogs()
    {
        File.WriteAllText(PayloadPath, "{ this is not valid json");
        var orchestrator = NewOrchestrator();

        await orchestrator.CheckPendingAsync();

        _payloadService.DidNotReceiveWithAnyArgs().StagePayload(default!);
        await _navigation.DidNotReceiveWithAnyArgs().PushModalWithNavigationBarAsync(default!);
        Assert.False(File.Exists(PayloadPath));
        Assert.Contains("parse-fail", File.ReadAllText(LogPath));
    }

    [Fact]
    public async Task EmptyPayload_DeletesFileAndLogs()
    {
        File.WriteAllText(PayloadPath, SerializePayload(""));
        var orchestrator = NewOrchestrator();

        await orchestrator.CheckPendingAsync();

        _payloadService.DidNotReceiveWithAnyArgs().StagePayload(default!);
        await _navigation.DidNotReceiveWithAnyArgs().PushModalWithNavigationBarAsync(default!);
        Assert.False(File.Exists(PayloadPath));
        Assert.Contains("empty", File.ReadAllText(LogPath));
    }

    [Fact]
    public async Task WhitespacePayload_DeletesFileAndLogs()
    {
        File.WriteAllText(PayloadPath, SerializePayload("   \n  "));
        var orchestrator = NewOrchestrator();

        await orchestrator.CheckPendingAsync();

        _payloadService.DidNotReceiveWithAnyArgs().StagePayload(default!);
        Assert.False(File.Exists(PayloadPath));
        Assert.Contains("empty", File.ReadAllText(LogPath));
    }

    [Fact]
    public async Task ContainerNull_DoesNothing()
    {
        _provider.ResolveContainerPath().Returns((string?)null);
        var orchestrator = NewOrchestrator();

        await orchestrator.CheckPendingAsync();

        _payloadService.DidNotReceiveWithAnyArgs().StagePayload(default!);
        await _navigation.DidNotReceiveWithAnyArgs().PushModalWithNavigationBarAsync(default!);
    }

    [Fact]
    public async Task HappyPath_RunsTruncationBeforeReadAppendsAfter()
    {
        // Pre-seed an oversized log
        for (int i = 0; i < 250; i++)
        {
            AppGroupBreadcrumbLog.Append(_tempDir, BreadcrumbOutcome.Ok, i);
        }
        File.WriteAllText(PayloadPath, SerializePayload("test"));
        var orchestrator = NewOrchestrator();

        await orchestrator.CheckPendingAsync();

        var logLines = File.ReadAllLines(LogPath);
        // 250 pre-seed entries truncated to 200, then one new "ok" appended = 201.
        Assert.Equal(201, logLines.Length);
    }
}

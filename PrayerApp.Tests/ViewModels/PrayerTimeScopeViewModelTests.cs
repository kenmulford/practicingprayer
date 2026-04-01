using CommunityToolkit.Mvvm.Input;
using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class PrayerTimeScopeViewModelTests
{
    private readonly ITagService _tagService = Substitute.For<ITagService>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();

    /// <summary>
    /// PrayerTimeScopeViewModel calls LoadTagsAsync().SafeFireAndForget() in its constructor.
    /// Because GetTagsAsync() returns an already-completed Task (NSubstitute default), the
    /// fire-and-forget completes synchronously before the constructor returns.
    /// </summary>
    private PrayerTimeScopeViewModel CreateSut(IEnumerable<PrayerTag>? tags = null)
    {
        _tagService.GetTagsAsync().Returns(
            (tags ?? Enumerable.Empty<PrayerTag>()).ToList().AsReadOnly());
        return new PrayerTimeScopeViewModel(_tagService, _navigationService);
    }

    // ── Tag loading ───────────────────────────────────────────────────

    [Fact]
    public void Constructor_LoadsTags_FromService()
    {
        var sut = CreateSut(new[]
        {
            new PrayerTag { Id = 1, Name = "Faith" },
            new PrayerTag { Id = 2, Name = "Family" }
        });

        Assert.Equal(2, sut.Tags.Count);
        Assert.Contains(sut.Tags, t => t.Tag.Name == "Faith");
        Assert.Contains(sut.Tags, t => t.Tag.Name == "Family");
    }

    [Fact]
    public void Constructor_NoTags_ProducesEmptyCollection()
    {
        var sut = CreateSut();

        Assert.Empty(sut.Tags);
    }

    [Fact]
    public void Constructor_Tags_DefaultToNotSelected()
    {
        var sut = CreateSut(new[]
        {
            new PrayerTag { Id = 1, Name = "Work" }
        });

        Assert.All(sut.Tags, t => Assert.False(t.IsSelected));
    }

    // ── StartCommand ──────────────────────────────────────────────────

    [Fact]
    public async Task StartCommand_NoTagsSelected_ShowsAlert()
    {
        var sut = CreateSut(new[]
        {
            new PrayerTag { Id = 1, Name = "Faith" }
        });
        // Nothing selected

        await ((IAsyncRelayCommand)sut.StartCommand).ExecuteAsync(null);

        await _navigationService.Received(1)
            .DisplayAlertAsync("No Tags Selected", Arg.Any<string>(), "OK");
        await _navigationService.DidNotReceive().PopModalAsync();
    }

    [Fact]
    public async Task StartCommand_WithSelectedTags_PopsModalAndNavigates()
    {
        var sut = CreateSut(new[]
        {
            new PrayerTag { Id = 3, Name = "Faith" },
            new PrayerTag { Id = 7, Name = "Family" }
        });
        sut.Tags[0].IsSelected = true;
        sut.Tags[1].IsSelected = true;

        await ((IAsyncRelayCommand)sut.StartCommand).ExecuteAsync(null);

        await _navigationService.Received(1).PopModalAsync();
        await _navigationService.Received(1)
            .GoToAsync(Arg.Is<string>(s => s.Contains("scope=tags") && s.Contains("3") && s.Contains("7")));
    }

    [Fact]
    public async Task StartCommand_PartialSelection_NavigatesWithSelectedIdsOnly()
    {
        var sut = CreateSut(new[]
        {
            new PrayerTag { Id = 1, Name = "A" },
            new PrayerTag { Id = 2, Name = "B" }
        });
        sut.Tags[0].IsSelected = true;
        // Tags[1] not selected

        await ((IAsyncRelayCommand)sut.StartCommand).ExecuteAsync(null);

        await _navigationService.Received(1)
            .GoToAsync(Arg.Is<string>(s => s.Contains("tagIds=1") && !s.Contains("2")));
    }

    // ── CancelCommand ─────────────────────────────────────────────────

    [Fact]
    public async Task CancelCommand_PopsModal()
    {
        var sut = CreateSut();

        await ((IAsyncRelayCommand)sut.CancelCommand).ExecuteAsync(null);

        await _navigationService.Received(1).PopModalAsync();
    }
}

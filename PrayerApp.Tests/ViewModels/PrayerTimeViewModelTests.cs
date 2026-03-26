using CommunityToolkit.Mvvm.Input;
using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class PrayerTimeViewModelTests
{
    private readonly IPrayerService _prayerService = Substitute.For<IPrayerService>();
    private readonly ICardService _cardService = Substitute.For<ICardService>();
    private readonly ITagService _tagService = Substitute.For<ITagService>();
    private readonly IPrayerInteractionService _interactionService = Substitute.For<IPrayerInteractionService>();
    private readonly IOnboardingService _onboardingService = Substitute.For<IOnboardingService>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
    private readonly IAccessibilityService _accessibilityService = Substitute.For<IAccessibilityService>();
    private readonly ISettings _settings = Substitute.For<ISettings>();

    public PrayerTimeViewModelTests()
    {
        _settings.AutoModeIntervalSeconds.Returns(30);
    }

    private PrayerTimeViewModel CreateSut() =>
        new(_prayerService, _cardService, _tagService, _interactionService,
            _onboardingService, _navigationService, _accessibilityService, _settings);

    // ── Construction ──────────────────────────────────────────────────

    [Fact]
    public void Constructor_DefaultState()
    {
        var sut = CreateSut();

        Assert.True(sut.IsLoading);
        Assert.Empty(sut.Entries);
        Assert.Equal(30, sut.SelectedIntervalSeconds);
        Assert.False(sut.IsAutoMode);
        Assert.False(sut.HasCompleted);
    }

    // ── SelectedIntervalSeconds persists ──────────────────────────────

    [Fact]
    public void Constructor_ReadsIntervalFromSettings()
    {
        _settings.AutoModeIntervalSeconds.Returns(120);

        var sut = CreateSut();

        Assert.Equal(120, sut.SelectedIntervalSeconds);
    }

    // ── ProgressDisplay ───────────────────────────────────────────────

    [Fact]
    public void ProgressDisplay_NoEntries_Empty()
    {
        var sut = CreateSut();
        Assert.Equal(string.Empty, sut.ProgressDisplay);
    }

    // ── EndSessionCommand ─────────────────────────────────────────────

    [Fact]
    public async Task EndSessionCommand_AdvancesOnboardingAndNavigatesBack()
    {
        var sut = CreateSut();

        await ((IAsyncRelayCommand)sut.EndSessionCommand).ExecuteAsync(null);

        _onboardingService.Received(1).Advance();
        await _navigationService.Received(1).GoToAsync("..");
    }

    // ── HasPrevious / HasNext ─────────────────────────────────────────

    [Fact]
    public void HasPrevious_AtZero_False()
    {
        var sut = CreateSut();
        Assert.False(sut.HasPrevious);
    }

    [Fact]
    public void HasNext_NoEntries_False()
    {
        var sut = CreateSut();
        Assert.False(sut.HasNext);
    }
}

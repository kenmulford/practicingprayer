using CommunityToolkit.Mvvm.Input;
using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class PrayerCardsViewModelTests
{
    private readonly ICardService _cardService = Substitute.For<ICardService>();
    private readonly IPrayerService _prayerService = Substitute.For<IPrayerService>();
    private readonly IOnboardingService _onboardingService = Substitute.For<IOnboardingService>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
    private readonly IAccessibilityService _accessibilityService = Substitute.For<IAccessibilityService>();

    private PrayerCardsViewModel CreateSut() =>
        new(_cardService, _prayerService, _onboardingService, _navigationService, _accessibilityService);

    // ── Construction ──────────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesEmptyCollections()
    {
        var sut = CreateSut();

        Assert.Empty(sut.AllPrayerCards);
        Assert.Empty(sut.FilteredPrayerCards);
        Assert.False(sut.IsLoading);
        Assert.Equal(string.Empty, sut.SearchText);
    }

    // ── IsLoading announces ───────────────────────────────────────────

    [Fact]
    public void IsLoading_True_AnnouncesLoading()
    {
        var sut = CreateSut();

        sut.IsLoading = true;

        _accessibilityService.Received(1).Announce("Loading");
    }

    [Fact]
    public void IsLoading_False_AnnouncesContentLoaded()
    {
        var sut = CreateSut();
        sut.IsLoading = true;
        _accessibilityService.ClearReceivedCalls();

        sut.IsLoading = false;

        _accessibilityService.Received(1).Announce("Content loaded");
    }

    // ── NewCommand ────────────────────────────────────────────────────

    [Fact]
    public async Task NewCommand_AdvancesOnboarding_AndNavigates()
    {
        var sut = CreateSut();

        await ((IAsyncRelayCommand)sut.NewCommand).ExecuteAsync(null);

        _onboardingService.Received(1).Advance();
        await _navigationService.Received(1).GoToAsync(Routes.PrayerCardPage);
    }

    // ── LoadAsync cache invalidation ──────────────────────────────────

    [Fact]
    public async Task LoadAsync_InvalidatesCardCache()
    {
        // LoadAsync will fail creating child VMs (no MAUI runtime),
        // but should still have called InvalidateCache before the error.
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        _cardService.Received(1).InvalidateCache();
    }

    // ── RefreshAsync invalidates both caches ──────────────────────────

    [Fact]
    public async Task RefreshAsync_InvalidatesBothCaches()
    {
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());

        var sut = CreateSut();
        await sut.RefreshAsync();

        _cardService.Received(1).InvalidateCache();
        _prayerService.Received(1).InvalidateCache();
    }
}

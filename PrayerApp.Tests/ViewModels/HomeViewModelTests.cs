using CommunityToolkit.Mvvm.Input;
using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class HomeViewModelTests
{
    private readonly IPrayerService _prayerService = Substitute.For<IPrayerService>();
    private readonly ICardService _cardService = Substitute.For<ICardService>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
    private readonly ISettings _settings = Substitute.For<ISettings>();

    private HomeViewModel CreateSut() => new(_prayerService, _cardService, _navigationService, _settings);

    // ── LoadAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_WithOverduePrayers_SetsOverdueCount()
    {
        _settings.OverdueDayThreshold.Returns(30);
        var prayers = new List<Prayer>
        {
            new() { Id = 1, Title = "P1", PrayerCardId = 1 },
            new() { Id = 2, Title = "P2", PrayerCardId = 1 }
        };
        _prayerService.GetOverduePrayersAsync(30).Returns(prayers.AsReadOnly());
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 1, Title = "General" }
        }.AsReadOnly());
        _prayerService.GetLastInteractionDateAsync().Returns((DateTime?)DateTime.Now);

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.Equal(2, sut.OverdueCount);
        Assert.True(sut.HasOverdue);
        Assert.Equal("2 Overdue", sut.OverdueHeadline);
    }

    [Fact]
    public async Task LoadAsync_NoOverdue_ShowsCaughtUp()
    {
        _settings.OverdueDayThreshold.Returns(30);
        _prayerService.GetOverduePrayersAsync(30).Returns(new List<Prayer>().AsReadOnly());
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
        _prayerService.GetLastInteractionDateAsync().Returns((DateTime?)null);

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.Equal(0, sut.OverdueCount);
        Assert.False(sut.HasOverdue);
        Assert.Equal("You're all caught up!", sut.OverdueHeadline);
    }

    [Fact]
    public async Task LoadAsync_PopulatesSuggestedPrayers_MaxFive()
    {
        _settings.OverdueDayThreshold.Returns(30);
        var prayers = Enumerable.Range(1, 8)
            .Select(i => new Prayer { Id = i, Title = $"P{i}", PrayerCardId = 1 })
            .ToList();
        _prayerService.GetOverduePrayersAsync(30).Returns(prayers.AsReadOnly());
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 1, Title = "General" }
        }.AsReadOnly());
        _prayerService.GetLastInteractionDateAsync().Returns((DateTime?)null);

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.Equal(5, sut.SuggestedPrayers.Count);
    }

    [Fact]
    public async Task LoadAsync_InvalidatesCachesBeforeQuery()
    {
        _settings.OverdueDayThreshold.Returns(30);
        _prayerService.GetOverduePrayersAsync(30).Returns(new List<Prayer>().AsReadOnly());
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
        _prayerService.GetLastInteractionDateAsync().Returns((DateTime?)null);

        var sut = CreateSut();
        await sut.LoadAsync();

        _prayerService.Received(1).InvalidateCache();
        _cardService.Received(1).InvalidateCache();
    }

    // ── FormatLastPrayed ──────────────────────────────────────────────

    [Fact]
    public void FormatLastPrayed_Null_ReturnsNever()
    {
        Assert.Equal("Never", HomeViewModel.FormatLastPrayed(null));
    }

    [Fact]
    public void FormatLastPrayed_Today_ReturnsToday()
    {
        Assert.Equal("Today", HomeViewModel.FormatLastPrayed(DateTime.Now));
    }

    [Fact]
    public void FormatLastPrayed_Yesterday_ReturnsYesterday()
    {
        Assert.Equal("Yesterday", HomeViewModel.FormatLastPrayed(DateTime.Now.AddDays(-1.5)));
    }

    // ── OverdueEmptyDescription ───────────────────────────────────────

    [Fact]
    public void OverdueEmptyDescription_UsesSetting()
    {
        _settings.OverdueDayThreshold.Returns(7);
        var sut = CreateSut();

        Assert.Contains("7 days", sut.OverdueEmptyDescription);
    }

    [Fact]
    public void OverdueEmptyDescription_SingularDay()
    {
        _settings.OverdueDayThreshold.Returns(1);
        var sut = CreateSut();

        Assert.Contains("1 day", sut.OverdueEmptyDescription);
        Assert.DoesNotContain("days", sut.OverdueEmptyDescription);
    }

    // ── GoToOverdueCommand ────────────────────────────────────────────

    [Fact]
    public async Task GoToOverdueCommand_WhenHasOverdue_Navigates()
    {
        _settings.OverdueDayThreshold.Returns(30);
        _prayerService.GetOverduePrayersAsync(30).Returns(new List<Prayer>
        {
            new() { Id = 1, Title = "P1", PrayerCardId = 1 }
        }.AsReadOnly());
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 1, Title = "Card" }
        }.AsReadOnly());
        _prayerService.GetLastInteractionDateAsync().Returns((DateTime?)null);

        var sut = CreateSut();
        await sut.LoadAsync();

        await ((IAsyncRelayCommand)sut.GoToOverdueCommand).ExecuteAsync(null);

        await _navigationService.Received(1).GoToAsync("//PrayersPage?filter=overdue");
    }

    [Fact]
    public async Task GoToOverdueCommand_WhenNoOverdue_DoesNotNavigate()
    {
        var sut = CreateSut(); // OverdueCount defaults to 0

        await ((IAsyncRelayCommand)sut.GoToOverdueCommand).ExecuteAsync(null);

        await _navigationService.DidNotReceive().GoToAsync(Arg.Any<string>());
    }
}

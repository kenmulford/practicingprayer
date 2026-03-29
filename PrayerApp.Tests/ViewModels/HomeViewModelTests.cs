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
    private readonly ITagService _tagService = Substitute.For<ITagService>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
    private readonly ISettings _settings = Substitute.For<ISettings>();

    private HomeViewModel CreateSut() => new(_prayerService, _cardService, _tagService, _navigationService, _settings);

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

    // ── Active Card Count ────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_SetsActiveCardCount_ExcludesEmptyCards()
    {
        SetupDefaultMocks();
        // 3 cards total, but only cards 1 and 2 have active prayers
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 1, Title = "Family" },
            new() { Id = 2, Title = "Work" },
            new() { Id = 3, Title = "Empty Card" }
        }.AsReadOnly());
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 1, Title = "P1", PrayerCardId = 1 },
            new() { Id = 2, Title = "P2", PrayerCardId = 1 },
            new() { Id = 3, Title = "P3", PrayerCardId = 2 }
        }.AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.Equal(2, sut.ActiveCardCount);
        Assert.True(sut.HasActiveCards);
    }

    // ── Unanswered Count ──────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_SetsUnansweredCount()
    {
        SetupDefaultMocks();
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 1, Title = "P1", PrayerCardId = 1 },
            new() { Id = 2, Title = "P2", PrayerCardId = 1 },
            new() { Id = 3, Title = "P3", PrayerCardId = 2 },
            new() { Id = 4, Title = "P4", PrayerCardId = 2 },
            new() { Id = 5, Title = "P5", PrayerCardId = 3 }
        }.AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.Equal(5, sut.UnansweredCount);
        Assert.True(sut.HasUnanswered);
    }

    // ── Last Prayed Date Components ───────────────────────────────────

    [Fact]
    public async Task LoadAsync_SetsLastPrayedDateComponents()
    {
        SetupDefaultMocks();
        _prayerService.GetLastInteractionDateAsync().Returns((DateTime?)new DateTime(2026, 3, 27));

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.Equal("MAR", sut.LastPrayedMonth);
        Assert.Equal("27", sut.LastPrayedDay);
        Assert.True(sut.HasLastPrayed);
    }

    [Fact]
    public async Task LoadAsync_NullLastPrayed_HasLastPrayedFalse()
    {
        SetupDefaultMocks();
        _prayerService.GetLastInteractionDateAsync().Returns((DateTime?)null);

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.False(sut.HasLastPrayed);
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

    // ── Singular/Plural Labels ────────────────────────────────────────

    [Fact]
    public async Task ActiveCardLabel_Singular()
    {
        SetupDefaultMocks();
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 1, Title = "P1", PrayerCardId = 1 }
        }.AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.Equal(1, sut.ActiveCardCount);
        Assert.Equal("Active Card", sut.ActiveCardLabel);
    }

    [Fact]
    public async Task ActiveCardLabel_Plural()
    {
        SetupDefaultMocks();
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 1, Title = "P1", PrayerCardId = 1 },
            new() { Id = 2, Title = "P2", PrayerCardId = 2 }
        }.AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.Equal(2, sut.ActiveCardCount);
        Assert.Equal("Active Cards", sut.ActiveCardLabel);
    }

    [Fact]
    public async Task UnansweredLabel_Singular()
    {
        SetupDefaultMocks();
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 1, Title = "P1", PrayerCardId = 1 }
        }.AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.Equal(1, sut.UnansweredCount);
        Assert.Equal("Unanswered Prayer", sut.UnansweredLabel);
    }

    [Fact]
    public async Task UnansweredLabel_Plural()
    {
        SetupDefaultMocks();
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 1, Title = "P1", PrayerCardId = 1 },
            new() { Id = 2, Title = "P2", PrayerCardId = 1 }
        }.AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.Equal(2, sut.UnansweredCount);
        Assert.Equal("Unanswered Prayers", sut.UnansweredLabel);
    }

    // ── Navigation Commands ───────────────────────────────────────────

    [Fact]
    public async Task GoToCardsCommand_NavigatesToCardsPage()
    {
        var sut = CreateSut();
        await ((IAsyncRelayCommand)sut.GoToCardsCommand).ExecuteAsync(null);
        await _navigationService.Received(1).GoToAsync(Routes.PrayerCardsTab);
    }

    [Fact]
    public async Task GoToPrayersCommand_NavigatesToPrayersPage()
    {
        var sut = CreateSut();
        await ((IAsyncRelayCommand)sut.GoToPrayersCommand).ExecuteAsync(null);
        await _navigationService.Received(1).GoToAsync(Routes.PrayersTab);
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

        await _navigationService.Received(1).GoToAsync($"{Routes.PrayersTab}?filter=overdue");
    }

    [Fact]
    public async Task GoToOverdueCommand_WhenNoOverdue_DoesNotNavigate()
    {
        var sut = CreateSut(); // OverdueCount defaults to 0

        await ((IAsyncRelayCommand)sut.GoToOverdueCommand).ExecuteAsync(null);

        await _navigationService.DidNotReceive().GoToAsync(Arg.Any<string>());
    }

    // ── HasActivePrayers ──────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_NoPrayers_HasActivePrayersFalse()
    {
        SetupDefaultMocks();
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>().AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.False(sut.HasActivePrayers);
    }

    [Fact]
    public async Task LoadAsync_WithPrayers_HasActivePrayersTrue()
    {
        SetupDefaultMocks();
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 1, Title = "Active Prayer", PrayerCardId = 1 }
        }.AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.True(sut.HasActivePrayers);
    }

    // ── HasTags ───────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_NoTags_HasTagsFalse()
    {
        SetupDefaultMocks();
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.False(sut.HasTags);
    }

    [Fact]
    public async Task LoadAsync_WithTags_HasTagsTrue()
    {
        SetupDefaultMocks();
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            new() { Id = 1, Name = "Gratitude" }
        }.AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.True(sut.HasTags);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    /// <summary>Set up minimal mocks so LoadAsync doesn't throw.</summary>
    private void SetupDefaultMocks()
    {
        _settings.OverdueDayThreshold.Returns(30);
        _prayerService.GetOverduePrayersAsync(30).Returns(new List<Prayer>().AsReadOnly());
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
        _prayerService.GetLastInteractionDateAsync().Returns((DateTime?)null);
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
    }
}

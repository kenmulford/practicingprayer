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
    private readonly IBoxService _boxService = Substitute.For<IBoxService>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
    private readonly IAccessibilityService _accessibilityService = Substitute.For<IAccessibilityService>();
    private readonly ISettings _settings = Substitute.For<ISettings>();

    private HomeViewModel CreateSut() => new(_prayerService, _cardService, _tagService, _boxService, _navigationService, _accessibilityService, _settings);

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
        Assert.Equal("All requests have been recently prayed for.", sut.OverdueHeadline);
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

    // ── HasUserBoxesWithCards ────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_UserBoxesWithActivePrayers_HasUserBoxesWithCardsTrue()
    {
        SetupDefaultMocks();
        _boxService.GetBoxesAsync().Returns(new List<CardBox>
        {
            new() { Id = 1, Name = "Family", IsSystem = false }
        }.AsReadOnly());
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 10, BoxId = 1 }
        }.AsReadOnly());
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 100, PrayerCardId = 10 }
        }.AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.True(sut.HasUserBoxesWithCards);
    }

    [Fact]
    public async Task LoadAsync_OnlySystemBoxes_HasUserBoxesWithCardsFalse()
    {
        SetupDefaultMocks();
        _boxService.GetBoxesAsync().Returns(new List<CardBox>
        {
            new() { Id = 1, Name = "System", IsSystem = true, SystemKey = CardBox.SystemKeySystem },
            new() { Id = 2, Name = "Archived", IsSystem = true, SystemKey = CardBox.SystemKeyArchived }
        }.AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.False(sut.HasUserBoxesWithCards);
    }

    [Fact]
    public async Task LoadAsync_UserBoxesButNoActivePrayers_HasUserBoxesWithCardsFalse()
    {
        SetupDefaultMocks();
        _boxService.GetBoxesAsync().Returns(new List<CardBox>
        {
            new() { Id = 1, Name = "Family", IsSystem = false }
        }.AsReadOnly());
        // Card is in the box but has no active prayers
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 10, BoxId = 1 }
        }.AsReadOnly());
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>().AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.False(sut.HasUserBoxesWithCards);
    }

    [Fact]
    public async Task LoadAsync_NoBoxes_HasUserBoxesWithCardsFalse()
    {
        SetupDefaultMocks();
        _boxService.GetBoxesAsync().Returns(new List<CardBox>().AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.False(sut.HasUserBoxesWithCards);
    }

    // ── Accessible Summaries ──────────────────────────────────────────

    [Fact]
    public async Task ActiveCardsAccessible_PopulatedAndEmpty()
    {
        SetupDefaultMocks();
        var sut = CreateSut();
        await sut.LoadAsync();
        Assert.Equal("No active cards. Tap to create your first card.", sut.ActiveCardsAccessible);

        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 1, Title = "Family" },
            new() { Id = 2, Title = "Work" }
        }.AsReadOnly());
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 1, Title = "P1", PrayerCardId = 1 },
            new() { Id = 2, Title = "P2", PrayerCardId = 2 }
        }.AsReadOnly());
        await sut.LoadAsync();
        Assert.Equal("Active cards, 2. Tap to view prayer cards.", sut.ActiveCardsAccessible);
    }

    [Fact]
    public async Task UnansweredAccessible_PopulatedAndEmpty()
    {
        SetupDefaultMocks();
        var sut = CreateSut();
        await sut.LoadAsync();
        Assert.Equal("No prayers yet. Tap to add your first prayer.", sut.UnansweredAccessible);

        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 1, Title = "P1", PrayerCardId = 1 },
            new() { Id = 2, Title = "P2", PrayerCardId = 1 },
            new() { Id = 3, Title = "P3", PrayerCardId = 1 }
        }.AsReadOnly());
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 1, Title = "General" }
        }.AsReadOnly());
        await sut.LoadAsync();
        Assert.Equal("Unanswered prayers, 3. Tap to view prayers.", sut.UnansweredAccessible);
    }

    [Fact]
    public async Task LastPrayedAccessible_PopulatedAndEmpty()
    {
        SetupDefaultMocks();
        var sut = CreateSut();
        await sut.LoadAsync();
        Assert.Equal("Not yet prayed.", sut.LastPrayedAccessible);

        var date = new DateTime(2026, 3, 15);
        _prayerService.GetLastInteractionDateAsync().Returns((DateTime?)date);
        await sut.LoadAsync();
        var expectedMonth = date.ToString("MMM").ToUpper();
        Assert.Equal($"Last prayed, {expectedMonth} 15.", sut.LastPrayedAccessible);
    }

    [Fact]
    public async Task OverdueAccessible_PopulatedAndEmpty()
    {
        SetupDefaultMocks();
        var sut = CreateSut();
        await sut.LoadAsync();
        Assert.Equal("All requests have been recently prayed for.", sut.OverdueAccessible);

        _prayerService.GetOverduePrayersAsync(30).Returns(new List<Prayer>
        {
            new() { Id = 1, Title = "Overdue 1", PrayerCardId = 1 },
            new() { Id = 2, Title = "Overdue 2", PrayerCardId = 1 }
        }.AsReadOnly());
        await sut.LoadAsync();
        Assert.Equal("Overdue prayers, 2. Tap to view overdue prayers.", sut.OverdueAccessible);
    }

    // ── Answered on this date ─────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_AnsweredOnThisDate_PopulatesWhenMatches()
    {
        SetupDefaultMocks();
        var matches = new List<Prayer>
        {
            new() { Id = 10, Title = "Healing for Mom", IsAnswered = true },
            new() { Id = 11, Title = "New job for Dad", IsAnswered = true }
        };
        _prayerService.GetAnsweredOnThisDateAsync().Returns(matches.AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.True(sut.HasAnsweredOnThisDate);
        Assert.Equal(2, sut.AnsweredOnThisDate.Count);
    }

    [Fact]
    public async Task LoadAsync_AnsweredOnThisDate_EmptyWhenNoMatches()
    {
        SetupDefaultMocks();
        _prayerService.GetAnsweredOnThisDateAsync().Returns(new List<Prayer>().AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.False(sut.HasAnsweredOnThisDate);
        Assert.Empty(sut.AnsweredOnThisDate);
    }

    [Fact]
    public async Task AnsweredOnThisDateAccessible_ComposesHeaderAndTitles()
    {
        SetupDefaultMocks();
        _prayerService.GetAnsweredOnThisDateAsync().Returns(new List<Prayer>
        {
            new() { Id = 1, Title = "First answer", IsAnswered = true },
            new() { Id = 2, Title = "Second answer", IsAnswered = true }
        }.AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.Contains("First answer", sut.AnsweredOnThisDateAccessible);
        Assert.Contains("Second answer", sut.AnsweredOnThisDateAccessible);
        Assert.Contains(sut.AnsweredOnThisDateHeader, sut.AnsweredOnThisDateAccessible);
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
        _prayerService.GetAnsweredOnThisDateAsync().Returns(new List<Prayer>().AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _boxService.GetBoxesAsync().Returns(new List<CardBox>().AsReadOnly());
    }
}

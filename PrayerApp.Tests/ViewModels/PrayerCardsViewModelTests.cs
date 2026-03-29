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
    private readonly ITagService _tagService = Substitute.For<ITagService>();

    public PrayerCardsViewModelTests()
    {
        // Default mock: GetActivePrayerCountByCardAsync returns 0 for any card
        // (needed because PrayerCardViewModel constructor fires LoadActivePrayerCountAsync)
        _prayerService.GetActivePrayerCountByCardAsync(Arg.Any<int>()).Returns(0);
    }

    private PrayerCardsViewModel CreateSut() =>
        new(_cardService, _prayerService, _onboardingService, _navigationService, _accessibilityService, _tagService);

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
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.LoadAsync();

        _cardService.Received(1).InvalidateCache();
    }

    // ── RefreshAsync invalidates both caches ──────────────────────────

    [Fact]
    public async Task RefreshAsync_InvalidatesBothCaches()
    {
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.RefreshAsync();

        _cardService.Received(1).InvalidateCache();
        _prayerService.Received(1).InvalidateCache();
    }

    // ── Tag filter tests ────────────────────────────────────────────────

    [Fact]
    public async Task TagFilter_ShowsCardsWithUnansweredTaggedPrayers()
    {
        // Arrange: card 1 has an unanswered prayer (id=10) tagged with tag 100
        var card1 = new PrayerCard { Id = 1, Title = "Card One" };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { card1 }.AsReadOnly());

        var tag = new PrayerTag { Id = 100, Name = "Healing" };
        _tagService.GetTagsAsync().Returns(new List<PrayerTag> { tag }.AsReadOnly());

        var prayer = new Prayer { Id = 10, PrayerCardId = 1, Title = "Pray for healing", IsAnswered = false };
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer> { prayer }.AsReadOnly());

        var junction = new PrayerCardTag { Id = 1, PrayerCardId = 1, PrayerTagId = 100, PrayerRequestId = 10 };
        SetupDbMocks(new List<PrayerCardTag> { junction });

        var sut = CreateSut();
        await sut.LoadAsync();

        // Act: select the tag chip
        Assert.Single(sut.AvailableTags);
        sut.AvailableTags[0].ToggleCommand.Execute(null);

        // Assert: card appears in filtered results
        Assert.Single(sut.FilteredPrayerCards);
        Assert.Equal("Card One", sut.FilteredPrayerCards[0].Title);
    }

    [Fact]
    public async Task TagFilter_ExcludesCardsWhereAllTaggedPrayersAnswered()
    {
        // Arrange: card 1 has an answered prayer (id=10) tagged with tag 100
        var card1 = new PrayerCard { Id = 1, Title = "Card One" };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { card1 }.AsReadOnly());

        var tag = new PrayerTag { Id = 100, Name = "Healing" };
        _tagService.GetTagsAsync().Returns(new List<PrayerTag> { tag }.AsReadOnly());

        var prayer = new Prayer { Id = 10, PrayerCardId = 1, Title = "Answered prayer", IsAnswered = true };
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer> { prayer }.AsReadOnly());

        var junction = new PrayerCardTag { Id = 1, PrayerCardId = 1, PrayerTagId = 100, PrayerRequestId = 10 };
        SetupDbMocks(new List<PrayerCardTag> { junction });

        var sut = CreateSut();
        await sut.LoadAsync();

        // Act: select the tag chip
        sut.AvailableTags[0].ToggleCommand.Execute(null);

        // Assert: card is excluded (no unanswered tagged prayers)
        Assert.Empty(sut.FilteredPrayerCards);
    }

    [Fact]
    public async Task TagFilter_MultipleTagsUseOrLogic()
    {
        // Arrange: card 1 tagged with tag 100, card 2 tagged with tag 200
        var card1 = new PrayerCard { Id = 1, Title = "Card One" };
        var card2 = new PrayerCard { Id = 2, Title = "Card Two" };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { card1, card2 }.AsReadOnly());

        var tag1 = new PrayerTag { Id = 100, Name = "Healing" };
        var tag2 = new PrayerTag { Id = 200, Name = "Guidance" };
        _tagService.GetTagsAsync().Returns(new List<PrayerTag> { tag1, tag2 }.AsReadOnly());

        var prayer1 = new Prayer { Id = 10, PrayerCardId = 1, Title = "Prayer 1", IsAnswered = false };
        var prayer2 = new Prayer { Id = 20, PrayerCardId = 2, Title = "Prayer 2", IsAnswered = false };
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer> { prayer1, prayer2 }.AsReadOnly());

        var junc1 = new PrayerCardTag { Id = 1, PrayerCardId = 1, PrayerTagId = 100, PrayerRequestId = 10 };
        var junc2 = new PrayerCardTag { Id = 2, PrayerCardId = 2, PrayerTagId = 200, PrayerRequestId = 20 };
        SetupDbMocks(new List<PrayerCardTag> { junc1, junc2 });

        var sut = CreateSut();
        await sut.LoadAsync();

        // Act: select both tag chips
        sut.AvailableTags[0].ToggleCommand.Execute(null);
        sut.AvailableTags[1].ToggleCommand.Execute(null);

        // Assert: both cards appear (OR logic)
        Assert.Equal(2, sut.FilteredPrayerCards.Count);
    }

    [Fact]
    public async Task TagFilter_ClearedShowsAllCards()
    {
        // Arrange: card 1 tagged, card 2 not tagged
        var card1 = new PrayerCard { Id = 1, Title = "Card One" };
        var card2 = new PrayerCard { Id = 2, Title = "Card Two" };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { card1, card2 }.AsReadOnly());

        var tag = new PrayerTag { Id = 100, Name = "Healing" };
        _tagService.GetTagsAsync().Returns(new List<PrayerTag> { tag }.AsReadOnly());

        var prayer1 = new Prayer { Id = 10, PrayerCardId = 1, Title = "Prayer 1", IsAnswered = false };
        var prayer2 = new Prayer { Id = 20, PrayerCardId = 2, Title = "Prayer 2", IsAnswered = false };
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer> { prayer1, prayer2 }.AsReadOnly());

        var junction = new PrayerCardTag { Id = 1, PrayerCardId = 1, PrayerTagId = 100, PrayerRequestId = 10 };
        SetupDbMocks(new List<PrayerCardTag> { junction });

        var sut = CreateSut();
        await sut.LoadAsync();

        // Act: select then deselect tag
        sut.AvailableTags[0].ToggleCommand.Execute(null);
        Assert.Single(sut.FilteredPrayerCards); // filtered to 1 card

        sut.AvailableTags[0].ToggleCommand.Execute(null); // deselect

        // Assert: all cards reappear
        Assert.Equal(2, sut.FilteredPrayerCards.Count);
    }

    [Fact]
    public async Task LoadAsync_PopulatesAvailableTags()
    {
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());

        var tag1 = new PrayerTag { Id = 1, Name = "Healing" };
        var tag2 = new PrayerTag { Id = 2, Name = "Guidance" };
        _tagService.GetTagsAsync().Returns(new List<PrayerTag> { tag1, tag2 }.AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.Equal(2, sut.AvailableTags.Count);
        Assert.Equal("Healing", sut.AvailableTags[0].Tag.Name);
        Assert.Equal("Guidance", sut.AvailableTags[1].Tag.Name);
    }

    [Fact]
    public async Task HasTags_TrueWhenTagsExist()
    {
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());

        var tag = new PrayerTag { Id = 1, Name = "Healing" };
        _tagService.GetTagsAsync().Returns(new List<PrayerTag> { tag }.AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.True(sut.HasTags);
    }

    // ── Helper ──────────────────────────────────────────────────────────

    private void SetupDbMocks(List<PrayerCardTag> junctions)
    {
        var db = Substitute.For<IDBService>();
        PrayerCardTag.SetDBService(db);
        db.GetAllAsync<PrayerCardTag>().Returns(junctions);
    }
}

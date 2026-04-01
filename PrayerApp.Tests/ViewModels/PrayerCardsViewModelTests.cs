using CommunityToolkit.Mvvm.Input;
using NSubstitute;
using PrayerApp.Helpers;
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
    private readonly ISettings _settings = Substitute.For<ISettings>();
    private readonly IBoxService _boxService = Substitute.For<IBoxService>();

    public PrayerCardsViewModelTests()
    {
        // Default mock: GetActivePrayerCountByCardAsync returns 0 for any card
        // (needed because PrayerCardViewModel constructor fires LoadActivePrayerCountAsync)
        _prayerService.GetActivePrayerCountByCardAsync(Arg.Any<int>()).Returns(0);

        // Default: no boxes (sections will be empty)
        _boxService.GetBoxesAsync().Returns(new List<CardBox>().AsReadOnly());
    }

    private PrayerCardsViewModel CreateSut() =>
        new(_cardService, _prayerService, _onboardingService, _navigationService,
            _accessibilityService, _tagService, _settings, _boxService);

    /// <summary>Sets up standard system boxes so sections can be built.</summary>
    private void SetupSystemBoxes()
    {
        var boxes = new List<CardBox>
        {
            new() { Id = 10, Name = "System", IsSystem = true, SystemKey = CardBox.SystemKeySystem, SortOrder = 900 },
            new() { Id = 20, Name = "Archived", IsSystem = true, SystemKey = CardBox.SystemKeyArchived, SortOrder = 999 }
        };
        _boxService.GetBoxesAsync().Returns(boxes.AsReadOnly());
        _settings.ArchivedFolderId.Returns(20);
    }

    /// <summary>Helper to get all visible cards across all sections.</summary>
    private List<PrayerCardViewModel> GetAllVisibleCards(PrayerCardsViewModel sut) =>
        sut.BoxSections.SelectMany(s => s).ToList();

    // ── Construction ──────────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesEmptyCollections()
    {
        var sut = CreateSut();

        Assert.Empty(sut.AllPrayerCards);
        Assert.Empty(sut.BoxSections);
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
    public async Task LoadAsync_InvalidatesCardAndBoxCaches()
    {
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.LoadAsync();

        _cardService.Received(1).InvalidateCache();
        _boxService.Received(1).InvalidateCache();
    }

    // ── RefreshAsync invalidates caches ───────────────────────────────

    [Fact]
    public async Task RefreshAsync_InvalidatesAllCaches()
    {
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.RefreshAsync();

        _cardService.Received(1).InvalidateCache();
        _prayerService.Received(1).InvalidateCache();
        _boxService.Received(1).InvalidateCache();
    }

    // ── Section building ──────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_BuildsSections_UnboxedAndSystemAndArchived()
    {
        SetupSystemBoxes();
        var card1 = new PrayerCard { Id = 1, Title = "Alpha", BoxId = 0 }; // Unboxed
        var card2 = new PrayerCard { Id = 2, Title = "Quick Add", IsSystem = true, BoxId = 10 }; // System
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { card1, card2 }.AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.LoadAsync();

        // Should have: Unboxed (1 card), System (1 card), Archived (0 cards but always shown)
        Assert.Equal(3, sut.BoxSections.Count);
        Assert.Equal(BoxStrings.Unorganized, sut.BoxSections[0].Name);
        Assert.Equal(1, sut.BoxSections[0].CardCount);
        Assert.Equal("System", sut.BoxSections[1].Name);
        Assert.Equal(1, sut.BoxSections[1].CardCount);
        Assert.Equal("Archived", sut.BoxSections[2].Name);
        Assert.Equal(0, sut.BoxSections[2].CardCount);
    }

    [Fact]
    public async Task LoadAsync_UserBoxesSortedByName()
    {
        SetupSystemBoxes();
        var boxes = new List<CardBox>
        {
            new() { Id = 5, Name = "Zulu", SortOrder = 0 },
            new() { Id = 6, Name = "Alpha", SortOrder = 0 },
            new() { Id = 10, Name = "System", IsSystem = true, SystemKey = CardBox.SystemKeySystem, SortOrder = 900 },
            new() { Id = 20, Name = "Archived", IsSystem = true, SystemKey = CardBox.SystemKeyArchived, SortOrder = 999 }
        };
        _boxService.GetBoxesAsync().Returns(boxes.AsReadOnly());

        var card1 = new PrayerCard { Id = 1, Title = "Card A", BoxId = 5 }; // Zulu
        var card2 = new PrayerCard { Id = 2, Title = "Card B", BoxId = 6 }; // Alpha
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { card1, card2 }.AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.LoadAsync();

        // User boxes sorted A→Z: Alpha before Zulu
        Assert.Equal("Alpha", sut.BoxSections[0].Name);
        Assert.Equal("Zulu", sut.BoxSections[1].Name);
    }

    [Fact]
    public async Task LoadAsync_CardsWithinSectionSorted_FavoritesFirst()
    {
        SetupSystemBoxes();
        var card1 = new PrayerCard { Id = 1, Title = "Beta", BoxId = 0, IsFavorite = false };
        var card2 = new PrayerCard { Id = 2, Title = "Alpha", BoxId = 0, IsFavorite = true };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { card1, card2 }.AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.LoadAsync();

        var unboxed = sut.BoxSections.First(s => s.BoxId == 0);
        Assert.Equal("Alpha", unboxed[0].Title); // Favorited → first
        Assert.Equal("Beta", unboxed[1].Title);
    }

    // ── Tag filter tests ────────────────────────────────────────────────

    [Fact]
    public async Task TagFilter_ShowsCardsWithUnansweredTaggedPrayers()
    {
        SetupSystemBoxes();
        var card1 = new PrayerCard { Id = 1, Title = "Card One", BoxId = 0 };
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

        // Assert: card appears in visible results
        var visible = GetAllVisibleCards(sut);
        Assert.Single(visible);
        Assert.Equal("Card One", visible[0].Title);
    }

    [Fact]
    public async Task TagFilter_ExcludesCardsWhereAllTaggedPrayersAnswered()
    {
        SetupSystemBoxes();
        var card1 = new PrayerCard { Id = 1, Title = "Card One", BoxId = 0 };
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
        var visible = GetAllVisibleCards(sut);
        Assert.Empty(visible);
    }

    [Fact]
    public async Task TagFilter_MultipleTagsUseOrLogic()
    {
        SetupSystemBoxes();
        var card1 = new PrayerCard { Id = 1, Title = "Card One", BoxId = 0 };
        var card2 = new PrayerCard { Id = 2, Title = "Card Two", BoxId = 0 };
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
        var visible = GetAllVisibleCards(sut);
        Assert.Equal(2, visible.Count);
    }

    [Fact]
    public async Task TagFilter_ClearedShowsAllCards()
    {
        SetupSystemBoxes();
        var card1 = new PrayerCard { Id = 1, Title = "Card One", BoxId = 0 };
        var card2 = new PrayerCard { Id = 2, Title = "Card Two", BoxId = 0 };
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
        var filteredVisible = GetAllVisibleCards(sut);
        Assert.Single(filteredVisible); // filtered to 1 card

        sut.AvailableTags[0].ToggleCommand.Execute(null); // deselect

        // Assert: all cards reappear
        var allVisible = GetAllVisibleCards(sut);
        Assert.Equal(2, allVisible.Count);
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

    // ── Archived section always visible ───────────────────────────────

    [Fact]
    public async Task LoadAsync_ArchivedSectionAlwaysPresent_EvenWhenEmpty()
    {
        SetupSystemBoxes();
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.LoadAsync();

        // Only Archived should be visible (no cards in any section, but Archived is always shown)
        Assert.Single(sut.BoxSections);
        Assert.Equal("Archived", sut.BoxSections[0].Name);
        Assert.Equal(0, sut.BoxSections[0].CardCount);
    }

    // ── Multi-select ─────────────────────────────────────────────────

    [Fact]
    public async Task EnterMultiSelectMode_SetsIsMultiSelectModeAndSelectsCard()
    {
        SetupSystemBoxes();
        var card = new PrayerCard { Id = 1, Title = "Test", BoxId = 0 };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { card }.AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.LoadAsync();

        sut.EnterMultiSelectMode(sut.AllPrayerCards[0]);

        Assert.True(sut.IsMultiSelectMode);
        Assert.True(sut.AllPrayerCards[0].IsMultiSelected);
        Assert.Equal(1, sut.SelectedCardCount);
    }

    [Fact]
    public async Task ToggleCardSelection_TogglesIsMultiSelected()
    {
        SetupSystemBoxes();
        var card = new PrayerCard { Id = 1, Title = "Test", BoxId = 0 };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { card }.AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.LoadAsync();
        sut.EnterMultiSelectMode(sut.AllPrayerCards[0]);

        sut.ToggleCardSelection(sut.AllPrayerCards[0]); // deselect
        Assert.False(sut.AllPrayerCards[0].IsMultiSelected);
        Assert.Equal(0, sut.SelectedCardCount);
    }

    [Fact]
    public async Task CancelMultiSelectCommand_DeselectsAllAndExitsMode()
    {
        SetupSystemBoxes();
        var card1 = new PrayerCard { Id = 1, Title = "A", BoxId = 0 };
        var card2 = new PrayerCard { Id = 2, Title = "B", BoxId = 0 };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { card1, card2 }.AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.LoadAsync();
        sut.EnterMultiSelectMode(sut.AllPrayerCards[0]);
        sut.ToggleCardSelection(sut.AllPrayerCards[1]); // select second too

        sut.CancelMultiSelectCommand.Execute(null);

        Assert.False(sut.IsMultiSelectMode);
        Assert.False(sut.AllPrayerCards[0].IsMultiSelected);
        Assert.False(sut.AllPrayerCards[1].IsMultiSelected);
    }

    [Fact]
    public async Task MoveSelectedCommand_Cancel_DoesNotMove()
    {
        SetupSystemBoxes();
        var card = new PrayerCard { Id = 1, Title = "Test", BoxId = 0 };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { card }.AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());
        _boxService.GetBoxesAsync().Returns(new List<CardBox>
        {
            new() { Id = 5, Name = "Family" },
            new() { Id = 10, Name = "System", IsSystem = true, SystemKey = CardBox.SystemKeySystem, SortOrder = 900 },
            new() { Id = 20, Name = "Archived", IsSystem = true, SystemKey = CardBox.SystemKeyArchived, SortOrder = 999 }
        }.AsReadOnly());
        _navigationService.DisplayActionSheetAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string[]>())
            .Returns("Cancel");

        var sut = CreateSut();
        await sut.LoadAsync();
        sut.EnterMultiSelectMode(sut.AllPrayerCards[0]);

        await ((IAsyncRelayCommand)sut.MoveSelectedCommand).ExecuteAsync(null);

        await _cardService.DidNotReceive().AssignBoxAsync(Arg.Any<PrayerCard>(), Arg.Any<int>());
        Assert.True(sut.IsMultiSelectMode); // stays in mode
    }

    [Fact]
    public async Task MoveSelectedCommand_SelectsBox_MovesCardsAndExitsMode()
    {
        SetupSystemBoxes();
        var card = new PrayerCard { Id = 1, Title = "Test", BoxId = 0 };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { card }.AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());
        _boxService.GetBoxesAsync().Returns(new List<CardBox>
        {
            new() { Id = 5, Name = "Family" },
            new() { Id = 10, Name = "System", IsSystem = true, SystemKey = CardBox.SystemKeySystem, SortOrder = 900 },
            new() { Id = 20, Name = "Archived", IsSystem = true, SystemKey = CardBox.SystemKeyArchived, SortOrder = 999 }
        }.AsReadOnly());
        _navigationService.DisplayActionSheetAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string[]>())
            .Returns("Family");

        var sut = CreateSut();
        await sut.LoadAsync();
        sut.EnterMultiSelectMode(sut.AllPrayerCards[0]);

        await ((IAsyncRelayCommand)sut.MoveSelectedCommand).ExecuteAsync(null);

        await _cardService.Received(1).AssignBoxAsync(Arg.Any<PrayerCard>(), 5);
        Assert.False(sut.IsMultiSelectMode); // exits mode after move
    }

    [Fact]
    public void SelectedCountText_FormatsCorrectly()
    {
        var sut = CreateSut();
        Assert.Equal("None selected", sut.SelectedCountText);
    }

    // ── Helper ──────────────────────────────────────────────────────────

    private void SetupDbMocks(List<PrayerCardTag> junctions)
    {
        var db = Substitute.For<IDBService>();
        PrayerCardTag.SetDBService(db);
        db.GetAllAsync<PrayerCardTag>().Returns(junctions);
    }
}

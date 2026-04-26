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
        _settings.ExpandedSectionIds.Returns("0"); // Expand unboxed so items are visible
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
        _settings.ExpandedSectionIds.Returns("0"); // Section starts expanded so restore returns to expanded
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

        // Wait past the 300ms long-press suppression window
        await Task.Delay(350);

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

    // ── BUG-55: HasNoSections ──────────────────────────────────────────────

    [Fact]
    public async Task HasNoSections_TrueWhenNoSections()
    {
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.True(sut.HasNoSections);
    }

    [Fact]
    public async Task HasNoSections_FalseWhenSectionsExist()
    {
        SetupSystemBoxes();
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 1, Title = "Test", BoxId = 0 }
        }.AsReadOnly());
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.False(sut.HasNoSections);
    }

    // ── BUG-56: Empty user boxes always shown ────────────────────────

    [Fact]
    public async Task LoadAsync_EmptyUserBox_AppearsAsSection()
    {
        var boxes = new List<CardBox>
        {
            new() { Id = 5, Name = "Empty Box", IsSystem = false, SortOrder = 0 },
            new() { Id = 10, Name = "System", IsSystem = true, SystemKey = CardBox.SystemKeySystem, SortOrder = 900 },
            new() { Id = 20, Name = "Archived", IsSystem = true, SystemKey = CardBox.SystemKeyArchived, SortOrder = 999 }
        };
        _boxService.GetBoxesAsync().Returns(boxes.AsReadOnly());
        _settings.ArchivedFolderId.Returns(20);
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
        var sut = CreateSut();

        await sut.LoadAsync();

        var emptySection = sut.BoxSections.FirstOrDefault(s => s.BoxId == 5);
        Assert.NotNull(emptySection);
        Assert.Equal("Empty Box", emptySection.Name);
        Assert.Equal(0, emptySection.CardCount);
    }

    // ── BUG-59: Multi-select propagation ─────────────────────────────

    [Fact]
    public async Task IsMultiSelectMode_PropagatesToSections()
    {
        SetupSystemBoxes();
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 1, Title = "Card", BoxId = 0 }
        }.AsReadOnly());
        var sut = CreateSut();
        await sut.LoadAsync();

        sut.IsMultiSelectMode = true;

        Assert.All(sut.BoxSections, s => Assert.True(s.IsMultiSelectMode));
    }

    [Fact]
    public async Task IsMultiSelectMode_False_ClearsOnSections()
    {
        SetupSystemBoxes();
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 1, Title = "Card", BoxId = 0 }
        }.AsReadOnly());
        var sut = CreateSut();
        await sut.LoadAsync();
        sut.IsMultiSelectMode = true;

        sut.IsMultiSelectMode = false;

        Assert.All(sut.BoxSections, s => Assert.False(s.IsMultiSelectMode));
    }

    // ── Collections Banner ─────────────────────────────────────────────

    [Fact]
    public void CollectionsBanner_NotDismissed_ShowsBanner()
    {
        _settings.CollectionsBannerDismissed.Returns(false);

        var sut = CreateSut();

        Assert.True(sut.ShowCollectionsBanner);
    }

    [Fact]
    public void CollectionsBanner_AlreadyDismissed_HidesBanner()
    {
        _settings.CollectionsBannerDismissed.Returns(true);

        var sut = CreateSut();

        Assert.False(sut.ShowCollectionsBanner);
    }

    [Fact]
    public void DismissCollectionsBannerCommand_SetsFlagAndHidesBanner()
    {
        _settings.CollectionsBannerDismissed.Returns(false);

        var sut = CreateSut();
        Assert.True(sut.ShowCollectionsBanner);

        sut.DismissCollectionsBannerCommand.Execute(null);

        Assert.False(sut.ShowCollectionsBanner);
        _settings.Received(1).CollectionsBannerDismissed = true;
    }

    // ── UX-31: Section expansion persistence ─────────────────────────────

    [Fact]
    public async Task LoadAsync_NoSavedState_AllSectionsCollapsed()
    {
        SetupSystemBoxes();
        // ExpandedSectionIds defaults to null/empty → all collapsed
        var card = new PrayerCard { Id = 1, Title = "Test", BoxId = 0 };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { card }.AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.All(sut.BoxSections, s => Assert.False(s.IsExpanded));
    }

    [Fact]
    public async Task LoadAsync_SavedExpandedIds_RestoresExpansionState()
    {
        SetupSystemBoxes();
        _settings.ExpandedSectionIds.Returns("0,20"); // Unboxed and Archived expanded
        var card = new PrayerCard { Id = 1, Title = "Test", BoxId = 0 };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { card }.AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.LoadAsync();

        var unboxed = sut.BoxSections.First(s => s.BoxId == 0);
        var archived = sut.BoxSections.First(s => s.BoxId == 20);
        Assert.True(unboxed.IsExpanded);
        Assert.True(archived.IsExpanded);
    }

    [Fact]
    public async Task SaveSectionExpansionState_PersistsExpandedIds()
    {
        SetupSystemBoxes();
        _settings.ExpandedSectionIds.Returns("0"); // Start with Unboxed expanded
        var card = new PrayerCard { Id = 1, Title = "Test", BoxId = 0 };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { card }.AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.LoadAsync();

        sut.SaveSectionExpansionState();

        // Should save "0" (the Unboxed section that was expanded)
        _settings.Received().ExpandedSectionIds = Arg.Is<string>(s => s.Contains("0"));
    }

    [Fact]
    public async Task SaveSectionExpansionState_AllCollapsed_SavesEmpty()
    {
        SetupSystemBoxes();
        // No saved state → all collapsed
        var card = new PrayerCard { Id = 1, Title = "Test", BoxId = 0 };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { card }.AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.LoadAsync();

        sut.SaveSectionExpansionState();

        _settings.Received().ExpandedSectionIds = "";
    }

    // ── PendingSavedIdentifier (replaces HighlightCardRequested event) ──
    // Background: a tester crashed reproducibly on a Galaxy Ultra when saving a new card.
    // Root cause: ApplyQueryAttributes("saved") fired a C# event whose handler called
    // CollectionView.ScrollTo on a MauiRecyclerView whose adapter snapshot hadn't
    // committed the BoxSections swap from RebuildSections. Fix: stage the identifier
    // here, consume it in OnAppearing on the lifecycle channel.

    [Fact]
    public void ApplyQueryAttributes_Saved_NewIdentifier_SetsPendingSavedIdentifier()
    {
        var sut = CreateSut();
        var query = new Dictionary<string, object> { { "saved", "42" } };

        ((IQueryAttributable)sut).ApplyQueryAttributes(query);

        Assert.Equal("42", sut.PendingSavedIdentifier);
    }

    [Fact]
    public async Task ConsumePendingSavedAsync_WhenIdentifierNull_NoOps_ReturnsNull()
    {
        var sut = CreateSut();

        var result = await sut.ConsumePendingSavedAsync();

        Assert.Null(result);
        Assert.Empty(sut.AllPrayerCards);
    }

    [Fact]
    public async Task ConsumePendingSavedAsync_NewCard_LoadsFromDbAddsToAllPrayerCards()
    {
        SetupSystemBoxes();
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());
        var pcDb = SetupCardLoadMock(42, new PrayerCard { Id = 42, Title = "Just Created", BoxId = 0 });

        var sut = CreateSut();
        await sut.LoadAsync();
        ((IQueryAttributable)sut).ApplyQueryAttributes(
            new Dictionary<string, object> { { "saved", "42" } });

        var result = await sut.ConsumePendingSavedAsync();

        Assert.NotNull(result);
        Assert.Equal(42, result.Id);
        Assert.Contains(sut.AllPrayerCards, c => c.Id == 42);
    }

    [Fact]
    public async Task ConsumePendingSavedAsync_NewCard_MarksHighlightedAndExpanded()
    {
        SetupSystemBoxes();
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());
        SetupCardLoadMock(42, new PrayerCard { Id = 42, Title = "Just Created", BoxId = 0 });

        var sut = CreateSut();
        await sut.LoadAsync();
        ((IQueryAttributable)sut).ApplyQueryAttributes(
            new Dictionary<string, object> { { "saved", "42" } });

        var result = await sut.ConsumePendingSavedAsync();

        Assert.NotNull(result);
        Assert.True(result.IsHighlighted);
        Assert.True(result.IsExpanded);
    }

    [Fact]
    public async Task ConsumePendingSavedAsync_ClearsPendingSavedIdentifier()
    {
        var sut = CreateSut();
        ((IQueryAttributable)sut).ApplyQueryAttributes(
            new Dictionary<string, object> { { "saved", "42" } });
        Assert.Equal("42", sut.PendingSavedIdentifier);

        // Even if no card is mocked (PrayerCard.LoadAsync returns null), the identifier
        // must clear so a stale signal doesn't fire on the next OnAppearing.
        SetupCardLoadMock(42, null);
        await sut.ConsumePendingSavedAsync();

        Assert.Null(sut.PendingSavedIdentifier);
    }

    [Fact]
    public async Task ConsumePendingSavedAsync_MatchedExisting_PreservesStateReturnsNull()
    {
        // Edit case: card was already in AllPrayerCards when ApplyQueryAttributes
        // ran. Don't highlight/expand. Don't scroll (no race window).
        SetupSystemBoxes();
        var existingCard = new PrayerCard { Id = 7, Title = "Existing", BoxId = 0 };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { existingCard }.AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.LoadAsync();
        var matched = sut.AllPrayerCards.Single(c => c.Id == 7);
        var wasExpanded = matched.IsExpanded;
        var wasHighlighted = matched.IsHighlighted;

        // ApplyQueryAttributes runs *while* card 7 is in the list — snapshots
        // wasAlreadyInList = true, so this is treated as an edit.
        ((IQueryAttributable)sut).ApplyQueryAttributes(
            new Dictionary<string, object> { { "saved", "7" } });
        var result = await sut.ConsumePendingSavedAsync();

        Assert.Null(result);
        Assert.Equal(wasExpanded, matched.IsExpanded);
        Assert.Equal(wasHighlighted, matched.IsHighlighted);
        Assert.Single(sut.AllPrayerCards, c => c.Id == 7);
    }

    [Fact]
    public async Task ConsumePendingSavedAsync_MatchedButAddedByRefresh_HighlightsAndExpands()
    {
        // Real-world Galaxy Ultra flow: PrayerCardPage.SaveAsync calls GoToAsync("..?saved=42").
        // Shell fires ApplyQueryAttributes BEFORE OnAppearing. At that moment, card 42 is NOT
        // in AllPrayerCards (it was just inserted into the DB). Snapshot captures
        // wasAlreadyInList=false. THEN OnAppearing's RefreshAsync runs, GetCardsAsync now
        // returns card 42, and the diff loop adds it to AllPrayerCards. THEN
        // ConsumePendingSavedAsync runs: matched != null but wasAlreadyInList=false →
        // highlight + return for scroll.
        SetupSystemBoxes();
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.LoadAsync(); // 0 cards loaded — card 42 not yet present

        // Stage the saved-id while AllPrayerCards is empty (snapshots wasAlreadyInList=false).
        ((IQueryAttributable)sut).ApplyQueryAttributes(
            new Dictionary<string, object> { { "saved", "42" } });

        // Simulate RefreshAsync's diff loop adding card 42 between ApplyQueryAttributes
        // and ConsumePendingSavedAsync (the order Shell + Branch 1's OnAppearing produce).
        var newCard = new PrayerCard { Id = 42, Title = "Just Created", BoxId = 0 };
        var newVm = new PrayerCardViewModel(newCard, _cardService, _prayerService,
            _onboardingService, _navigationService, _accessibilityService, _boxService);
        sut.AllPrayerCards.Add(newVm);

        var result = await sut.ConsumePendingSavedAsync();

        Assert.NotNull(result);
        Assert.Equal(42, result.Id);
        Assert.True(result.IsHighlighted);
        Assert.True(result.IsExpanded);
        Assert.Same(newVm, result); // returned the existing VM, not a duplicate
        Assert.Single(sut.AllPrayerCards, c => c.Id == 42);
    }

    [Fact]
    public void HighlightCardRequested_EventIsRemoved()
    {
        // Reflection guard: this VM→View C# event was the crash path. If a future
        // change re-introduces it, this test fails as a tripwire.
        var evt = typeof(PrayerCardsViewModel).GetEvent("HighlightCardRequested");
        Assert.Null(evt);
    }

    // ── PERF-1: BoxSections reassignment guard ──────────────────────────────
    //
    // RebuildSections must NOT reassign the outer ObservableCollection<BoxSectionViewModel>
    // when section structure (BoxIds in order) is unchanged. Reassigning forces Android
    // RecyclerView to tear down all visible Borders and re-inflate from scratch
    // (~600ms × visible cards), the root cause of PERF-1's post-Save 5–6s stall.
    // When structure DOES change, the reassignment must still happen (iOS replace-not-mutate).

    [Fact]
    public async Task RebuildSections_WhenStructureUnchanged_PreservesBoxSectionsReference()
    {
        // First load + refresh produce identical section structure (same BoxIds in same order).
        // Outer BoxSections reference must be stable across the refresh.
        SetupSystemBoxes();
        var card = new PrayerCard { Id = 1, Title = "A", BoxId = 0 };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { card }.AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.LoadAsync();
        var firstReference = sut.BoxSections;

        await sut.RefreshAsync();

        Assert.Same(firstReference, sut.BoxSections);
    }

    [Fact]
    public async Task RebuildSections_WhenCardAddedToExistingSection_PreservesBoxSectionsReference()
    {
        // Adding a card to an existing section does NOT change section structure
        // (same BoxIds in same order — only one section's inner card count changed).
        // The new card materialization happens via the per-section CollectionChanged event,
        // not via outer rebind. Reference stability is the wire that makes this work.
        SetupSystemBoxes();
        var card1 = new PrayerCard { Id = 1, Title = "A", BoxId = 0 };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { card1 }.AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.LoadAsync();
        var firstReference = sut.BoxSections;

        // Simulate a save by extending the cards list under the same boxes.
        var card2 = new PrayerCard { Id = 2, Title = "B", BoxId = 0 };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { card1, card2 }.AsReadOnly());
        await sut.RefreshAsync();

        Assert.Same(firstReference, sut.BoxSections);
        // Both cards landed in AllPrayerCards via the diff loop.
        Assert.Equal(2, sut.AllPrayerCards.Count);
        // The Unboxed section's backing card count includes the new card (visible count
        // is 0 if the section is collapsed by default — that's a separate concern).
        var unboxed = sut.BoxSections.First(s => s.BoxId == 0);
        Assert.Equal(2, unboxed.CardCount);
    }

    [Fact]
    public async Task RebuildSections_WhenUserBoxAdded_ReplacesBoxSectionsReference()
    {
        // Adding a new user box DOES change section structure (new BoxId appears between
        // Unboxed and System). The outer ObservableCollection MUST be replaced so iOS
        // UICollectionView gets the structure-change signal via the bound-property setter.
        SetupSystemBoxes();
        var card = new PrayerCard { Id = 1, Title = "A", BoxId = 0 };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { card }.AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        SetupDbMocks(new List<PrayerCardTag>());

        var sut = CreateSut();
        await sut.LoadAsync();
        var firstReference = sut.BoxSections;
        var firstSectionCount = sut.BoxSections.Count;

        // Now a user box exists.
        var systemBoxes = new List<CardBox>
        {
            new() { Id = 10, Name = "System", IsSystem = true, SystemKey = CardBox.SystemKeySystem, SortOrder = 900 },
            new() { Id = 20, Name = "Archived", IsSystem = true, SystemKey = CardBox.SystemKeyArchived, SortOrder = 999 },
            new() { Id = 5, Name = "Family", SortOrder = 0 }
        };
        _boxService.GetBoxesAsync().Returns(systemBoxes.AsReadOnly());
        await sut.RefreshAsync();

        Assert.NotSame(firstReference, sut.BoxSections);
        Assert.Equal(firstSectionCount + 1, sut.BoxSections.Count);
    }

    // ── Helper ──────────────────────────────────────────────────────────

    private void SetupDbMocks(List<PrayerCardTag> junctions)
    {
        var db = Substitute.For<IDBService>();
        PrayerCardTag.SetDBService(db);
        db.GetAllAsync<PrayerCardTag>().Returns(junctions);
    }

    /// <summary>Configure PrayerCard.LoadAsync(id) to return the supplied card (or null).</summary>
    private static IDBService SetupCardLoadMock(int cardId, PrayerCard? card)
    {
        var db = Substitute.For<IDBService>();
        PrayerCard.SetDBService(db);
        db.GetByIdAsync<PrayerCard>(cardId).Returns(card!);
        return db;
    }
}

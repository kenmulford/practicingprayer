using CommunityToolkit.Mvvm.Input;
using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class PrayerTimeCardSelectViewModelTests
{
    private readonly IBoxService _boxService = Substitute.For<IBoxService>();
    private readonly ICardService _cardService = Substitute.For<ICardService>();
    private readonly IPrayerService _prayerService = Substitute.For<IPrayerService>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
    private readonly IAccessibilityService _accessibilityService = Substitute.For<IAccessibilityService>();
    private readonly IPrayerSelectionService _selectionService = Substitute.For<IPrayerSelectionService>();
    private readonly ISettings _settings = Substitute.For<ISettings>();

    private const int ArchivedBoxId = 999;

    public PrayerTimeCardSelectViewModelTests()
    {
        _settings.ArchivedFolderId.Returns(ArchivedBoxId);
        _boxService.GetBoxesAsync().Returns(new List<CardBox>().AsReadOnly());
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>().AsReadOnly());
    }

    private PrayerTimeCardSelectViewModel CreateSut() =>
        new(_boxService, _cardService, _prayerService, _navigationService,
            _accessibilityService, _selectionService, _settings);

    private async Task<PrayerTimeCardSelectViewModel> CreateLoadedSut()
    {
        var sut = CreateSut();
        await sut.LoadCardsAsync();
        return sut;
    }

    // ── Eligibility ───────────────────────────────────────────────────

    [Fact]
    public async Task LoadCards_IncludesOnlyCardsWithActivePrayers_ExcludesSystemAndArchived()
    {
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 1, Title = "Family", BoxId = 1, IsSystem = false },
            new() { Id = 2, Title = "Empty", BoxId = 1, IsSystem = false },          // no active prayers
            new() { Id = 3, Title = "Quick Add", BoxId = 0, IsSystem = true },        // system
            new() { Id = 4, Title = "Old", BoxId = ArchivedBoxId, IsSystem = false }  // archived
        }.AsReadOnly());
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 100, PrayerCardId = 1 },
            new() { Id = 101, PrayerCardId = 3 }, // system card — excluded
            new() { Id = 102, PrayerCardId = 4 }  // archived card — excluded
        }.AsReadOnly());

        var sut = await CreateLoadedSut();

        Assert.Single(sut.Cards);
        Assert.Equal("Family", sut.Cards[0].Title);
    }

    // ── Collection filter ─────────────────────────────────────────────

    [Fact]
    public async Task CollectionFilter_AllCollections_ShowsAllEligible_SpecificBox_Narrows()
    {
        _boxService.GetBoxesAsync().Returns(new List<CardBox>
        {
            new() { Id = 1, Name = "Family", IsSystem = false },
            new() { Id = 2, Name = "Work", IsSystem = false }
        }.AsReadOnly());
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 10, Title = "Mom", BoxId = 1 },
            new() { Id = 20, Title = "Project", BoxId = 2 }
        }.AsReadOnly());
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 100, PrayerCardId = 10 },
            new() { Id = 200, PrayerCardId = 20 }
        }.AsReadOnly());

        var sut = await CreateLoadedSut();

        // Default = All collections → both cards
        Assert.Equal(2, sut.Cards.Count);

        // Picker offers All + the two real boxes
        Assert.Contains(sut.AvailableBoxes, b => b is AllCollectionsPickerItem);
        var workBox = sut.AvailableBoxes.OfType<RealBoxPickerItem>().First(b => b.BoxId == 2);

        sut.SelectedBox = workBox;

        Assert.Single(sut.Cards);
        Assert.Equal("Project", sut.Cards[0].Title);
    }

    [Fact]
    public async Task CollectionFilter_BoxWithNoEligibleCards_NotInPicker()
    {
        _boxService.GetBoxesAsync().Returns(new List<CardBox>
        {
            new() { Id = 1, Name = "Family", IsSystem = false },
            new() { Id = 2, Name = "Empty", IsSystem = false }
        }.AsReadOnly());
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 10, Title = "Mom", BoxId = 1 },
            new() { Id = 20, Title = "NoPrayers", BoxId = 2 }
        }.AsReadOnly());
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 100, PrayerCardId = 10 }
        }.AsReadOnly());

        var sut = await CreateLoadedSut();

        Assert.DoesNotContain(sut.AvailableBoxes.OfType<RealBoxPickerItem>(), b => b.BoxId == 2);
    }

    // ── Empty state ───────────────────────────────────────────────────

    [Fact]
    public async Task LoadCards_NoEligibleCards_IsEmptyTrue()
    {
        var sut = await CreateLoadedSut();

        Assert.True(sut.IsEmpty);
        Assert.Empty(sut.Cards);
    }

    // ── Selection accumulation ────────────────────────────────────────

    [Fact]
    public async Task Selection_Accumulates_DrivesCountAndStartEnable()
    {
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 10, Title = "A", BoxId = 0 },
            new() { Id = 20, Title = "B", BoxId = 0 }
        }.AsReadOnly());
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 100, PrayerCardId = 10 },
            new() { Id = 200, PrayerCardId = 20 }
        }.AsReadOnly());

        var sut = await CreateLoadedSut();

        Assert.False(((IAsyncRelayCommand)sut.StartCommand).CanExecute(null));
        Assert.Equal(0, sut.SelectedCount);

        sut.Cards[0].IsSelected = true;
        Assert.Equal(1, sut.SelectedCount);
        Assert.Contains("(1)", sut.StartButtonText);

        sut.Cards[1].IsSelected = true;
        Assert.Equal(2, sut.SelectedCount);
        Assert.True(((IAsyncRelayCommand)sut.StartCommand).CanExecute(null));
    }

    // ── Accessibility ─────────────────────────────────────────────────

    [Fact]
    public async Task Selection_Toggle_AnnouncesSelectedCount()
    {
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 10, Title = "A", BoxId = 0 },
            new() { Id = 20, Title = "B", BoxId = 0 }
        }.AsReadOnly());
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 100, PrayerCardId = 10 },
            new() { Id = 200, PrayerCardId = 20 }
        }.AsReadOnly());

        var sut = await CreateLoadedSut();

        // Initial load resets the count to 0 — it must NOT announce on cold load.
        _accessibilityService.DidNotReceive().Announce(Arg.Any<string>());

        sut.Cards[0].IsSelected = true;
        _accessibilityService.Received(1).Announce("1 selected");

        sut.Cards[1].IsSelected = true;
        _accessibilityService.Received(1).Announce("2 selected");
    }

    // ── Start ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Start_UnionsActivePrayerIds_SetsSelection_NavigatesScopeSelection()
    {
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 10, Title = "A", BoxId = 0 },
            new() { Id = 20, Title = "B", BoxId = 0 }
        }.AsReadOnly());
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 100, PrayerCardId = 10 },
            new() { Id = 101, PrayerCardId = 10 },
            new() { Id = 200, PrayerCardId = 20 }
        }.AsReadOnly());

        var sut = await CreateLoadedSut();
        sut.Cards.First(c => c.Title == "A").IsSelected = true;
        sut.Cards.First(c => c.Title == "B").IsSelected = true;

        await ((IAsyncRelayCommand)sut.StartCommand).ExecuteAsync(null);

        _selectionService.Received(1).Set(Arg.Is<IEnumerable<int>>(
            ids => ids.OrderBy(x => x).SequenceEqual(new[] { 100, 101, 200 })));
        await _navigationService.Received(1).PopModalAsync();
        await _navigationService.Received(1).GoToAsync(
            $"{Routes.PrayerTimePage}?scope={Routes.ScopeSelection}");
    }

    [Fact]
    public async Task Start_NoneSelected_DoesNotSetOrNavigate()
    {
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 10, Title = "A", BoxId = 0 }
        }.AsReadOnly());
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 100, PrayerCardId = 10 }
        }.AsReadOnly());

        var sut = await CreateLoadedSut();

        // Force the command body even though canExecute is false.
        await ((IAsyncRelayCommand)sut.StartCommand).ExecuteAsync(null);

        _selectionService.DidNotReceive().Set(Arg.Any<IEnumerable<int>>());
        await _navigationService.DidNotReceive().GoToAsync(Arg.Any<string>());
    }

    // ── Cancel ────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_PopsModal()
    {
        var sut = CreateSut();

        await sut.CancelAsync();

        await _navigationService.Received(1).PopModalAsync();
    }
}

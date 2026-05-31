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
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
    private readonly IAccessibilityService _accessibilityService = Substitute.For<IAccessibilityService>();
    private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
    private readonly ISettings _settings = Substitute.For<ISettings>();

    public PrayerTimeViewModelTests()
    {
        _settings.AutoModeIntervalSeconds.Returns(30);
    }

    private PrayerTimeViewModel CreateSut() =>
        new(_prayerService, _cardService, _tagService, _interactionService,
            _navigationService, _accessibilityService, _notificationService, _settings);

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
    public async Task EndSessionCommand_NavigatesBack()
    {
        var sut = CreateSut();

        await ((IAsyncRelayCommand)sut.EndSessionCommand).ExecuteAsync(null);

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

    // ── scope=box filtering ──────────────────────────────────────────

    [Fact]
    public async Task ApplyQueryAttributes_BoxScope_FiltersToBoxCards()
    {
        // Card 1 is in box 5, Card 2 is in a different box
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 1, Title = "Family", BoxId = 5 },
            new() { Id = 2, Title = "Work", BoxId = 10 }
        }.AsReadOnly());

        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 100, Title = "Prayer A", PrayerCardId = 1 },
            new() { Id = 200, Title = "Prayer B", PrayerCardId = 2 },
            new() { Id = 300, Title = "Prayer C", PrayerCardId = 1 }
        }.AsReadOnly());

        var sut = CreateSut();
        sut.ApplyQueryAttributes(new Dictionary<string, object>
        {
            { "scope", "box" },
            { "boxId", "5" }
        });

        // Wait for async load to complete
        await Task.Delay(200);

        // Should include only prayers from cards in box 5 (+ completion sentinel)
        var realEntries = sut.Entries.Where(e => !e.IsSentinel).ToList();
        Assert.Equal(2, realEntries.Count);
        Assert.All(realEntries, e => Assert.Equal("Family", e.CardTitle));
    }

    // ── Ordering (BUG-61) ────────────────────────────────────────────

    [Fact]
    public async Task LoadEntries_OrdersByCardTitleThenPrayerTitle()
    {
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 1, Title = "Zebra" },
            new() { Id = 2, Title = "Alpha" }
        }.AsReadOnly());

        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 100, Title = "C Prayer", PrayerCardId = 1 },
            new() { Id = 200, Title = "A Prayer", PrayerCardId = 2 },
            new() { Id = 300, Title = "B Prayer", PrayerCardId = 2 },
            new() { Id = 400, Title = "A Prayer", PrayerCardId = 1 }
        }.AsReadOnly());

        var sut = CreateSut();
        sut.ApplyQueryAttributes(new Dictionary<string, object> { { "scope", "all" } });

        await Task.Delay(200);

        var realEntries = sut.Entries.Where(e => !e.IsSentinel).ToList();
        Assert.Equal(4, realEntries.Count);
        // Alpha card first, sorted by prayer title
        Assert.Equal("Alpha", realEntries[0].CardTitle);
        Assert.Equal("A Prayer", realEntries[0].PrayerTitle);
        Assert.Equal("Alpha", realEntries[1].CardTitle);
        Assert.Equal("B Prayer", realEntries[1].PrayerTitle);
        // Then Zebra card
        Assert.Equal("Zebra", realEntries[2].CardTitle);
        Assert.Equal("A Prayer", realEntries[2].PrayerTitle);
        Assert.Equal("Zebra", realEntries[3].CardTitle);
        Assert.Equal("C Prayer", realEntries[3].PrayerTitle);
    }

    // ── scope=list filtering ─────────────────────────────────────────

    [Fact]
    public async Task ApplyQueryAttributes_ListScope_FiltersToPrayerIds()
    {
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 1, Title = "Family" },
            new() { Id = 2, Title = "Work" }
        }.AsReadOnly());

        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 100, Title = "Prayer A", PrayerCardId = 1, IsAnswered = false },
            new() { Id = 200, Title = "Prayer B", PrayerCardId = 2, IsAnswered = false },
            new() { Id = 300, Title = "Prayer C", PrayerCardId = 1, IsAnswered = true }
        }.AsReadOnly());

        var sut = CreateSut();
        sut.ApplyQueryAttributes(new Dictionary<string, object>
        {
            { "scope", "list" },
            { "prayerIds", "100,300" }
        });

        await Task.Delay(200);

        var realEntries = sut.Entries.Where(e => !e.IsSentinel).ToList();
        Assert.Equal(2, realEntries.Count);
        Assert.Contains(realEntries, e => e.PrayerId == 100);
        Assert.Contains(realEntries, e => e.PrayerId == 300);
    }
}

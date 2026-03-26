using CommunityToolkit.Mvvm.Input;
using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class PrayerListViewModelTests
{
    private readonly IPrayerService _prayerService = Substitute.For<IPrayerService>();
    private readonly ICardService _cardService = Substitute.For<ICardService>();
    private readonly ITagService _tagService = Substitute.For<ITagService>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
    private readonly IAccessibilityService _accessibilityService = Substitute.For<IAccessibilityService>();
    private readonly ISettings _settings = Substitute.For<ISettings>();

    private PrayerListViewModel CreateSut() =>
        new(_prayerService, _cardService, _tagService, _navigationService, _accessibilityService, _settings);

    // ── Construction ──────────────────────────────────────────────────

    [Fact]
    public void Constructor_DefaultState()
    {
        var sut = CreateSut();

        Assert.True(sut.IsLoading); // starts true
        Assert.Empty(sut.AllPrayers);
        Assert.Empty(sut.FilteredPrayers);
        Assert.Equal(FilterStatus.Active, sut.StatusFilter);
        Assert.Equal(string.Empty, sut.SearchText);
    }

    // ── IsLoading announces ───────────────────────────────────────────

    [Fact]
    public void IsLoading_TransitionAnnounces()
    {
        var sut = CreateSut();
        _accessibilityService.ClearReceivedCalls();

        sut.IsLoading = false;
        _accessibilityService.Received(1).Announce("Content loaded");

        sut.IsLoading = true;
        _accessibilityService.Received(1).Announce("Loading");
    }

    // ── StatusFilter ──────────────────────────────────────────────────

    [Fact]
    public void SetStatusCommand_ChangesFilter()
    {
        var sut = CreateSut();

        sut.SetStatusCommand.Execute("Answered");
        Assert.Equal(FilterStatus.Answered, sut.StatusFilter);
        Assert.True(sut.IsAnsweredSelected);
        Assert.False(sut.IsActiveSelected);

        sut.SetStatusCommand.Execute("All");
        Assert.Equal(FilterStatus.All, sut.StatusFilter);
        Assert.True(sut.IsAllSelected);

        sut.SetStatusCommand.Execute("Overdue");
        Assert.Equal(FilterStatus.Overdue, sut.StatusFilter);
        Assert.True(sut.IsOverdueSelected);

        sut.SetStatusCommand.Execute("Active");
        Assert.Equal(FilterStatus.Active, sut.StatusFilter);
        Assert.True(sut.IsActiveSelected);
    }

    // ── NewCommand ────────────────────────────────────────────────────

    [Fact]
    public async Task NewCommand_NavigatesToNewPrayer()
    {
        var sut = CreateSut();

        await ((IAsyncRelayCommand)sut.NewCommand).ExecuteAsync(null);

        await _navigationService.Received(1).GoToAsync(
            Arg.Is<string>(s => s.Contains(Routes.PrayerDetailPage) && s.Contains("new=true")));
    }

    // ── SearchText ────────────────────────────────────────────────────

    [Fact]
    public void SearchText_PropertyChanges()
    {
        var sut = CreateSut();
        var changed = false;
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PrayerListViewModel.SearchText))
                changed = true;
        };

        sut.SearchText = "test";

        Assert.True(changed);
        Assert.Equal("test", sut.SearchText);
    }

    // ── RefreshAsync invalidates caches ───────────────────────────────

    [Fact]
    public async Task RefreshAsync_InvalidatesPrayerCache()
    {
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _settings.OverdueDayThreshold.Returns(30);
        _prayerService.GetOverduePrayersAsync(30).Returns(new List<Prayer>().AsReadOnly());
        _db_Setup();

        var sut = CreateSut();
        await sut.RefreshAsync();

        _prayerService.Received(1).InvalidateCache();
    }

    // ── HasTags ───────────────────────────────────────────────────────

    [Fact]
    public void HasTags_InitiallyFalse()
    {
        var sut = CreateSut();
        Assert.False(sut.HasTags);
    }

    // Helper to set up DB mocks needed for RefreshAsync
    private void _db_Setup()
    {
        var db = Substitute.For<IDBService>();
        PrayerCardTag.SetDBService(db);
        db.GetAllAsync<PrayerCardTag>().Returns(new List<PrayerCardTag>());
    }
}

using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NSubstitute;
using PrayerApp.Messages;
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
    private readonly IMessenger _messenger = new WeakReferenceMessenger();

    private PrayerListViewModel CreateSut() =>
        new(_prayerService, _cardService, _tagService, _navigationService, _accessibilityService, _settings, _messenger);

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

    // ── SyncAsync no longer invalidates service cache ─────────────────
    // Slice 3: services auto-invalidate on mutation (Slice 2). VMs trust the cache.

    [Fact]
    public async Task SyncAsync_DoesNotInvalidateServiceCache()
    {
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _settings.OverdueDayThreshold.Returns(30);
        _prayerService.GetOverduePrayersAsync(30).Returns(new List<Prayer>().AsReadOnly());
        _db_Setup();

        var sut = CreateSut();
        await sut.SyncAsync();

        _prayerService.DidNotReceive().InvalidateCache();
    }

    // ── HasTags ───────────────────────────────────────────────────────

    [Fact]
    public void HasTags_InitiallyFalse()
    {
        var sut = CreateSut();
        Assert.False(sut.HasTags);
    }

    // Helper to set up DB mocks needed for SyncAsync (PrayerCardTag.LoadAllAsync)
    private void _db_Setup()
    {
        var db = Substitute.For<IDBService>();
        PrayerCardTag.SetDBService(db);
        db.GetAllAsync<PrayerCardTag>().Returns(new List<PrayerCardTag>());
    }

    // ── Slice 6a — single-flight + coalesce-pending SyncAsync ─────────

    [Fact]
    public async Task SyncAsync_BurstOfThreeConcurrent_CoalescesToTwoFetches()
    {
        // See PrayerCardsViewModelTests for full context. Same coalesce contract.
        var gate = new TaskCompletionSource<IReadOnlyList<PrayerCard>>();
        _cardService.GetCardsAsync().Returns(gate.Task);
        _prayerService.GetAllPrayersAsync().Returns(new List<Prayer>().AsReadOnly());
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        _settings.OverdueDayThreshold.Returns(30);
        _prayerService.GetOverduePrayersAsync(30).Returns(new List<Prayer>().AsReadOnly());
        _db_Setup();

        var sut = CreateSut();

        var t1 = sut.SyncAsync();
        var t2 = sut.SyncAsync();
        var t3 = sut.SyncAsync();

        gate.SetResult(new List<PrayerCard>().AsReadOnly());
        await Task.WhenAll(t1, t2, t3);

        await _cardService.Received(2).GetCardsAsync();
    }
}

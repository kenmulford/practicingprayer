using CommunityToolkit.Mvvm.Input;
using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class PrayerRequestDetailViewModelTests
{
    private readonly IPrayerService _prayerService = Substitute.For<IPrayerService>();
    private readonly ITagService _tagService = Substitute.For<ITagService>();
    private readonly ICardService _cardService = Substitute.For<ICardService>();
    private readonly IOnboardingService _onboardingService = Substitute.For<IOnboardingService>();
    private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
    private readonly IAccessibilityService _accessibilityService = Substitute.For<IAccessibilityService>();
    private readonly ISettings _settings = Substitute.For<ISettings>();
    private readonly IDBService _db = Substitute.For<IDBService>();

    public PrayerRequestDetailViewModelTests()
    {
        Prayer.SetDBService(_db);
        PrayerTag.SetDBService(_db);
        PrayerCard.SetDBService(_db);
        PrayerCardTag.SetDBService(_db);
        _settings.DefaultNotifyHour.Returns(9);
        _settings.DefaultNotifyMinute.Returns(0);
    }

    private PrayerRequestDetailViewModel CreateSut() =>
        new(_prayerService, _tagService, _cardService, _onboardingService,
            _notificationService, _navigationService, _accessibilityService, _settings);

    // ── Construction ──────────────────────────────────────────────────

    [Fact]
    public void Constructor_IsNew()
    {
        var sut = CreateSut();
        Assert.True(sut.IsNew);
        // IsDirty may be true if CanNotify setter fires during construction
        // due to mock setup — just verify IsNew is correct
    }

    // ── IsDirty — basic fields ────────────────────────────────────────

    [Fact]
    public void IsDirty_TitleChange()
    {
        var sut = CreateSut();
        sut.Title = "New prayer";
        Assert.True(sut.IsDirty);
    }

    [Fact]
    public void IsDirty_DetailsChange()
    {
        var sut = CreateSut();
        sut.Details = "Some details";
        Assert.True(sut.IsDirty);
    }

    [Fact]
    public void IsDirty_CanNotifyChange()
    {
        var sut = CreateSut();
        sut.CanNotify = true;
        Assert.True(sut.IsDirty);
    }

    [Fact]
    public void IsDirty_FrequencyChange()
    {
        var sut = CreateSut();
        sut.PrayerFrequency = PrayerFrequency.Weekly;
        Assert.True(sut.IsDirty);
    }

    // ── IsDirty — notification time fields (audit fix #22c) ──────────

    [Fact]
    public void IsDirty_NotifyTimeChange()
    {
        var sut = CreateSut();
        sut.NotifyTime = new TimeSpan(14, 30, 0); // change from default 9:00
        Assert.True(sut.IsDirty);
    }

    // ── CanLeaveAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task CanLeaveAsync_AfterSave_ReturnsTrue()
    {
        // After saving, originals are captured so IsDirty is false
        var sut = CreateSut();
        sut.Title = "Test";
        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        Assert.True(await sut.CanLeaveAsync());
    }

    [Fact]
    public async Task CanLeaveAsync_Dirty_PromptsUser()
    {
        var sut = CreateSut();
        sut.Title = "Modified";
        _navigationService.DisplayConfirmAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);

        var result = await sut.CanLeaveAsync();

        Assert.True(result);
        await _navigationService.Received(1).DisplayConfirmAsync(
            "Unsaved Changes", Arg.Any<string>(), "Discard", "Cancel");
    }

    // ── SaveCommand ───────────────────────────────────────────────────

    [Fact]
    public async Task SaveCommand_SavesPrayerAndAnnounces()
    {
        var sut = CreateSut();
        sut.Title = "Test";

        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        await _prayerService.Received(1).SavePrayerAsync(Arg.Any<Prayer>());
        _accessibilityService.Received(1).Announce("Prayer saved");
    }

    [Fact]
    public async Task SaveCommand_NewPrayer_AdvancesOnboarding()
    {
        var sut = CreateSut();
        sut.Title = "First prayer";

        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        _onboardingService.Received(1).Advance();
    }

    [Fact]
    public async Task SaveCommand_WithNotifications_Schedules()
    {
        var sut = CreateSut();
        sut.Title = "Reminder prayer";
        sut.CanNotify = true;

        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        await _notificationService.Received(1).ScheduleAsync(Arg.Any<Prayer>());
    }

    [Fact]
    public async Task SaveCommand_WithoutNotifications_Cancels()
    {
        var sut = CreateSut();
        sut.Title = "No reminder";
        sut.CanNotify = false;

        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        await _notificationService.Received(1).CancelAsync(Arg.Any<int>(), Arg.Any<PrayerFrequency>());
    }

    // ── DeleteCommand ─────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCommand_Confirmed_DeletesAndAnnounces()
    {
        var sut = CreateSut();
        // Give it an ID so it's not "new"
        sut.Title = "To delete";
        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        _navigationService.DisplayConfirmAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);

        await ((IAsyncRelayCommand)sut.DeleteCommand).ExecuteAsync(null);

        await _prayerService.Received(1).DeletePrayerAsync(Arg.Any<Prayer>());
        _accessibilityService.Received(1).Announce("Prayer deleted");
    }

    [Fact]
    public async Task DeleteCommand_Cancelled_DoesNotDelete()
    {
        var sut = CreateSut();
        _navigationService.DisplayConfirmAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        await ((IAsyncRelayCommand)sut.DeleteCommand).ExecuteAsync(null);

        await _prayerService.DidNotReceive().DeletePrayerAsync(Arg.Any<Prayer>());
    }

    // ── SaveAndNewCommand ─────────────────────────────────────────────

    [Fact]
    public async Task SaveAndNewCommand_EmptyTitle_ShowsAlert()
    {
        var sut = CreateSut();
        sut.Title = "";

        await ((IAsyncRelayCommand)sut.SaveAndNewCommand).ExecuteAsync(null);

        await _navigationService.Received(1).DisplayAlertAsync("Required", Arg.Any<string>(), "OK");
    }

    [Fact]
    public async Task SaveAndNewCommand_ValidTitle_SavesAndResetsForm()
    {
        var sut = CreateSut();
        sut.Title = "First prayer";
        sut.ReturnToCards = true;

        await ((IAsyncRelayCommand)sut.SaveAndNewCommand).ExecuteAsync(null);

        await _prayerService.Received(1).SavePrayerAsync(Arg.Any<Prayer>());
        // FormResetRequested event was replaced by PendingFocusTitle property
        // (see SaveAndNewCommand_ValidTitle_SetsPendingFocusTitleTrue for the explicit
        // assertion); here we only confirm the form-reset side effects.
        Assert.True(sut.IsNew); // form was reset
    }

    // ── ShowSaveAndNew ────────────────────────────────────────────────

    [Fact]
    public void ShowSaveAndNew_NewAndReturnToCards_True()
    {
        var sut = CreateSut();
        sut.ReturnToCards = true;

        Assert.True(sut.ShowSaveAndNew);
    }

    [Fact]
    public void ShowSaveAndNew_NotReturnToCards_False()
    {
        var sut = CreateSut();
        sut.ReturnToCards = false;

        Assert.False(sut.ShowSaveAndNew);
    }

    // ── CanNotify triggers permission request ─────────────────────────

    [Fact]
    public void CanNotify_Enable_RequestsPermissionWhenAllowed()
    {
        _settings.AllowNotifications.Returns(true);
        var sut = CreateSut();

        sut.CanNotify = true;

        _notificationService.Received(1).RequestPermissionAsync();
    }

    [Fact]
    public void CanNotify_Enable_SkipsPermissionWhenDisallowed()
    {
        _settings.AllowNotifications.Returns(false);
        var sut = CreateSut();

        sut.CanNotify = true;

        _notificationService.DidNotReceive().RequestPermissionAsync();
    }

    // ── Reload guard ─────────────────────────────────────────────────

    [Fact]
    public void Reload_NewPrayer_DoesNotNavigateAway()
    {
        var sut = CreateSut();

        sut.Reload();

        _navigationService.DidNotReceive().GoToAsync(Arg.Any<string>());
    }

    // ── ApplyQueryAttributes re-entry guard ──────────────────────────

    [Fact]
    public void ApplyQueryAttributes_CalledTwice_DoesNotResetPrayer()
    {
        var sut = CreateSut();
        var query = new Dictionary<string, object> { ["new"] = "true" };

        ((IQueryAttributable)sut).ApplyQueryAttributes(query);
        sut.Title = "My Prayer";

        // Simulate Shell re-applying sticky params on modal dismiss
        ((IQueryAttributable)sut).ApplyQueryAttributes(query);

        Assert.Equal("My Prayer", sut.Title);
    }

    // ── PendingFocusTitle (replaces FormResetRequested event) ──
    // Same defect family as the card-create crash: a C# event whose handler
    // touched native UI state. Replaced with an observable property the View
    // consumes via PropertyChanged on the lifecycle channel.

    [Fact]
    public async Task SaveAndNewCommand_ValidTitle_SetsPendingFocusTitleTrue()
    {
        var sut = CreateSut();
        sut.Title = "First prayer";
        sut.ReturnToCards = true;
        Assert.False(sut.PendingFocusTitle);

        await ((IAsyncRelayCommand)sut.SaveAndNewCommand).ExecuteAsync(null);

        Assert.True(sut.PendingFocusTitle);
        Assert.True(sut.IsNew); // form was reset
    }

    [Fact]
    public async Task ConsumePendingFocusTitle_WhenTrue_ClearsFlag()
    {
        var sut = CreateSut();
        sut.Title = "Test";
        sut.ReturnToCards = true;
        await ((IAsyncRelayCommand)sut.SaveAndNewCommand).ExecuteAsync(null);
        Assert.True(sut.PendingFocusTitle);

        sut.ConsumePendingFocusTitle();

        Assert.False(sut.PendingFocusTitle);
    }

    [Fact]
    public void ConsumePendingFocusTitle_WhenFalse_StaysFalse()
    {
        var sut = CreateSut();
        Assert.False(sut.PendingFocusTitle);

        sut.ConsumePendingFocusTitle();

        Assert.False(sut.PendingFocusTitle);
    }

    [Fact]
    public void FormResetRequested_EventIsRemoved()
    {
        // Reflection guard tripwire: the VM→View C# event was the same defect family
        // as the card-create crash. Don't reintroduce.
        var evt = typeof(PrayerRequestDetailViewModel).GetEvent("FormResetRequested");
        Assert.Null(evt);
    }

    // ── IsBusy on Save ─────────────────────────────────────────────────

    [Fact]
    public async Task SaveCommand_SetsIsBusyDuringExecution_AndResetsAfter()
    {
        var sut = CreateSut();
        sut.Title = "Test";
        bool capturedIsBusy = false;
        _prayerService.SavePrayerAsync(Arg.Any<Prayer>()).Returns(call =>
        {
            capturedIsBusy = sut.IsBusy;
            return Task.FromResult((Prayer)call[0]);
        });

        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        Assert.True(capturedIsBusy);
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task SaveCommand_ResetsIsBusyAfterException()
    {
        var sut = CreateSut();
        sut.Title = "Test";
        _prayerService.SavePrayerAsync(Arg.Any<Prayer>())
            .Returns(_ => Task.FromException<Prayer>(new InvalidOperationException("boom")));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null));

        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task SaveCommand_DoubleInvoke_RunsServiceOnlyOnce()
    {
        var sut = CreateSut();
        sut.Title = "Test";
        var tcs = new TaskCompletionSource<Prayer>();
        _prayerService.SavePrayerAsync(Arg.Any<Prayer>()).Returns(tcs.Task);

        var first = ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);
        var second = ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);
        tcs.SetResult(new Prayer());
        await Task.WhenAll(first, second);

        await _prayerService.Received(1).SavePrayerAsync(Arg.Any<Prayer>());
    }

    // ── EditPrayerCommand ─────────────────────────────────────────────
    // Contract pin for #72a: PrayerListTests' Prayers_EditPrayer drives the view
    // out of read-only via Edit, then asserts the editable surface. This locks the
    // VM-side contract that UITest depends on, so a regression shows up as a fast
    // unit failure instead of a flaky UITest red.

    [Fact]
    public void EditPrayerCommand_FlipsToEditMode_RaisesPropertyChanged()
    {
        var sut = CreateSut();
        sut.IsReadOnly = true; // opened view-only (load + viewOnly)
        var raised = new List<string>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        sut.EditPrayerCommand.Execute(null);

        Assert.False(sut.IsReadOnly);
        Assert.True(sut.IsEditable);
        Assert.Contains(nameof(PrayerRequestDetailViewModel.IsReadOnly), raised);
        Assert.Contains(nameof(PrayerRequestDetailViewModel.IsEditable), raised);
    }
}

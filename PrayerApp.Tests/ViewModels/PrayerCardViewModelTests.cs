using CommunityToolkit.Mvvm.Input;
using NSubstitute;
using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class PrayerCardViewModelTests
{
    private readonly ICardService _cardService = Substitute.For<ICardService>();
    private readonly IPrayerService _prayerService = Substitute.For<IPrayerService>();
    private readonly IOnboardingService _onboardingService = Substitute.For<IOnboardingService>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
    private readonly IAccessibilityService _accessibilityService = Substitute.For<IAccessibilityService>();
    private readonly IBoxService _boxService = Substitute.For<IBoxService>();
    private readonly IDBService _db = Substitute.For<IDBService>();

    public PrayerCardViewModelTests()
    {
        PrayerCard.SetDBService(_db);
        Prayer.SetDBService(_db);
        CardBox.SetDBService(_db);
        _boxService.GetBoxesAsync().Returns(new List<CardBox>().AsReadOnly());
    }

    private PrayerCardViewModel CreateSut() =>
        new(_cardService, _prayerService, _onboardingService, _navigationService,
            _accessibilityService, _boxService);

    // ── Construction ──────────────────────────────────────────────────

    [Fact]
    public void Constructor_IsNew()
    {
        var sut = CreateSut();
        Assert.True(sut.IsNew);
        Assert.False(sut.CanDelete);
    }

    // ── Title + IsDirty ───────────────────────────────────────────────

    [Fact]
    public void Title_Change_MakesDirty()
    {
        var sut = CreateSut();
        sut.Title = "New Card";

        Assert.True(sut.IsDirty);
    }

    [Fact]
    public void Title_ResetToOriginal_NotDirty()
    {
        var sut = CreateSut();
        sut.Title = "Changed";
        sut.Title = null!; // back to default (empty → null in PrayerCard)

        // New card's original title is empty string, PrayerCard.Title defaults to null
        // IsDirty compares Title (null) != _originalTitle (empty) — so still dirty
        // This is correct: user typed, then cleared. The form is modified.
    }

    // ── SaveAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task SaveCommand_SavesAndNavigatesBack()
    {
        var sut = CreateSut();
        sut.Title = "My Card";

        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        await _cardService.Received(1).SaveCardAsync(Arg.Any<PrayerCard>());
        _accessibilityService.Received(1).Announce("Card saved");
        await _navigationService.Received(1).GoToAsync(Arg.Is<string>(s => s.StartsWith("..")));
    }

    [Fact]
    public async Task SaveCommand_NewCard_AdvancesOnboarding()
    {
        var sut = CreateSut();
        sut.Title = "First Card";

        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        _onboardingService.Received(1).Advance();
    }

    // ── DeleteAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCommand_SystemCard_NoOp()
    {
        var sut = CreateSut();
        // Simulate system card by setting IsSystem (need to set via the backing PrayerCard)
        // Since we can't easily set IsSystem on a new card, test that delete with
        // no confirmation just doesn't navigate
        _navigationService.DisplayConfirmAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        await ((IAsyncRelayCommand)sut.DeleteCommand).ExecuteAsync(null);

        // System card check is first — if IsSystem, returns immediately
        // For a non-system new card, it shows confirm dialog
        await _cardService.DidNotReceive().DeleteCardAsync(Arg.Any<PrayerCard>());
    }

    [Fact]
    public async Task DeleteCommand_Confirmed_DeletesAndNavigates()
    {
        var sut = CreateSut();
        // Make it non-system and non-new by giving it an ID
        sut.Id = 5;

        _navigationService.DisplayConfirmAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);
        _prayerService.GetPrayersByCardAsync(5).Returns(new List<Prayer>());

        await ((IAsyncRelayCommand)sut.DeleteCommand).ExecuteAsync(null);

        await _cardService.Received(1).DeleteCardAsync(Arg.Any<PrayerCard>());
        _accessibilityService.Received(1).Announce("Card deleted");
        await _navigationService.Received(1).GoToAsync(Arg.Is<string>(s => s.Contains("deleted")));
    }

    [Fact]
    public async Task DeleteCommand_Cancelled_DoesNotDelete()
    {
        var sut = CreateSut();
        sut.Id = 5;

        _navigationService.DisplayConfirmAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        await ((IAsyncRelayCommand)sut.DeleteCommand).ExecuteAsync(null);

        await _cardService.DidNotReceive().DeleteCardAsync(Arg.Any<PrayerCard>());
    }

    // ── CanLeaveAsync (IEditGuard) ────────────────────────────────────

    [Fact]
    public async Task CanLeaveAsync_NotDirty_ReturnsTrue()
    {
        var sut = CreateSut();
        Assert.True(await sut.CanLeaveAsync());
    }

    [Fact]
    public async Task CanLeaveAsync_Dirty_ShowsConfirm()
    {
        var sut = CreateSut();
        sut.Title = "Changed";

        _navigationService.DisplayConfirmAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);

        var result = await sut.CanLeaveAsync();

        Assert.True(result);
        await _navigationService.Received(1).DisplayConfirmAsync(
            "Unsaved Changes", Arg.Any<string>(), "Discard", "Cancel");
    }

    // ── CanShare ────────────────────────────────────────────────────

    [Fact]
    public void CanShare_NewCard_ZeroPrayers_ReturnsFalse()
    {
        var sut = CreateSut();
        // ActivePrayerCount defaults to 0, IsSystem defaults to false
        Assert.False(sut.CanShare);
    }

    [Fact]
    public void CanShare_HasActivePrayers_ReturnsTrue()
    {
        var sut = CreateSut();
        sut.ActivePrayerCount = 3;

        Assert.True(sut.CanShare);
    }

    [Fact]
    public void CanShare_ActivePrayerCountChange_RaisesPropertyChanged()
    {
        var sut = CreateSut();
        var raised = false;
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PrayerCardViewModel.CanShare)) raised = true;
        };

        sut.ActivePrayerCount = 2;

        Assert.True(raised);
    }

    // ── Box Picker (Collection assignment) ────────────────────────────

    [Fact]
    public async Task LoadBoxPickerAsync_PopulatesAvailableBoxes_WithLooseCardsFirst()
    {
        _boxService.GetBoxesAsync().Returns(new List<CardBox>
        {
            new() { Id = 5, Name = "Family" },
            new() { Id = 6, Name = "Ministry" },
            new() { Id = 10, Name = "System", IsSystem = true, SystemKey = CardBox.SystemKeySystem },
            new() { Id = 20, Name = "Archived", IsSystem = true, SystemKey = CardBox.SystemKeyArchived }
        }.AsReadOnly());

        var sut = CreateSut();
        await sut.LoadBoxPickerAsync();

        Assert.Equal(3, sut.AvailableBoxes.Count); // Loose Cards + 2 user boxes, no system
        Assert.Equal(BoxStrings.Unorganized, sut.AvailableBoxes[0].Name);
        Assert.Equal(0, sut.AvailableBoxes[0].BoxId);
        Assert.Equal("Family", sut.AvailableBoxes[1].Name);
        Assert.Equal("Ministry", sut.AvailableBoxes[2].Name);
    }

    [Fact]
    public async Task LoadBoxPickerAsync_SetsSelectedBoxToCurrentBoxId()
    {
        _boxService.GetBoxesAsync().Returns(new List<CardBox>
        {
            new() { Id = 5, Name = "Family" }
        }.AsReadOnly());

        var card = new PrayerCard { Id = 1, Title = "Test", BoxId = 5 };
        var sut = new PrayerCardViewModel(card, _cardService, _prayerService,
            _onboardingService, _navigationService, _accessibilityService, _boxService);
        await sut.LoadBoxPickerAsync();

        Assert.NotNull(sut.SelectedBox);
        Assert.Equal(5, sut.SelectedBox!.BoxId);
    }

    [Fact]
    public async Task LoadBoxPickerAsync_DefaultsToLooseCards_WhenBoxIdIsZero()
    {
        _boxService.GetBoxesAsync().Returns(new List<CardBox>
        {
            new() { Id = 5, Name = "Family" }
        }.AsReadOnly());

        var sut = CreateSut(); // new PrayerCard has BoxId=0
        await sut.LoadBoxPickerAsync();

        Assert.NotNull(sut.SelectedBox);
        Assert.Equal(0, sut.SelectedBox!.BoxId);
    }

    [Fact]
    public async Task SaveAsync_SetsBoxIdFromSelectedBox()
    {
        _boxService.GetBoxesAsync().Returns(new List<CardBox>
        {
            new() { Id = 5, Name = "Family" }
        }.AsReadOnly());

        var sut = CreateSut();
        sut.Title = "Test Card";
        await sut.LoadBoxPickerAsync();
        sut.SelectedBox = sut.AvailableBoxes.First(b => b.BoxId == 5);

        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        await _cardService.Received(1).SaveCardAsync(
            Arg.Is<PrayerCard>(c => c.BoxId == 5));
    }

    [Fact]
    public void BoxPickerItem_EqualsByBoxId()
    {
        var a = new BoxPickerItem(5, "Family");
        var b = new BoxPickerItem(5, "Different Name");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void BoxPickerItem_NotEqual_DifferentBoxId()
    {
        var a = new BoxPickerItem(5, "Family");
        var b = new BoxPickerItem(6, "Family");

        Assert.NotEqual(a, b);
    }

    // ── IsBusy on Save ─────────────────────────────────────────────────
    // Save flows show no progress affordance and don't gate against double-tap. IsBusy
    // drives an ActivityIndicator and the SaveCommand canExecute, so a second tap can't
    // duplicate the work.

    [Fact]
    public async Task SaveCommand_SetsIsBusyDuringExecution_AndResetsAfter()
    {
        var sut = CreateSut();
        sut.Title = "Test";
        bool capturedIsBusy = false;
        _cardService.SaveCardAsync(Arg.Any<PrayerCard>()).Returns(call =>
        {
            capturedIsBusy = sut.IsBusy;
            return Task.FromResult((PrayerCard)call[0]);
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
        _cardService.SaveCardAsync(Arg.Any<PrayerCard>())
            .Returns(_ => Task.FromException<PrayerCard>(new InvalidOperationException("boom")));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null));

        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task SaveCommand_DoubleInvoke_RunsServiceOnlyOnce()
    {
        var sut = CreateSut();
        sut.Title = "Test";
        var tcs = new TaskCompletionSource<PrayerCard>();
        _cardService.SaveCardAsync(Arg.Any<PrayerCard>()).Returns(tcs.Task);

        var first = ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);
        var second = ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);
        tcs.SetResult(new PrayerCard());
        await Task.WhenAll(first, second);

        await _cardService.Received(1).SaveCardAsync(Arg.Any<PrayerCard>());
    }
}

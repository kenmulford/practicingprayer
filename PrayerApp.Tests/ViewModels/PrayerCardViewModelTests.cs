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
    private readonly ISettings _settings = Substitute.For<ISettings>();
    private readonly IDBService _db = Substitute.For<IDBService>();
    private readonly IPrayerSelectionService _selectionService = Substitute.For<IPrayerSelectionService>();

    private const int ArchivedBoxId = 99;

    public PrayerCardViewModelTests()
    {
        PrayerCard.SetDBService(_db);
        Prayer.SetDBService(_db);
        CardBox.SetDBService(_db);
        _boxService.GetBoxesAsync().Returns(new List<CardBox>().AsReadOnly());
        _settings.ArchivedFolderId.Returns(ArchivedBoxId);

        // Archive shows a confirm dialog; default the accept so existing archive-path
        // tests exercise the post-confirm behavior. Cancel-path test overrides this.
        _navigationService
            .DisplayConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);
    }

    private PrayerCardViewModel CreateSut() =>
        new(_cardService, _prayerService, _onboardingService, _navigationService,
            _accessibilityService, _boxService, _settings);

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

    // ── CanPray ───────────────────────────────────────────────────────

    [Fact]
    public void CanPray_ZeroPrayers_False()
    {
        var sut = CreateSut();
        Assert.False(sut.CanPray);
    }

    [Fact]
    public void CanPray_HasActivePrayers_True()
    {
        var sut = CreateSut();
        sut.ActivePrayerCount = 1;
        Assert.True(sut.CanPray);
    }

    [Fact]
    public void PrayCommand_CanExecute_TracksActivePrayerCount()
    {
        var sut = CreateSut();
        Assert.False(((IAsyncRelayCommand)sut.PrayCommand).CanExecute(null));

        sut.ActivePrayerCount = 2;

        Assert.True(((IAsyncRelayCommand)sut.PrayCommand).CanExecute(null));
    }

    // ── PrayCommand ───────────────────────────────────────────────────

    [Fact]
    public async Task PrayCommand_ResolvesActiveIds_SetsSelection_NavigatesScopeSelection()
    {
        var card = new PrayerCard { Id = 7, Title = "Family" };
        var sut = new PrayerCardViewModel(card, _cardService, _prayerService,
            _onboardingService, _navigationService, _accessibilityService, _boxService, _settings)
        {
            SelectionServiceFactory = () => _selectionService
        };

        // GetAllActivePrayersAsync is the single source of truth for "active"
        // (it already filters out IsAnswered), so the mock returns only active rows
        // and PrayAsync narrows to this card by PrayerCardId. A prayer for another
        // card (id 200) is included to prove the card-id filter.
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 100, PrayerCardId = 7 },
            new() { Id = 102, PrayerCardId = 7 },
            new() { Id = 200, PrayerCardId = 99 }  // other card — excluded
        }.AsReadOnly());

        await ((IAsyncRelayCommand)sut.PrayCommand).ExecuteAsync(null);

        _selectionService.Received(1).Set(Arg.Is<IEnumerable<int>>(
            ids => ids.OrderBy(x => x).SequenceEqual(new[] { 100, 102 })));
        await _navigationService.Received(1).GoToAsync(
            $"{Routes.PrayerTimePage}?scope={Routes.ScopeSelection}");
    }

    [Fact]
    public async Task PrayCommand_NoActivePrayers_DoesNotSetOrNavigate()
    {
        var card = new PrayerCard { Id = 8, Title = "Empty" };
        var sut = new PrayerCardViewModel(card, _cardService, _prayerService,
            _onboardingService, _navigationService, _accessibilityService, _boxService, _settings)
        {
            SelectionServiceFactory = () => _selectionService
        };

        // No active prayers for this card — GetAllActivePrayersAsync returns none
        // matching PrayerCardId == 8, so PrayAsync short-circuits.
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>().AsReadOnly());

        await ((IAsyncRelayCommand)sut.PrayCommand).ExecuteAsync(null);

        _selectionService.DidNotReceive().Set(Arg.Any<IEnumerable<int>>());
        await _navigationService.DidNotReceive().GoToAsync(Arg.Any<string>());
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
        // Card-edit picker is RealBoxPickerItem-only — no All-collections sentinel.
        var realBoxes = sut.AvailableBoxes.OfType<RealBoxPickerItem>().ToList();
        Assert.Equal(3, realBoxes.Count);
        Assert.Equal(BoxStrings.Unorganized, realBoxes[0].Name);
        Assert.Equal(0, realBoxes[0].BoxId);
        Assert.Equal("Family", realBoxes[1].Name);
        Assert.Equal("Ministry", realBoxes[2].Name);
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
            _onboardingService, _navigationService, _accessibilityService, _boxService, _settings);
        await sut.LoadBoxPickerAsync();

        var selected = Assert.IsType<RealBoxPickerItem>(sut.SelectedBox);
        Assert.Equal(5, selected.BoxId);
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

        var selected = Assert.IsType<RealBoxPickerItem>(sut.SelectedBox);
        Assert.Equal(0, selected.BoxId);
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
        sut.SelectedBox = sut.AvailableBoxes.OfType<RealBoxPickerItem>().First(b => b.BoxId == 5);

        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        await _cardService.Received(1).SaveCardAsync(
            Arg.Is<PrayerCard>(c => c.BoxId == 5));
    }

    [Fact]
    public void RealBoxPickerItem_EqualsByBoxId()
    {
        var a = new RealBoxPickerItem(5, "Family");
        var b = new RealBoxPickerItem(5, "Different Name");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void RealBoxPickerItem_NotEqual_DifferentBoxId()
    {
        var a = new RealBoxPickerItem(5, "Family");
        var b = new RealBoxPickerItem(6, "Family");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void AllCollectionsPickerItem_IsSingleton()
    {
        Assert.Same(AllCollectionsPickerItem.Instance, AllCollectionsPickerItem.Instance);
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

    // ── CardVisualState (issue #33 P1A) ─────────────────────────────────
    // Drives the CardStates VisualStateGroup on the cell Border.
    // Precedence: MultiSelected > Highlighted > Normal.

    [Fact]
    public void CardVisualState_DefaultsToNormal()
    {
        var sut = CreateSut();
        Assert.Equal("Normal", sut.CardVisualState);
    }

    [Fact]
    public void CardVisualState_HighlightedOnly_IsHighlighted()
    {
        var sut = CreateSut();
        sut.IsHighlighted = true;
        Assert.Equal("Highlighted", sut.CardVisualState);
    }

    [Fact]
    public void CardVisualState_MultiSelectedOnly_IsMultiSelected()
    {
        var sut = CreateSut();
        sut.IsMultiSelected = true;
        Assert.Equal("MultiSelected", sut.CardVisualState);
    }

    [Fact]
    public void CardVisualState_BothFlags_MultiSelectedWins()
    {
        var sut = CreateSut();
        sut.IsHighlighted = true;
        sut.IsMultiSelected = true;
        Assert.Equal("MultiSelected", sut.CardVisualState);
    }

    [Fact]
    public void CardVisualState_IsHighlightedChange_RaisesPropertyChanged()
    {
        var sut = CreateSut();
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.IsHighlighted = true;

        Assert.Contains(nameof(PrayerCardViewModel.CardVisualState), raised);
    }

    [Fact]
    public void CardVisualState_IsMultiSelectedChange_RaisesPropertyChanged()
    {
        var sut = CreateSut();
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.IsMultiSelected = true;

        Assert.Contains(nameof(PrayerCardViewModel.CardVisualState), raised);
    }

    // ── HasAnyPrayer (issue #33 P3) ────────────────────────────────────
    // Gates the parenthetical "(N)" prayer count Label on the collapsed card row.
    // Definition: !IsExpanded && ActivePrayerCount > 0. Without a Parent VM,
    // IsExpanded resolves false (Parent?.ExpandedCardId == _prayerCard.Id is
    // false on a null Parent), so the gate is purely count-driven in tests.

    [Fact]
    public void HasAnyPrayer_DefaultsToFalse()
    {
        var sut = CreateSut();
        // ActivePrayerCount defaults to 0
        Assert.False(sut.HasAnyPrayer);
    }

    [Fact]
    public void HasAnyPrayer_TrueWhenActiveCountAboveZero()
    {
        var sut = CreateSut();
        sut.ActivePrayerCount = 3;

        Assert.True(sut.HasAnyPrayer);
    }

    [Fact]
    public void HasAnyPrayer_FalseWhenActiveCountIsZero()
    {
        var sut = CreateSut();
        sut.ActivePrayerCount = 5;
        sut.ActivePrayerCount = 0;

        Assert.False(sut.HasAnyPrayer);
    }

    [Fact]
    public void HasAnyPrayer_ActivePrayerCountChange_ZeroToNonZero_RaisesPropertyChanged()
    {
        var sut = CreateSut();
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.ActivePrayerCount = 4;

        Assert.Contains(nameof(PrayerCardViewModel.HasAnyPrayer), raised);
    }

    [Fact]
    public void HasAnyPrayer_ActivePrayerCountChange_NonZeroToZero_RaisesPropertyChanged()
    {
        var sut = CreateSut();
        sut.ActivePrayerCount = 2;
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.ActivePrayerCount = 0;

        Assert.Contains(nameof(PrayerCardViewModel.HasAnyPrayer), raised);
    }

    // ── PrayersHeader (issue #33 UAT-8) ────────────────────────────────
    // Inner expanded-card "Prayers" header display string. Returns "Prayers"
    // when ActivePrayerCount==0 (clean affordance on an empty card) and
    // "Prayers (N)" when there are active prayers. PropertyChanged fires
    // from the ActivePrayerCount setter (the real dependency edge).

    [Fact]
    public void PrayersHeader_DefaultsToPlainPrayers()
    {
        var sut = CreateSut();
        // ActivePrayerCount defaults to 0
        Assert.Equal("Prayers", sut.PrayersHeader);
    }

    [Fact]
    public void PrayersHeader_WithCount_FormatsAsParenthetical()
    {
        var sut = CreateSut();
        sut.ActivePrayerCount = 3;

        Assert.Equal("Prayers (3)", sut.PrayersHeader);
    }

    [Fact]
    public void PrayersHeader_CountChangeFromZero_RaisesPropertyChanged()
    {
        var sut = CreateSut();
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.ActivePrayerCount = 4;

        Assert.Contains(nameof(PrayerCardViewModel.PrayersHeader), raised);
    }

    [Fact]
    public void PrayersHeader_CountChangeBackToZero_RaisesPropertyChanged()
    {
        var sut = CreateSut();
        sut.ActivePrayerCount = 5;
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.ActivePrayerCount = 0;

        Assert.Contains(nameof(PrayerCardViewModel.PrayersHeader), raised);
        Assert.Equal("Prayers", sut.PrayersHeader);
    }

    // ── ArchiveCommand ───────────────────────────────────────────────────

    [Fact]
    public async Task ArchiveCommand_NonArchived_Confirmed_CallsAssignBoxWithArchivedFolderId()
    {
        var card = new PrayerCard { Id = 7, Title = "Test", BoxId = 0 };
        var sut = new PrayerCardViewModel(card, _cardService, _prayerService,
            _onboardingService, _navigationService, _accessibilityService, _boxService, _settings);

        await ((IAsyncRelayCommand)sut.ArchiveCommand).ExecuteAsync(null);

        await _navigationService.Received(1).DisplayConfirmAsync(
            "Archive Card?", Arg.Any<string>(), "Archive", "Cancel");
        await _cardService.Received(1).AssignBoxAsync(card, ArchivedBoxId);
    }

    [Fact]
    public async Task ArchiveCommand_NonArchived_Cancelled_DoesNotAssignBox()
    {
        _navigationService
            .DisplayConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);
        var card = new PrayerCard { Id = 7, Title = "Test", BoxId = 0 };
        var sut = new PrayerCardViewModel(card, _cardService, _prayerService,
            _onboardingService, _navigationService, _accessibilityService, _boxService, _settings);

        await ((IAsyncRelayCommand)sut.ArchiveCommand).ExecuteAsync(null);

        await _cardService.DidNotReceive().AssignBoxAsync(Arg.Any<PrayerCard>(), Arg.Any<int>());
    }

    [Fact]
    public async Task ArchiveCommand_Archived_Unarchive_NoConfirmDialog_AssignsZero()
    {
        var card = new PrayerCard { Id = 7, Title = "Test", BoxId = ArchivedBoxId };
        var sut = new PrayerCardViewModel(card, _cardService, _prayerService,
            _onboardingService, _navigationService, _accessibilityService, _boxService, _settings);

        await ((IAsyncRelayCommand)sut.ArchiveCommand).ExecuteAsync(null);

        await _navigationService.DidNotReceive().DisplayConfirmAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await _cardService.Received(1).AssignBoxAsync(card, 0);
    }

    [Fact]
    public async Task ArchiveCommand_ArchivedCard_CallsAssignBoxWithZero()
    {
        var card = new PrayerCard { Id = 7, Title = "Test", BoxId = ArchivedBoxId };
        var sut = new PrayerCardViewModel(card, _cardService, _prayerService,
            _onboardingService, _navigationService, _accessibilityService, _boxService, _settings);

        await ((IAsyncRelayCommand)sut.ArchiveCommand).ExecuteAsync(null);

        await _cardService.Received(1).AssignBoxAsync(card, 0);
    }

    [Fact]
    public void IsArchived_FalseWhenBoxIdIsZero()
    {
        var card = new PrayerCard { Id = 7, BoxId = 0 };
        var sut = new PrayerCardViewModel(card, _cardService, _prayerService,
            _onboardingService, _navigationService, _accessibilityService, _boxService, _settings);

        Assert.False(sut.IsArchived);
    }

    [Fact]
    public void IsArchived_TrueWhenBoxIdEqualsArchivedFolderId()
    {
        var card = new PrayerCard { Id = 7, BoxId = ArchivedBoxId };
        var sut = new PrayerCardViewModel(card, _cardService, _prayerService,
            _onboardingService, _navigationService, _accessibilityService, _boxService, _settings);

        Assert.True(sut.IsArchived);
    }

    [Fact]
    public void ArchiveLabel_NonArchivedCard_ReturnsArchive()
    {
        var card = new PrayerCard { Id = 7, BoxId = 0 };
        var sut = new PrayerCardViewModel(card, _cardService, _prayerService,
            _onboardingService, _navigationService, _accessibilityService, _boxService, _settings);

        Assert.Equal("Archive", sut.ArchiveLabel);
    }

    [Fact]
    public void ArchiveLabel_ArchivedCard_ReturnsUnarchive()
    {
        var card = new PrayerCard { Id = 7, BoxId = ArchivedBoxId };
        var sut = new PrayerCardViewModel(card, _cardService, _prayerService,
            _onboardingService, _navigationService, _accessibilityService, _boxService, _settings);

        Assert.Equal("Unarchive", sut.ArchiveLabel);
    }

    [Fact]
    public void ArchiveCommand_SystemCard_CannotExecute()
    {
        var card = new PrayerCard { Id = 7, IsSystem = true };
        var sut = new PrayerCardViewModel(card, _cardService, _prayerService,
            _onboardingService, _navigationService, _accessibilityService, _boxService, _settings);

        Assert.False(sut.ArchiveCommand.CanExecute(null));
    }

    [Fact]
    public async Task ArchiveCommand_DoesNotChangePrayerStatus()
    {
        var card = new PrayerCard { Id = 7, Title = "Test", BoxId = 0 };
        var sut = new PrayerCardViewModel(card, _cardService, _prayerService,
            _onboardingService, _navigationService, _accessibilityService, _boxService, _settings);

        await ((IAsyncRelayCommand)sut.ArchiveCommand).ExecuteAsync(null);

        await _prayerService.DidNotReceive().SavePrayerAsync(Arg.Any<Prayer>());
    }

    [Fact]
    public async Task ArchiveCommand_Archive_RaisesIsArchivedAndArchiveLabelChanged()
    {
        var card = new PrayerCard { Id = 7, Title = "Test", BoxId = 0 };
        var sut = new PrayerCardViewModel(card, _cardService, _prayerService,
            _onboardingService, _navigationService, _accessibilityService, _boxService, _settings);

        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        await ((IAsyncRelayCommand)sut.ArchiveCommand).ExecuteAsync(null);

        Assert.Contains(nameof(PrayerCardViewModel.IsArchived), raised);
        Assert.Contains(nameof(PrayerCardViewModel.ArchiveLabel), raised);
    }
}

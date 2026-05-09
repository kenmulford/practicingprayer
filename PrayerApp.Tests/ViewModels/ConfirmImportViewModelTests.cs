using CommunityToolkit.Mvvm.Messaging;
using NSubstitute;
using PrayerApp;
using PrayerApp.Messages;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class ConfirmImportViewModelTests
{
    private readonly IDBService _db = Substitute.For<IDBService>();
    private readonly ICardService _cardService = Substitute.For<ICardService>();
    private readonly IPrayerService _prayerService = Substitute.For<IPrayerService>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
    private readonly IAccessibilityService _accessibilityService = Substitute.For<IAccessibilityService>();
    private readonly IImportPayloadService _payloadService = Substitute.For<IImportPayloadService>();
    private readonly ITextSelectionParser _parser = Substitute.For<ITextSelectionParser>();
    private readonly IBoxService _boxService = Substitute.For<IBoxService>();

    private readonly IMessenger _messenger = new WeakReferenceMessenger();
    private readonly object _recipient = new();
    private readonly List<BulkChangedMessage> _bulkMessages = new();

    public ConfirmImportViewModelTests()
    {
        PrayerCard.SetDBService(_db);
        Prayer.SetDBService(_db);
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));
        // Default to empty box list — picker tests override before calling
        // CreateSut. Default lives here (not in CreateSut) so per-test
        // .Returns() calls aren't clobbered when CreateSut runs.
        _boxService.GetBoxesAsync().Returns(Task.FromResult<IReadOnlyList<CardBox>>(
            Array.Empty<CardBox>()));
        _messenger.Register<object, BulkChangedMessage>(_recipient, (_, m) => _bulkMessages.Add(m));
    }

    private ConfirmImportViewModel CreateSut() =>
        new(_cardService, _prayerService, _navigationService, _accessibilityService,
            _messenger, _payloadService, _parser, _boxService);

    private static ParseResult Result(string suggestedTitle, params (string Title, string? Details)[] prayers) =>
        new(prayers.Select(p => new ParsedPrayer(p.Title, p.Details)).ToList().AsReadOnly(), suggestedTitle);

    // ── ConsumePending ──────────────────────────

    [Fact]
    public void ConsumePending_WithStagedPayload_ParsesAndPopulates()
    {
        _payloadService.ConsumePayload().Returns("1. Mom\n2. Dad");
        _parser.Parse("1. Mom\n2. Dad").Returns(Result("Imported May 1",
            ("Mom", null), ("Dad", null)));
        var sut = CreateSut();

        sut.ConsumePending();

        Assert.Equal("Imported May 1", sut.CardTitle);
        Assert.Equal(2, sut.Prayers.Count);
        Assert.Equal("Mom", sut.Prayers[0].Title);
        Assert.Equal("Dad", sut.Prayers[1].Title);
    }

    [Fact]
    public void ConsumePending_WithNoStagedPayload_LeavesDefaults()
    {
        _payloadService.ConsumePayload().Returns((string?)null);
        var sut = CreateSut();

        sut.ConsumePending();

        Assert.Equal(string.Empty, sut.CardTitle);
        Assert.Empty(sut.Prayers);
        _parser.DidNotReceive().Parse(Arg.Any<string>());
    }

    [Fact]
    public void ConsumePending_CalledTwice_DoesNotReParse()
    {
        // Modal OnAppearing fires on initial show AND on resume from background;
        // re-parsing would clobber the user's in-progress edits.
        _payloadService.ConsumePayload().Returns("1. Mom", (string?)null);
        _parser.Parse("1. Mom").Returns(Result("Imported May 1", ("Mom", null)));
        var sut = CreateSut();

        sut.ConsumePending();
        sut.Prayers[0].Title = "Mom edited";
        sut.ConsumePending();

        Assert.Equal("Mom edited", sut.Prayers[0].Title);
        _parser.Received(1).Parse(Arg.Any<string>());
    }

    [Fact]
    public void ConsumePending_WithStructured_PopulatesDirectlyAndSkipsParser()
    {
        // Deep-link / .prayercard inbound: payload is already structured JSON,
        // parsed inside DeepLinkService. Re-running it through the
        // text-selection parser would mangle clauses (e.g., split a notes
        // string on ';' inside a sentence).
        var staged = new ParseResult(new[]
        {
            new ParsedPrayer("Mom", "chemo starts Tuesday; pray for nausea relief"),
            new ParsedPrayer("Dad", null),
        }, "My Card");
        _payloadService.ConsumeStructured().Returns(staged);
        var sut = CreateSut();

        sut.ConsumePending();

        Assert.Equal("My Card", sut.CardTitle);
        Assert.Equal(2, sut.Prayers.Count);
        Assert.Equal("Mom", sut.Prayers[0].Title);
        Assert.Equal("chemo starts Tuesday; pray for nausea relief", sut.Prayers[0].Details);
        Assert.Equal("Dad", sut.Prayers[1].Title);
        _parser.DidNotReceive().Parse(Arg.Any<string>());
    }

    [Fact]
    public void ConsumePending_StructuredAndRaw_PrefersStructured()
    {
        // If both slots happen to be staged (shouldn't in practice), prefer
        // the structured payload — it's already authoritative; raw would
        // re-parse the same content lossy.
        var staged = new ParseResult(new[] { new ParsedPrayer("S1", null) }, "Structured Title");
        _payloadService.ConsumeStructured().Returns(staged);
        _payloadService.ConsumePayload().Returns("raw");
        _parser.Parse("raw").Returns(Result("Raw Title", ("R1", null)));
        var sut = CreateSut();

        sut.ConsumePending();

        Assert.Equal("Structured Title", sut.CardTitle);
        Assert.Single(sut.Prayers);
        Assert.Equal("S1", sut.Prayers[0].Title);
        _parser.DidNotReceive().Parse(Arg.Any<string>());
    }

    // ── SaveCommand.CanExecute ──────────────────────────

    [Fact]
    public void Save_CannotExecute_WhenCardTitleBlank()
    {
        _payloadService.ConsumePayload().Returns("1. Mom");
        _parser.Parse(Arg.Any<string>()).Returns(Result("", ("Mom", null)));
        var sut = CreateSut();
        sut.ConsumePending();

        sut.CardTitle = "   ";

        Assert.False(sut.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void Save_CannotExecute_WhenPrayersEmpty()
    {
        _payloadService.ConsumePayload().Returns((string?)null);
        var sut = CreateSut();
        sut.ConsumePending();
        sut.CardTitle = "My card";

        Assert.False(sut.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void Save_CanExecute_WhenTitleAndAtLeastOneRow()
    {
        _payloadService.ConsumePayload().Returns("1. Mom");
        _parser.Parse(Arg.Any<string>()).Returns(Result("Imported May 1", ("Mom", null)));
        var sut = CreateSut();
        sut.ConsumePending();

        Assert.True(sut.SaveCommand.CanExecute(null));
    }

    // ── SaveCommand effects ──────────────────────────

    [Fact]
    public async Task Save_CreatesCardWithTitleAndIsImported()
    {
        var sut = SetupSutWithRows(("Mom", null));
        sut.CardTitle = "Family";

        await sut.SaveCommand.ExecuteAsync(null);

        await _db.Received(1).InsertAsync(Arg.Is<PrayerCard>(c =>
            c.Title == "Family" && c.IsImported == true));
    }

    [Fact]
    public async Task Save_TrimsCardTitle()
    {
        var sut = SetupSutWithRows(("Mom", null));
        sut.CardTitle = "  Family  ";

        await sut.SaveCommand.ExecuteAsync(null);

        await _db.Received(1).InsertAsync(Arg.Is<PrayerCard>(c => c.Title == "Family"));
    }

    [Fact]
    public async Task Save_CreatesPrayerPerNonBlankRow()
    {
        var sut = SetupSutWithRows(("Mom", "Surgery"), ("Dad", null));

        await sut.SaveCommand.ExecuteAsync(null);

        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p =>
            p.Title == "Mom" && p.Details == "Surgery" && p.IsImported == true && p.CanNotify == false));
        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p =>
            p.Title == "Dad" && p.Details == null));
    }

    [Fact]
    public async Task Save_SkipsRowsWithBlankTitle()
    {
        var sut = SetupSutWithRows(("Mom", null), ("   ", null), ("Dad", null));

        await sut.SaveCommand.ExecuteAsync(null);

        await _db.Received(2).InsertAsync(Arg.Any<Prayer>());
    }

    [Fact]
    public async Task Save_TrimsPrayerTitleAndDetails()
    {
        var sut = SetupSutWithRows(("  Mom  ", "  Surgery  "));

        await sut.SaveCommand.ExecuteAsync(null);

        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p =>
            p.Title == "Mom" && p.Details == "Surgery"));
    }

    [Fact]
    public async Task Save_BlankDetails_StoredAsNull()
    {
        var sut = SetupSutWithRows(("Mom", "   "));

        await sut.SaveCommand.ExecuteAsync(null);

        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p => p.Details == null));
    }

    [Fact]
    public async Task Save_InvalidatesBothCaches()
    {
        var sut = SetupSutWithRows(("Mom", null));

        await sut.SaveCommand.ExecuteAsync(null);

        _cardService.Received(1).InvalidateCache();
        _prayerService.Received(1).InvalidateCache();
    }

    [Fact]
    public async Task Save_SendsBulkChangedMessage()
    {
        var sut = SetupSutWithRows(("Mom", null), ("Dad", null));

        await sut.SaveCommand.ExecuteAsync(null);

        Assert.Single(_bulkMessages);
    }

    [Fact]
    public async Task Save_AnnouncesPrayerCountAndCardTitle()
    {
        var sut = SetupSutWithRows(("Mom", null), ("Dad", null), ("Sis", null));
        sut.CardTitle = "Family";

        await sut.SaveCommand.ExecuteAsync(null);

        _accessibilityService.Received(1).Announce(Arg.Is<string>(s =>
            s.Contains("3") && s.Contains("Family")));
    }

    [Fact]
    public async Task Save_NavigatesToCardsTabWithSavedCardIdInUrl()
    {
        // Card id must flow through the route so PrayerCardsViewModel.ApplyQueryAttributes
        // stages PendingSavedIdentifier — without it the imported card and its containing
        // section never get auto-expanded and the user can't see the import landed.
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(callInfo =>
        {
            ((PrayerCard)callInfo[0]).Id = 42;
            return Task.FromResult(1);
        });
        var sut = SetupSutWithRows(("Mom", null));

        await sut.SaveCommand.ExecuteAsync(null);

        await _navigationService.Received(1).GoToAsync(Routes.PrayerCardsTabImported(42));
    }

    // ── CancelCommand ──────────────────────────

    [Fact]
    public async Task Cancel_PopsModal()
    {
        var sut = CreateSut();

        await sut.CancelCommand.ExecuteAsync(null);

        await _navigationService.Received(1).PopModalAsync();
    }

    [Fact]
    public async Task Cancel_DrainsBothPayloadSlots()
    {
        // If the user dismisses before OnAppearing fires ConsumePending, a
        // stale payload (raw OR structured) could surface on the next launch.
        // Cancel must drain both channels to be safe.
        var sut = CreateSut();

        await sut.CancelCommand.ExecuteAsync(null);

        _payloadService.Received(1).ConsumePayload();
        _payloadService.Received(1).ConsumeStructured();
    }

    // ── DrainIfNotConsumed (swipe-dismiss safety net) ─────────────────────

    [Fact]
    public void DrainIfNotConsumed_FirstCall_DrainsBothChannels()
    {
        // iOS PageSheet swipe-dismiss does not fire CancelCommand. The page's
        // OnDisappearing wires DrainIfNotConsumed so a swipe-down doesn't
        // leak a stale payload to the next launch.
        var sut = CreateSut();

        sut.DrainIfNotConsumed();

        _payloadService.Received(1).ConsumePayload();
        _payloadService.Received(1).ConsumeStructured();
    }

    [Fact]
    public void DrainIfNotConsumed_SecondCall_IsNoOp()
    {
        // OnDisappearing can fire more than once across the page lifecycle;
        // the guard must keep the drain to a single round trip per ViewModel.
        var sut = CreateSut();

        sut.DrainIfNotConsumed();
        sut.DrainIfNotConsumed();

        _payloadService.Received(1).ConsumePayload();
        _payloadService.Received(1).ConsumeStructured();
    }

    [Fact]
    public async Task SaveAsync_ThenDrainIfNotConsumed_DoesNotDrainAgain()
    {
        // Save's path through ConsumePending already pulled both channels.
        // SaveAsync flips the consumed flag so OnDisappearing's safety-net
        // call is a no-op rather than re-invoking the (now-empty) Consume*.
        var sut = SetupSutWithRows(("Mom", null));
        // Reset call counts: SetupSutWithRows ran ConsumePending which already
        // hit ConsumePayload once; the assertion below targets the post-save
        // window only.
        _payloadService.ClearReceivedCalls();

        await sut.SaveCommand.ExecuteAsync(null);
        sut.DrainIfNotConsumed();

        _payloadService.DidNotReceive().ConsumePayload();
        _payloadService.DidNotReceive().ConsumeStructured();
    }

    [Fact]
    public async Task CancelAsync_ThenDrainIfNotConsumed_DoesNotDrainAgain()
    {
        // Cancel itself drains both channels via DrainIfNotConsumed; a
        // follow-on OnDisappearing must not trigger a second round trip.
        var sut = CreateSut();

        await sut.CancelCommand.ExecuteAsync(null);
        sut.DrainIfNotConsumed();

        _payloadService.Received(1).ConsumePayload();
        _payloadService.Received(1).ConsumeStructured();
    }

    // ── AddPrayer / RemovePrayer ──────────────────────────

    [Fact]
    public void AddPrayer_AppendsEmptyRow()
    {
        var sut = CreateSut();
        var initialCount = sut.Prayers.Count;

        sut.AddPrayerCommand.Execute(null);

        Assert.Equal(initialCount + 1, sut.Prayers.Count);
        Assert.Equal(string.Empty, sut.Prayers[^1].Title);
    }

    [Fact]
    public void RemovePrayer_RemovesTargetedRow()
    {
        var sut = SetupSutWithRows(("Mom", null), ("Dad", null), ("Sis", null));
        var middle = sut.Prayers[1];

        sut.RemovePrayerCommand.Execute(middle);

        Assert.Equal(2, sut.Prayers.Count);
        Assert.DoesNotContain(middle, sut.Prayers);
    }

    // ── PrayersHeader ──────────────────────────

    [Fact]
    public void PrayersHeader_ReflectsCurrentCount()
    {
        var sut = CreateSut();
        Assert.Equal("Prayers (0)", sut.PrayersHeader);

        sut.Prayers.Add(new EditablePrayer());
        sut.Prayers.Add(new EditablePrayer());
        Assert.Equal("Prayers (2)", sut.PrayersHeader);

        sut.Prayers.RemoveAt(0);
        Assert.Equal("Prayers (1)", sut.PrayersHeader);
    }

    [Fact]
    public void PrayersHeader_RaisesPropertyChanged_OnCollectionChange()
    {
        // Without INPC notification on collection change, the bound Label
        // would stay at "Prayers (0)" after parse populates the list.
        var sut = CreateSut();
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.Prayers.Add(new EditablePrayer());

        Assert.Contains(nameof(sut.PrayersHeader), raised);
    }

    // ── Collection picker (LoadBoxesAsync + SaveAsync BoxId) ──────────

    [Fact]
    public async Task LoadBoxesAsync_LooseCardsFirst_FollowedByUserBoxes()
    {
        _boxService.GetBoxesAsync().Returns(Task.FromResult<IReadOnlyList<CardBox>>(new[]
        {
            new CardBox { Id = 7, Name = "Family", IsSystem = false },
            new CardBox { Id = 8, Name = "Work", IsSystem = false },
        }));
        var sut = CreateSut();

        await sut.LoadBoxesAsync();

        Assert.Equal(3, sut.AvailableBoxes.Count);
        // Picker is RealBoxPickerItem-only outside Existing-Card mode.
        var realBoxes = sut.AvailableBoxes.OfType<RealBoxPickerItem>().ToList();
        Assert.Equal(3, realBoxes.Count);
        Assert.Equal(0, realBoxes[0].BoxId);
        Assert.Equal("Loose Cards", realBoxes[0].Name);
        Assert.Equal(7, realBoxes[1].BoxId);
        Assert.Equal(8, realBoxes[2].BoxId);
    }

    [Fact]
    public async Task LoadBoxesAsync_DefaultsSelectedBoxToLooseCards()
    {
        _boxService.GetBoxesAsync().Returns(Task.FromResult<IReadOnlyList<CardBox>>(new[]
        {
            new CardBox { Id = 7, Name = "Family", IsSystem = false },
        }));
        var sut = CreateSut();

        await sut.LoadBoxesAsync();

        var selected = Assert.IsType<RealBoxPickerItem>(sut.SelectedBox);
        Assert.Equal(0, selected.BoxId);
    }

    [Fact]
    public async Task LoadBoxesAsync_ExcludesSystemBoxes()
    {
        // Mirror PrayerCardViewModel.LoadBoxPickerAsync: System and Archived
        // are out of scope for the import landing page (users shouldn't be
        // pushed toward archiving an import).
        _boxService.GetBoxesAsync().Returns(Task.FromResult<IReadOnlyList<CardBox>>(new[]
        {
            new CardBox { Id = 7, Name = "Family", IsSystem = false },
            new CardBox { Id = 99, Name = "Archived", IsSystem = true },
        }));
        var sut = CreateSut();

        await sut.LoadBoxesAsync();

        Assert.Equal(2, sut.AvailableBoxes.Count); // Loose + Family only
        Assert.DoesNotContain(sut.AvailableBoxes, b => b.Name == "Archived");
    }

    [Fact]
    public async Task LoadBoxesAsync_CalledTwice_DoesNotReloadOrClobberSelection()
    {
        // Modal OnAppearing fires on initial show AND on resume from background.
        // Reloading would clobber a user's mid-flow Collection pick.
        _boxService.GetBoxesAsync().Returns(Task.FromResult<IReadOnlyList<CardBox>>(new[]
        {
            new CardBox { Id = 7, Name = "Family", IsSystem = false },
        }));
        var sut = CreateSut();
        await sut.LoadBoxesAsync();
        sut.SelectedBox = sut.AvailableBoxes.OfType<RealBoxPickerItem>().First(b => b.BoxId == 7);

        await sut.LoadBoxesAsync();

        var selected = Assert.IsType<RealBoxPickerItem>(sut.SelectedBox);
        Assert.Equal(7, selected.BoxId);
        Assert.Equal(2, sut.AvailableBoxes.Count);
        await _boxService.Received(1).GetBoxesAsync();
    }

    [Fact]
    public async Task Save_WithSelectedUserBox_AssignsCardToThatBox()
    {
        _boxService.GetBoxesAsync().Returns(Task.FromResult<IReadOnlyList<CardBox>>(new[]
        {
            new CardBox { Id = 7, Name = "Family", IsSystem = false },
        }));
        var sut = SetupSutWithRows(("Mom", null));
        sut.CardTitle = "Family imports";
        await sut.LoadBoxesAsync();
        sut.SelectedBox = sut.AvailableBoxes.OfType<RealBoxPickerItem>().First(b => b.BoxId == 7);

        await sut.SaveCommand.ExecuteAsync(null);

        await _db.Received(1).InsertAsync(Arg.Is<PrayerCard>(c => c.BoxId == 7));
    }

    [Fact]
    public async Task Save_WithoutLoadingBoxes_DefaultsToBoxIdZero()
    {
        // Defensive: if the picker was never wired (or LoadBoxesAsync failed),
        // saved imports still land in Loose Cards — matches the prior
        // hardcoded behavior.
        var sut = SetupSutWithRows(("Mom", null));
        sut.CardTitle = "Imported May 1";

        await sut.SaveCommand.ExecuteAsync(null);

        await _db.Received(1).InsertAsync(Arg.Is<PrayerCard>(c => c.BoxId == 0));
    }

    [Fact]
    public async Task Save_WithDefaultLooseCardsSelection_AssignsBoxIdZero()
    {
        var sut = SetupSutWithRows(("Mom", null));
        sut.CardTitle = "Imported May 1";
        await sut.LoadBoxesAsync(); // default selection = Loose Cards

        await sut.SaveCommand.ExecuteAsync(null);

        await _db.Received(1).InsertAsync(Arg.Is<PrayerCard>(c => c.BoxId == 0));
    }

    // ── ImportMode switching ──────────────────────────────────────────────

    [Fact]
    public void ImportMode_DefaultIsNewCard()
    {
        var sut = CreateSut();

        Assert.Equal(ImportMode.NewCard, sut.ImportMode);
    }

    [Fact]
    public void SetNewCardModeCommand_SetsImportModeToNewCard()
    {
        var sut = CreateSut();
        sut.SetExistingCardModeCommand.Execute(null);
        Assert.Equal(ImportMode.ExistingCard, sut.ImportMode);

        sut.SetNewCardModeCommand.Execute(null);

        Assert.Equal(ImportMode.NewCard, sut.ImportMode);
    }

    [Fact]
    public void SetExistingCardModeCommand_SetsImportModeToExistingCard()
    {
        var sut = CreateSut();

        sut.SetExistingCardModeCommand.Execute(null);

        Assert.Equal(ImportMode.ExistingCard, sut.ImportMode);
    }

    // ── CanSave per mode ──────────────────────────────────────────────────

    [Fact]
    public void CanSave_ExistingCardMode_NullSelectedCard_WithPrayers_ReturnsFalse()
    {
        var sut = SetupSutWithRows(("Mom", null));
        sut.SetExistingCardModeCommand.Execute(null);
        // SelectedCard is null by default

        Assert.False(sut.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void CanSave_ExistingCardMode_SelectedCardSet_WithPrayers_ReturnsTrue()
    {
        var sut = SetupSutWithRows(("Mom", null));
        sut.SetExistingCardModeCommand.Execute(null);
        sut.SelectedCard = new CardPickerItem { CardId = 1, Title = "Family" };

        Assert.True(sut.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void CanSave_ExistingCardMode_SelectedCardSet_NoPrayers_ReturnsFalse()
    {
        var sut = CreateSut();
        sut.SetExistingCardModeCommand.Execute(null);
        sut.SelectedCard = new CardPickerItem { CardId = 1, Title = "Family" };

        Assert.False(sut.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void SwitchingImportMode_FiresSaveCommandCanExecuteChanged()
    {
        var sut = SetupSutWithRows(("Mom", null));
        sut.CardTitle = "My Card";
        var canExecuteChangedFired = false;
        sut.SaveCommand.CanExecuteChanged += (_, _) => canExecuteChangedFired = true;

        sut.SetExistingCardModeCommand.Execute(null);

        Assert.True(canExecuteChangedFired);
    }

    [Fact]
    public void CanSave_NewCardMode_BlankCardTitle_WithPrayers_ReturnsFalse()
    {
        var sut = SetupSutWithRows(("Mom", null));
        sut.CardTitle = "   ";
        // ImportMode is NewCard by default

        Assert.False(sut.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void CanSave_NewCardMode_AllPrayerTitlesBlank_ReturnsFalse()
    {
        var sut = CreateSut();
        sut.CardTitle = "My Card";
        sut.AddPrayerCommand.Execute(null); // adds row with blank Title
        // ImportMode defaults to NewCard, prayer count > 0 but all titles blank

        Assert.False(sut.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void CanSave_ExistingCardMode_AllPrayerTitlesBlank_ReturnsFalse()
    {
        var sut = CreateSut();
        sut.SetExistingCardModeCommand.Execute(null);
        sut.SelectedCard = new CardPickerItem { CardId = 1, Title = "Family" };
        sut.AddPrayerCommand.Execute(null); // adds row with blank Title

        Assert.False(sut.SaveCommand.CanExecute(null));
    }

    // ── AvailableCardGroups filtering (hybrid card picker) ────────────────

    [Fact]
    public async Task LoadCardGroupsAsync_AllCollectionsSentinel_ShowsAllCardsGrouped()
    {
        // Default ExistingCard mode pre-selects the All-collections sentinel;
        // every non-system card surfaces, partitioned by collection.
        var alpha = new PrayerCard { Id = 1, Title = "Alpha", BoxId = 7, IsSystem = false };
        var beta = new PrayerCard { Id = 2, Title = "Beta", BoxId = 8, IsSystem = false };
        var loose = new PrayerCard { Id = 3, Title = "Loose1", BoxId = 0, IsSystem = false };
        _cardService.GetCardsAsync().Returns(Task.FromResult<IReadOnlyList<PrayerCard>>(
            new[] { alpha, beta, loose }));
        _boxService.GetBoxesAsync().Returns(Task.FromResult<IReadOnlyList<CardBox>>(new[]
        {
            new CardBox { Id = 7, Name = "Family", IsSystem = false },
            new CardBox { Id = 8, Name = "Work", IsSystem = false },
        }));
        var sut = CreateSut();
        await sut.LoadBoxesAsync();

        sut.SetExistingCardModeCommand.Execute(null);
        await Task.Delay(50);

        // 3 groups: Family, Loose Cards, Work
        Assert.Equal(3, sut.AvailableCardGroups.Count);
        var totalCards = sut.AvailableCardGroups.Sum(g => g.Cards.Count);
        Assert.Equal(3, totalCards);
    }

    [Fact]
    public async Task LoadCardGroupsAsync_SpecificBox_FiltersToThatCollection()
    {
        var alpha = new PrayerCard { Id = 1, Title = "Alpha", BoxId = 7, IsSystem = false };
        var beta = new PrayerCard { Id = 2, Title = "Beta", BoxId = 8, IsSystem = false };
        _cardService.GetCardsAsync().Returns(Task.FromResult<IReadOnlyList<PrayerCard>>(
            new[] { alpha, beta }));
        _boxService.GetBoxesAsync().Returns(Task.FromResult<IReadOnlyList<CardBox>>(new[]
        {
            new CardBox { Id = 7, Name = "Family", IsSystem = false },
            new CardBox { Id = 8, Name = "Work", IsSystem = false },
        }));
        var sut = CreateSut();
        await sut.LoadBoxesAsync();
        sut.SetExistingCardModeCommand.Execute(null);
        await Task.Delay(50);

        sut.SelectedBox = sut.AvailableBoxes.OfType<RealBoxPickerItem>().First(b => b.BoxId == 7);
        await Task.Delay(50);

        Assert.Single(sut.AvailableCardGroups);
        Assert.Equal("Family", sut.AvailableCardGroups[0].CollectionName);
        Assert.Single(sut.AvailableCardGroups[0].Cards);
        Assert.Equal("Alpha", sut.AvailableCardGroups[0].Cards[0].Title);
    }

    [Fact]
    public async Task LoadCardGroupsAsync_SortsCardsAlphabeticallyWithinGroup()
    {
        var zulu = new PrayerCard { Id = 1, Title = "Zulu", BoxId = 7, IsSystem = false };
        var alpha = new PrayerCard { Id = 2, Title = "Alpha", BoxId = 7, IsSystem = false };
        _cardService.GetCardsAsync().Returns(Task.FromResult<IReadOnlyList<PrayerCard>>(
            new[] { zulu, alpha }));
        _boxService.GetBoxesAsync().Returns(Task.FromResult<IReadOnlyList<CardBox>>(new[]
        {
            new CardBox { Id = 7, Name = "Family", IsSystem = false },
        }));
        var sut = CreateSut();
        await sut.LoadBoxesAsync();
        sut.SetExistingCardModeCommand.Execute(null);
        await Task.Delay(50);

        sut.SelectedBox = sut.AvailableBoxes.OfType<RealBoxPickerItem>().First(b => b.BoxId == 7);
        await Task.Delay(50);

        Assert.Equal("Alpha", sut.AvailableCardGroups[0].Cards[0].Title);
        Assert.Equal("Zulu", sut.AvailableCardGroups[0].Cards[1].Title);
    }

    [Fact]
    public async Task LoadCardGroupsAsync_SortsGroupsByCollectionName()
    {
        var workCard = new PrayerCard { Id = 1, Title = "WC", BoxId = 8, IsSystem = false };
        var familyCard = new PrayerCard { Id = 2, Title = "FC", BoxId = 7, IsSystem = false };
        var apostlesCard = new PrayerCard { Id = 3, Title = "AC", BoxId = 9, IsSystem = false };
        _cardService.GetCardsAsync().Returns(Task.FromResult<IReadOnlyList<PrayerCard>>(
            new[] { workCard, familyCard, apostlesCard }));
        _boxService.GetBoxesAsync().Returns(Task.FromResult<IReadOnlyList<CardBox>>(new[]
        {
            new CardBox { Id = 8, Name = "Work", IsSystem = false },
            new CardBox { Id = 7, Name = "Family", IsSystem = false },
            new CardBox { Id = 9, Name = "Apostles", IsSystem = false },
        }));
        var sut = CreateSut();
        await sut.LoadBoxesAsync();

        sut.SetExistingCardModeCommand.Execute(null);
        await Task.Delay(50);

        Assert.Equal("Apostles", sut.AvailableCardGroups[0].CollectionName);
        Assert.Equal("Family", sut.AvailableCardGroups[1].CollectionName);
        Assert.Equal("Work", sut.AvailableCardGroups[2].CollectionName);
    }

    [Fact]
    public async Task LoadCardGroupsAsync_LooseCardsAppearAsGroup_BoxId0()
    {
        // BoxId 0 cards must surface under the "Loose Cards" header — without
        // a name lookup hit, they would fall into "Unknown" and look broken.
        var loose = new PrayerCard { Id = 1, Title = "Stray", BoxId = 0, IsSystem = false };
        _cardService.GetCardsAsync().Returns(Task.FromResult<IReadOnlyList<PrayerCard>>(
            new[] { loose }));
        _boxService.GetBoxesAsync().Returns(Task.FromResult<IReadOnlyList<CardBox>>(
            Array.Empty<CardBox>()));
        var sut = CreateSut();
        await sut.LoadBoxesAsync();

        sut.SetExistingCardModeCommand.Execute(null);
        await Task.Delay(50);

        Assert.Single(sut.AvailableCardGroups);
        Assert.Equal("Loose Cards", sut.AvailableCardGroups[0].CollectionName);
    }

    [Fact]
    public async Task LoadCardGroupsAsync_ExcludesSystemCards()
    {
        var user = new PrayerCard { Id = 1, Title = "Alpha", BoxId = 7, IsSystem = false };
        var system = new PrayerCard { Id = 2, Title = "System", BoxId = 7, IsSystem = true };
        _cardService.GetCardsAsync().Returns(Task.FromResult<IReadOnlyList<PrayerCard>>(
            new[] { user, system }));
        _boxService.GetBoxesAsync().Returns(Task.FromResult<IReadOnlyList<CardBox>>(new[]
        {
            new CardBox { Id = 7, Name = "Family", IsSystem = false },
        }));
        var sut = CreateSut();
        await sut.LoadBoxesAsync();

        sut.SetExistingCardModeCommand.Execute(null);
        await Task.Delay(50);

        var allCards = sut.AvailableCardGroups.SelectMany(g => g.Cards).ToList();
        Assert.DoesNotContain(allCards, c => c.CardId == 2);
        Assert.Single(allCards);
    }

    [Fact]
    public async Task LoadCardGroupsAsync_BoxChange_NullsSelectedCard()
    {
        var alpha = new PrayerCard { Id = 1, Title = "Alpha", BoxId = 7, IsSystem = false };
        _cardService.GetCardsAsync().Returns(Task.FromResult<IReadOnlyList<PrayerCard>>(
            new[] { alpha }));
        _boxService.GetBoxesAsync().Returns(Task.FromResult<IReadOnlyList<CardBox>>(new[]
        {
            new CardBox { Id = 7, Name = "Family", IsSystem = false },
        }));
        var sut = CreateSut();
        await sut.LoadBoxesAsync();
        sut.SetExistingCardModeCommand.Execute(null);
        await Task.Delay(50);
        sut.SelectedBox = sut.AvailableBoxes.OfType<RealBoxPickerItem>().First(b => b.BoxId == 7);
        await Task.Delay(50);
        sut.SelectedCard = sut.AvailableCardGroups[0].Cards.FirstOrDefault();
        Assert.NotNull(sut.SelectedCard);

        // Change to a different box → SelectedCard must clear so save-gate stays accurate.
        sut.SelectedBox = sut.AvailableBoxes.OfType<RealBoxPickerItem>().First(b => b.BoxId == 0);
        await Task.Delay(50);

        Assert.Null(sut.SelectedCard);
    }

    // ── ImportMode flip — All-sentinel insertion / removal ────────────────

    [Fact]
    public async Task EnteringExistingCardMode_AddsAllSentinelAndSelectsIt()
    {
        // The All-collections sentinel only exists in ExistingCard mode; it
        // would be misleading in NewCard mode (a new card needs ONE BoxId).
        _boxService.GetBoxesAsync().Returns(Task.FromResult<IReadOnlyList<CardBox>>(new[]
        {
            new CardBox { Id = 7, Name = "Family", IsSystem = false },
        }));
        var sut = CreateSut();
        await sut.LoadBoxesAsync();
        Assert.DoesNotContain(sut.AvailableBoxes, b => b is AllCollectionsPickerItem);

        sut.SetExistingCardModeCommand.Execute(null);

        Assert.IsType<AllCollectionsPickerItem>(sut.AvailableBoxes[0]);
        Assert.IsType<AllCollectionsPickerItem>(sut.SelectedBox);
    }

    [Fact]
    public async Task LeavingExistingCardMode_RemovesAllSentinel()
    {
        _boxService.GetBoxesAsync().Returns(Task.FromResult<IReadOnlyList<CardBox>>(new[]
        {
            new CardBox { Id = 7, Name = "Family", IsSystem = false },
        }));
        var sut = CreateSut();
        await sut.LoadBoxesAsync();
        sut.SetExistingCardModeCommand.Execute(null);
        Assert.Contains(sut.AvailableBoxes, b => b is AllCollectionsPickerItem);

        sut.SetNewCardModeCommand.Execute(null);

        Assert.DoesNotContain(sut.AvailableBoxes, b => b is AllCollectionsPickerItem);
        // SelectedBox restored to Loose Cards so a NewCard save still has a valid BoxId.
        var selectedAfter = Assert.IsType<RealBoxPickerItem>(sut.SelectedBox);
        Assert.Equal(0, selectedAfter.BoxId);
    }

    [Fact]
    public async Task SelectedBoxChange_InExistingCardMode_TriggersReload()
    {
        var alpha = new PrayerCard { Id = 1, Title = "Alpha", BoxId = 7, IsSystem = false };
        _cardService.GetCardsAsync().Returns(Task.FromResult<IReadOnlyList<PrayerCard>>(
            new[] { alpha }));
        _boxService.GetBoxesAsync().Returns(Task.FromResult<IReadOnlyList<CardBox>>(new[]
        {
            new CardBox { Id = 7, Name = "Family", IsSystem = false },
        }));
        var sut = CreateSut();
        await sut.LoadBoxesAsync();
        sut.SetExistingCardModeCommand.Execute(null);
        await Task.Delay(50);
        _cardService.ClearReceivedCalls();

        sut.SelectedBox = sut.AvailableBoxes.OfType<RealBoxPickerItem>().First(b => b.BoxId == 7);
        await Task.Delay(50);

        await _cardService.Received().GetCardsAsync();
    }

    [Fact]
    public async Task SelectedBoxChange_InNewCardMode_DoesNotTriggerCardLoad()
    {
        // NewCard mode: the picker drives the new card's BoxId. There is no
        // card list to refresh, and pulling cards on every box change is waste.
        _boxService.GetBoxesAsync().Returns(Task.FromResult<IReadOnlyList<CardBox>>(new[]
        {
            new CardBox { Id = 7, Name = "Family", IsSystem = false },
        }));
        var sut = CreateSut();
        await sut.LoadBoxesAsync();
        _cardService.ClearReceivedCalls();

        sut.SelectedBox = sut.AvailableBoxes.OfType<RealBoxPickerItem>().First(b => b.BoxId == 7);
        await Task.Delay(50);

        await _cardService.DidNotReceive().GetCardsAsync();
    }

    // ── HasNoAvailableCards (empty-state Label) ───────────────────────────

    [Fact]
    public void HasNoAvailableCards_TrueWhenAllGroupsEmpty_FalseOtherwise()
    {
        var sut = CreateSut();

        // NewCard mode with no groups → false (empty state is only meaningful
        // in ExistingCard mode; the list is hidden anyway).
        Assert.False(sut.HasNoAvailableCards);

        sut.SetExistingCardModeCommand.Execute(null);
        // ExistingCard mode + no groups → true (empty surface)
        Assert.True(sut.HasNoAvailableCards);
    }

    [Fact]
    public void HasNoAvailableCards_FalseWhenAtLeastOneGroupHasCards()
    {
        var sut = CreateSut();
        sut.SetExistingCardModeCommand.Execute(null);
        Assert.True(sut.HasNoAvailableCards);

        sut.AvailableCardGroups.Add(new CardCollectionGroup
        {
            CollectionName = "Family",
            Cards = { new CardPickerItem { CardId = 1, Title = "Alpha" } },
        });

        Assert.False(sut.HasNoAvailableCards);
    }

    [Fact]
    public void HasNoAvailableCards_RaisesPropertyChanged_OnImportModeSwitch()
    {
        var sut = CreateSut();
        var fired = 0;
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ConfirmImportViewModel.HasNoAvailableCards))
                fired++;
        };

        sut.SetExistingCardModeCommand.Execute(null);

        Assert.True(fired >= 1);
    }

    [Fact]
    public void HasNoAvailableCards_RaisesPropertyChanged_OnAvailableCardGroupsChange()
    {
        // CollectionChanged → property changed; the XAML binding only
        // refreshes when PropertyChanged fires, so this wiring matters.
        var sut = CreateSut();
        sut.SetExistingCardModeCommand.Execute(null);
        var fired = 0;
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ConfirmImportViewModel.HasNoAvailableCards))
                fired++;
        };

        sut.AvailableCardGroups.Add(new CardCollectionGroup
        {
            CollectionName = "Family",
            Cards = { new CardPickerItem { CardId = 1, Title = "Alpha" } },
        });
        sut.AvailableCardGroups.Clear();

        Assert.True(fired >= 2);
    }

    // ── SelectCardCommand ─────────────────────────────────────────────────

    [Fact]
    public void SelectCardCommand_SetsSelectedCard()
    {
        var sut = CreateSut();
        var item = new CardPickerItem { CardId = 1, Title = "Family" };

        sut.SelectCardCommand.Execute(item);

        Assert.Same(item, sut.SelectedCard);
    }

    [Fact]
    public void SelectCardCommand_ClearsIsSelectedOnPreviousItem()
    {
        var sut = CreateSut();
        var prev = new CardPickerItem { CardId = 1, Title = "Old" };
        var next = new CardPickerItem { CardId = 2, Title = "New" };
        sut.SelectCardCommand.Execute(prev);
        Assert.True(prev.IsSelected);

        sut.SelectCardCommand.Execute(next);

        Assert.False(prev.IsSelected);
    }

    [Fact]
    public void SelectCardCommand_SetsIsSelectedOnNewItem()
    {
        var sut = CreateSut();
        var item = new CardPickerItem { CardId = 1, Title = "Family" };

        sut.SelectCardCommand.Execute(item);

        Assert.True(item.IsSelected);
    }

    // ── SaveAsync existing-card path ──────────────────────────────────────

    [Fact]
    public async Task Save_ExistingCard_CreatesPrayerWithCorrectCardId_NoNewPrayerCardCreated()
    {
        var sut = SetupSutWithRows(("Mom", null));
        sut.SetExistingCardModeCommand.Execute(null);
        sut.SelectedCard = new CardPickerItem { CardId = 42, Title = "Family" };

        await sut.SaveCommand.ExecuteAsync(null);

        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p => p.PrayerCardId == 42));
        await _db.DidNotReceive().InsertAsync(Arg.Any<PrayerCard>());
    }

    [Fact]
    public async Task Save_ExistingCard_TrimsAndNormalizesQuotes()
    {
        // Use curly quotes to verify NormalizeQuotes runs, and leading/trailing spaces for Trim
        var sut = SetupSutWithRows(("  “Mom”  ", "  heal’s concern  "));
        sut.SetExistingCardModeCommand.Execute(null);
        sut.SelectedCard = new CardPickerItem { CardId = 42, Title = "Family" };

        await sut.SaveCommand.ExecuteAsync(null);

        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p =>
            p.Title == "\"Mom\"" && p.Details == "heal's concern"));
    }

    [Fact]
    public async Task Save_ExistingCard_SkipsRowsWithBlankTitle()
    {
        var sut = SetupSutWithRows(("Mom", null), ("   ", null), ("Dad", null));
        sut.SetExistingCardModeCommand.Execute(null);
        sut.SelectedCard = new CardPickerItem { CardId = 42, Title = "Family" };

        await sut.SaveCommand.ExecuteAsync(null);

        await _db.Received(2).InsertAsync(Arg.Any<Prayer>());
    }

    [Fact]
    public async Task Save_ExistingCard_InvalidatesBothCaches()
    {
        var sut = SetupSutWithRows(("Mom", null));
        sut.SetExistingCardModeCommand.Execute(null);
        sut.SelectedCard = new CardPickerItem { CardId = 42, Title = "Family" };

        await sut.SaveCommand.ExecuteAsync(null);

        _cardService.Received(1).InvalidateCache();
        _prayerService.Received(1).InvalidateCache();
    }

    [Fact]
    public async Task Save_ExistingCard_SendsBulkChangedMessage()
    {
        var sut = SetupSutWithRows(("Mom", null));
        sut.SetExistingCardModeCommand.Execute(null);
        sut.SelectedCard = new CardPickerItem { CardId = 42, Title = "Family" };

        await sut.SaveCommand.ExecuteAsync(null);

        Assert.Single(_bulkMessages);
    }

    [Fact]
    public async Task Save_ExistingCard_NavigatesToImportedToExistingRoute()
    {
        var sut = SetupSutWithRows(("Mom", null));
        sut.SetExistingCardModeCommand.Execute(null);
        sut.SelectedCard = new CardPickerItem { CardId = 42, Title = "Family" };

        await sut.SaveCommand.ExecuteAsync(null);

        await _navigationService.Received(1).GoToAsync(Routes.PrayerCardsTabImportedToExisting(42));
        await _navigationService.DidNotReceive().GoToAsync(
            Arg.Is<string>(s => s.Contains("saved=")));
    }

    // ── Re-entrancy: in-flight LoadBoxesAsync ─────────────────────────────

    [Fact]
    public async Task LoadBoxesAsync_ReentrantWhileInFlight_DoesNotDoubleLoad()
    {
        // Race window: caller A awaits LoadBoxesAsync; mid-await on
        // GetBoxesAsync, caller B (e.g. re-entrant OnAppearing on resume)
        // calls LoadBoxesAsync. The in-flight guard keeps the second call
        // from re-issuing GetBoxesAsync and from double-populating
        // AvailableBoxes.
        var tcs = new TaskCompletionSource<IReadOnlyList<CardBox>>();
        _boxService.GetBoxesAsync().Returns(tcs.Task);
        var sut = CreateSut();

        var first = sut.LoadBoxesAsync();
        var second = sut.LoadBoxesAsync(); // re-entrant before first completes
        tcs.SetResult(new[] { new CardBox { Id = 7, Name = "Family", IsSystem = false } });
        await Task.WhenAll(first, second);

        Assert.Equal(2, sut.AvailableBoxes.Count); // Loose + Family — not 4
        await _boxService.Received(1).GetBoxesAsync();
    }

    // ── CardCollectionGroup keyed on BoxId ────────────────────────────────

    [Fact]
    public async Task LoadCardGroupsAsync_TwoBoxesWithSameName_DoesNotMerge()
    {
        // CardBox.Name uniqueness is not enforced; data drift can produce
        // two rows with the same display name. Grouping by name would
        // silently merge their card lists. Group key is BoxId.
        var c1 = new PrayerCard { Id = 1, Title = "A", BoxId = 7, IsSystem = false };
        var c2 = new PrayerCard { Id = 2, Title = "B", BoxId = 8, IsSystem = false };
        _cardService.GetCardsAsync().Returns(Task.FromResult<IReadOnlyList<PrayerCard>>(
            new[] { c1, c2 }));
        _boxService.GetBoxesAsync().Returns(Task.FromResult<IReadOnlyList<CardBox>>(new[]
        {
            new CardBox { Id = 7, Name = "Family", IsSystem = false },
            new CardBox { Id = 8, Name = "Family", IsSystem = false },
        }));
        var sut = CreateSut();
        await sut.LoadBoxesAsync();
        sut.SetExistingCardModeCommand.Execute(null);
        await Task.Delay(50);

        Assert.Equal(2, sut.AvailableCardGroups.Count);
        var ids = sut.AvailableCardGroups.Select(g => g.BoxId).OrderBy(i => i).ToArray();
        Assert.Equal(new[] { 7, 8 }, ids);
    }

    // ── HasNoAvailableCards change-detection guard ────────────────────────

    [Fact]
    public void HasNoAvailableCards_OnlyFiresPropertyChangedWhenValueActuallyChanges()
    {
        // Clear()+N×Add() fires N+1 CollectionChanged events on
        // AvailableCardGroups, but HasNoAvailableCards transitions at most
        // once (true → false on the first non-empty add). Without the cache,
        // the bound binding would receive a PropertyChanged storm.
        var sut = CreateSut();
        sut.SetExistingCardModeCommand.Execute(null);
        var fired = 0;
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ConfirmImportViewModel.HasNoAvailableCards))
                fired++;
        };

        sut.AvailableCardGroups.Add(new CardCollectionGroup
        {
            BoxId = 1, CollectionName = "A",
            Cards = { new CardPickerItem { CardId = 1, Title = "x" } }
        });
        sut.AvailableCardGroups.Add(new CardCollectionGroup
        {
            BoxId = 2, CollectionName = "B",
            Cards = { new CardPickerItem { CardId = 2, Title = "y" } }
        });
        sut.AvailableCardGroups.Add(new CardCollectionGroup
        {
            BoxId = 3, CollectionName = "C",
            Cards = { new CardPickerItem { CardId = 3, Title = "z" } }
        });

        Assert.Equal(1, fired);
    }

    // ── Dispose idempotency ───────────────────────────────────────────────

    [Fact]
    public void Dispose_IsIdempotent()
    {
        // Page lifecycle can call Dispose() more than once via OnDisappearing
        // (and Dispose() also runs implicitly via the test harness after
        // SaveAsync/CancelAsync). Second call must not throw.
        var sut = CreateSut();

        sut.Dispose();
        var ex = Record.Exception(() => sut.Dispose());

        Assert.Null(ex);
    }

    // ── Helpers ──────────────────────────

    private ConfirmImportViewModel SetupSutWithRows(params (string Title, string? Details)[] rows)
    {
        var raw = "raw payload";
        _payloadService.ConsumePayload().Returns(raw);
        _parser.Parse(raw).Returns(Result("Imported May 1", rows));
        var sut = CreateSut();
        sut.ConsumePending();
        return sut;
    }
}

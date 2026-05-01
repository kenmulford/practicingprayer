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

    private readonly IMessenger _messenger = new WeakReferenceMessenger();
    private readonly object _recipient = new();
    private readonly List<BulkChangedMessage> _bulkMessages = new();

    public ConfirmImportViewModelTests()
    {
        PrayerCard.SetDBService(_db);
        Prayer.SetDBService(_db);
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));
        _messenger.Register<object, BulkChangedMessage>(_recipient, (_, m) => _bulkMessages.Add(m));
    }

    private ConfirmImportViewModel CreateSut() =>
        new(_cardService, _prayerService, _navigationService, _accessibilityService,
            _messenger, _payloadService, _parser);

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
    public async Task Save_NavigatesToCardsTabWithImportedFlag()
    {
        var sut = SetupSutWithRows(("Mom", null));

        await sut.SaveCommand.ExecuteAsync(null);

        await _navigationService.Received(1).GoToAsync(Routes.PrayerCardsTabImported);
    }

    // ── CancelCommand ──────────────────────────

    [Fact]
    public async Task Cancel_PopsModal()
    {
        var sut = CreateSut();

        await sut.CancelCommand.ExecuteAsync(null);

        await _navigationService.Received(1).PopModalAsync();
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

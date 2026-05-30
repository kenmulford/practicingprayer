using CommunityToolkit.Mvvm.Messaging;
using NSubstitute;
using PrayerApp.Messages;
using PrayerApp.Models;
using PrayerApp.Services;

namespace PrayerApp.Tests.Services;

public class CardServiceTests
{
    private readonly IDBService _db;
    // Real messenger — NSubstitute can't proxy CT.Mvvm's Send extension method
    // (the extension's null-check trips on Arg.Any<X>() before reaching the mock).
    // Capture sent messages via a registered handler instead.
    private readonly IMessenger _messenger = new WeakReferenceMessenger();
    private readonly object _recipient = new();
    private readonly List<PrayerCardChangedMessage> _cardMessages = new();
    private readonly CardService _service;

    public CardServiceTests()
    {
        _db = Substitute.For<IDBService>();
        PrayerCard.SetDBService(_db);
        CardBox.SetDBService(_db);
        _db.GetAllAsync<CardBox>().Returns(Task.FromResult(new List<CardBox>()));
        _messenger.Register<object, PrayerCardChangedMessage>(_recipient, (_, m) => _cardMessages.Add(m));
        _service = new CardService(_messenger);
    }

    // ── GetCardsAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCardsAsync_FirstCall_QueriesDatabase()
    {
        _db.GetAllAsync<PrayerCard>().Returns(Task.FromResult(new List<PrayerCard>()));

        await _service.GetCardsAsync();

        await _db.Received(1).GetAllAsync<PrayerCard>();
    }

    [Fact]
    public async Task GetCardsAsync_ReturnsCardsFromDatabase()
    {
        var cards = new List<PrayerCard>
        {
            new() { Id = 1, Title = "Alpha" },
            new() { Id = 2, Title = "Beta" }
        };
        _db.GetAllAsync<PrayerCard>().Returns(Task.FromResult(cards));

        var result = await _service.GetCardsAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c.Title == "Alpha");
        Assert.Contains(result, c => c.Title == "Beta");
    }

    [Fact]
    public async Task GetCardsAsync_SecondCall_UsesCacheNotDatabase()
    {
        _db.GetAllAsync<PrayerCard>().Returns(Task.FromResult(new List<PrayerCard>()));

        await _service.GetCardsAsync();
        await _service.GetCardsAsync();

        // DB should only have been queried once despite two calls
        await _db.Received(1).GetAllAsync<PrayerCard>();
    }

    // ── SaveCardAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveCardAsync_NewCard_InsertsIntoDatabase()
    {
        var card = new PrayerCard { Id = 0, Title = "New Card" };
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));

        await _service.SaveCardAsync(card);

        await _db.Received(1).InsertAsync(Arg.Is<PrayerCard>(c => c.Title == "New Card"));
    }

    [Fact]
    public async Task SaveCardAsync_ReturnsTheSameCard()
    {
        var card = new PrayerCard { Id = 0, Title = "Test" };
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));

        var result = await _service.SaveCardAsync(card);

        Assert.Same(card, result);
    }

    [Fact]
    public async Task SaveCardAsync_InvalidatesCache()
    {
        // Populate cache
        _db.GetAllAsync<PrayerCard>().Returns(Task.FromResult(new List<PrayerCard>()));
        await _service.GetCardsAsync();

        // Save a card — should bust cache
        var card = new PrayerCard { Id = 0, Title = "New" };
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));
        await _service.SaveCardAsync(card);

        // Next GetCardsAsync must query DB again
        await _service.GetCardsAsync();
        await _db.Received(2).GetAllAsync<PrayerCard>();
    }

    // ── DeleteCardAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCardAsync_DeletesFromDatabase()
    {
        var card = new PrayerCard { Id = 5, Title = "To Delete" };
        _db.DeleteAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));

        await _service.DeleteCardAsync(card);

        await _db.Received(1).DeleteAsync(Arg.Is<PrayerCard>(c => c.Id == 5));
    }

    [Fact]
    public async Task DeleteCardAsync_InvalidatesCache()
    {
        // Populate cache
        _db.GetAllAsync<PrayerCard>().Returns(Task.FromResult(new List<PrayerCard>()));
        await _service.GetCardsAsync();

        // Delete a card — should bust cache
        var card = new PrayerCard { Id = 1 };
        _db.DeleteAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));
        await _service.DeleteCardAsync(card);

        // Next GetCardsAsync must query DB again
        await _service.GetCardsAsync();
        await _db.Received(2).GetAllAsync<PrayerCard>();
    }

    // ── GetOrCreateQuickAddCardAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetOrCreateQuickAddCardAsync_NoCardsExist_CreatesSystemCard()
    {
        _db.GetAllAsync<PrayerCard>().Returns(Task.FromResult(new List<PrayerCard>()));
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));

        var result = await _service.GetOrCreateQuickAddCardAsync();

        Assert.Equal("Quick Add", result.Title);
        Assert.True(result.IsSystem);
        await _db.Received(1).InsertAsync(Arg.Is<PrayerCard>(c => c.IsSystem && c.Title == "Quick Add"));
    }

    [Fact]
    public async Task GetOrCreateQuickAddCardAsync_SystemCardExists_ReturnsExisting()
    {
        var existing = new PrayerCard { Id = 42, Title = "Quick Add", IsSystem = true };
        _db.GetAllAsync<PrayerCard>().Returns(Task.FromResult(new List<PrayerCard> { existing }));

        var result = await _service.GetOrCreateQuickAddCardAsync();

        Assert.Equal(42, result.Id);
        Assert.True(result.IsSystem);
        await _db.DidNotReceive().InsertAsync(Arg.Any<PrayerCard>());
    }

    [Fact]
    public async Task GetOrCreateQuickAddCardAsync_OtherCardsExist_CreatesSystemCard()
    {
        var userCard = new PrayerCard { Id = 1, Title = "Family" };
        _db.GetAllAsync<PrayerCard>().Returns(Task.FromResult(new List<PrayerCard> { userCard }));
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));

        var result = await _service.GetOrCreateQuickAddCardAsync();

        Assert.True(result.IsSystem);
        await _db.Received(1).InsertAsync(Arg.Is<PrayerCard>(c => c.IsSystem));
    }

    // ── InvalidateCache ───────────────────────────────────────────────────────

    [Fact]
    public async Task InvalidateCache_ForcesNewQueryOnNextCall()
    {
        _db.GetAllAsync<PrayerCard>().Returns(Task.FromResult(new List<PrayerCard>()));
        await _service.GetCardsAsync(); // populates cache

        _service.InvalidateCache();

        await _service.GetCardsAsync(); // should re-query
        await _db.Received(2).GetAllAsync<PrayerCard>();
    }

    // ── GetOrCreateSharedCardAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetOrCreateSharedCardAsync_NoCardsExist_CreatesSharedCard()
    {
        _db.GetAllAsync<PrayerCard>().Returns(Task.FromResult(new List<PrayerCard>()));
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));

        var result = await _service.GetOrCreateSharedCardAsync();

        Assert.Equal("Shared with me", result.Title);
        Assert.True(result.IsSystem);
        await _db.Received(1).InsertAsync(Arg.Is<PrayerCard>(c => c.IsSystem && c.Title == "Shared with me"));
    }

    [Fact]
    public async Task GetOrCreateSharedCardAsync_SharedCardExists_ReturnsExisting()
    {
        var shared = new PrayerCard { Id = 99, Title = "Shared with me", IsSystem = true };
        _db.GetAllAsync<PrayerCard>().Returns(Task.FromResult(new List<PrayerCard> { shared }));

        var result = await _service.GetOrCreateSharedCardAsync();

        Assert.Equal(99, result.Id);
        Assert.True(result.IsSystem);
        await _db.DidNotReceive().InsertAsync(Arg.Any<PrayerCard>());
    }

    [Fact]
    public async Task GetOrCreateSharedCardAsync_QuickAddExists_CreatesSharedCard()
    {
        var quickAdd = new PrayerCard { Id = 1, Title = "Quick Add", IsSystem = true };
        _db.GetAllAsync<PrayerCard>().Returns(Task.FromResult(new List<PrayerCard> { quickAdd }));
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));

        var result = await _service.GetOrCreateSharedCardAsync();

        Assert.Equal("Shared with me", result.Title);
        Assert.True(result.IsSystem);
    }

    [Fact]
    public async Task GetOrCreateSharedCardAsync_InvalidatesCache()
    {
        _db.GetAllAsync<PrayerCard>().Returns(Task.FromResult(new List<PrayerCard>()));
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));

        await _service.GetOrCreateSharedCardAsync();
        await _service.GetCardsAsync();

        // Should have queried DB twice: once for shared card creation, once after cache bust
        await _db.Received(2).GetAllAsync<PrayerCard>();
    }

    // ── AssignBoxAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AssignBoxAsync_SetsBoxIdAndSaves()
    {
        var card = new PrayerCard { Id = 1, Title = "Test", BoxId = 0 };
        _db.UpdateAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));

        await _service.AssignBoxAsync(card, boxId: 5);

        Assert.Equal(5, card.BoxId);
        await _db.Received(1).UpdateAsync(Arg.Is<PrayerCard>(c => c.BoxId == 5));
    }

    [Fact]
    public async Task AssignBoxAsync_Unboxed_SetsBoxIdZero()
    {
        var card = new PrayerCard { Id = 1, Title = "Test", BoxId = 5 };
        _db.UpdateAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));

        await _service.AssignBoxAsync(card, boxId: 0);

        Assert.Equal(0, card.BoxId);
    }

    [Fact]
    public async Task AssignBoxAsync_InvalidatesCache()
    {
        _db.GetAllAsync<PrayerCard>().Returns(Task.FromResult(new List<PrayerCard>()));
        await _service.GetCardsAsync(); // populate cache

        var card = new PrayerCard { Id = 1, Title = "Test" };
        _db.UpdateAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));
        await _service.AssignBoxAsync(card, boxId: 5);

        await _service.GetCardsAsync(); // should re-query
        await _db.Received(2).GetAllAsync<PrayerCard>();
    }

    // ── BUG-58: System card creation assigns System box ────────────────────

    [Fact]
    public async Task GetOrCreateQuickAddCardAsync_NewCard_AssignsSystemBoxId()
    {
        _db.GetAllAsync<PrayerCard>().Returns(Task.FromResult(new List<PrayerCard>()));
        _db.GetAllAsync<CardBox>().Returns(Task.FromResult(new List<CardBox>
        {
            new() { Id = 10, SystemKey = CardBox.SystemKeySystem, IsSystem = true }
        }));
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));

        var result = await _service.GetOrCreateQuickAddCardAsync();

        Assert.Equal(10, result.BoxId);
    }

    [Fact]
    public async Task GetOrCreateSharedCardAsync_NewCard_AssignsSystemBoxId()
    {
        _db.GetAllAsync<PrayerCard>().Returns(Task.FromResult(new List<PrayerCard>()));
        _db.GetAllAsync<CardBox>().Returns(Task.FromResult(new List<CardBox>
        {
            new() { Id = 7, SystemKey = CardBox.SystemKeySystem, IsSystem = true }
        }));
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));

        var result = await _service.GetOrCreateSharedCardAsync();

        Assert.Equal(7, result.BoxId);
    }

    [Fact]
    public async Task GetOrCreateQuickAddCardAsync_NoBoxesExist_FallsBackToBoxIdZero()
    {
        _db.GetAllAsync<PrayerCard>().Returns(Task.FromResult(new List<PrayerCard>()));
        _db.GetAllAsync<CardBox>().Returns(Task.FromResult(new List<CardBox>()));
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));

        var result = await _service.GetOrCreateQuickAddCardAsync();

        Assert.Equal(0, result.BoxId);
    }

    // ── GetOrCreateQuickAddCardAsync — title matching ────────────────────────

    [Fact]
    public async Task GetOrCreateQuickAddCardAsync_BothSystemCardsExist_ReturnsQuickAddNotShared()
    {
        var quickAdd = new PrayerCard { Id = 1, Title = "Quick Add", IsSystem = true };
        var shared = new PrayerCard { Id = 2, Title = "Shared with me", IsSystem = true };
        _db.GetAllAsync<PrayerCard>().Returns(Task.FromResult(new List<PrayerCard> { shared, quickAdd }));

        var result = await _service.GetOrCreateQuickAddCardAsync();

        Assert.Equal("Quick Add", result.Title);
        Assert.Equal(1, result.Id);
    }

    // ── Messenger publishes ──────────────────────────────────────────────────

    [Fact]
    public async Task SaveCardAsync_New_PublishesCreated()
    {
        var card = new PrayerCard { Title = "Family" };

        await _service.SaveCardAsync(card);

        Assert.Single(_cardMessages);
        Assert.Equal(ChangeKind.Created, _cardMessages[0].Kind);
    }

    [Fact]
    public async Task SaveCardAsync_Existing_PublishesUpdated()
    {
        var card = new PrayerCard { Id = 7, Title = "Family" };

        await _service.SaveCardAsync(card);

        Assert.Single(_cardMessages);
        Assert.Equal(7, _cardMessages[0].CardId);
        Assert.Equal(ChangeKind.Updated, _cardMessages[0].Kind);
    }

    [Fact]
    public async Task AssignBoxAsync_PublishesUpdated()
    {
        var card = new PrayerCard { Id = 9, Title = "Work", BoxId = 0 };

        await _service.AssignBoxAsync(card, boxId: 3);

        Assert.Single(_cardMessages);
        Assert.Equal(9, _cardMessages[0].CardId);
        Assert.Equal(ChangeKind.Updated, _cardMessages[0].Kind);
    }

    [Fact]
    public async Task DeleteCardAsync_PublishesDeleted()
    {
        var card = new PrayerCard { Id = 11, Title = "Old" };

        await _service.DeleteCardAsync(card);

        Assert.Single(_cardMessages);
        Assert.Equal(11, _cardMessages[0].CardId);
        Assert.Equal(ChangeKind.Deleted, _cardMessages[0].Kind);
    }

    // ── SaveCardAsync publishMessage=false ────────────────────────────────────

    [Fact]
    public async Task SaveCardAsync_PublishMessageFalse_DoesNotSendMessage()
    {
        // Bulk-import path: caller suppresses per-entity messages and sends
        // a single BulkChangedMessage after all rows are persisted.
        var card = new PrayerCard { Title = "Family" };
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));

        await _service.SaveCardAsync(card, publishMessage: false);

        Assert.Empty(_cardMessages);
    }

    [Fact]
    public async Task SaveCardAsync_PublishMessageFalse_StillInvalidatesCache()
    {
        // Cache must still be busted even when the message is suppressed —
        // the import batch mutates the DB; the next GetCardsAsync must see
        // fresh data.
        _db.GetAllAsync<PrayerCard>().Returns(Task.FromResult(new List<PrayerCard>()));
        await _service.GetCardsAsync(); // populate cache

        var card = new PrayerCard { Title = "New" };
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));
        await _service.SaveCardAsync(card, publishMessage: false);

        // Next call must re-query, not return stale cache.
        await _service.GetCardsAsync();
        await _db.Received(2).GetAllAsync<PrayerCard>();
    }

    [Fact]
    public async Task SaveCardAsync_DefaultPublishMessage_StillSendsMessage()
    {
        // Guard: adding the optional param must not silently break the default
        // (publishMessage = true) path used by every other caller.
        var card = new PrayerCard { Title = "Family" };
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));

        await _service.SaveCardAsync(card);

        Assert.Single(_cardMessages);
    }
}

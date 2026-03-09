using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;

namespace PrayerApp.Tests.Services;

public class CardServiceTests
{
    private readonly IDBService _db;
    private readonly CardService _service;

    public CardServiceTests()
    {
        _db = Substitute.For<IDBService>();
        PrayerCard.SetDBService(_db);
        _service = new CardService(); // CardService uses Active Record; no IDBService ctor arg
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
}

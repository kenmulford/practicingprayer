using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;

namespace PrayerApp.Tests.Services;

public class PrayerServiceTests
{
    private readonly IDBService _db;
    private readonly PrayerService _service;

    public PrayerServiceTests()
    {
        _db = Substitute.For<IDBService>();
        Prayer.SetDBService(_db);
        _service = new PrayerService();
    }

    // ── GetAllPrayersAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllPrayersAsync_FirstCall_QueriesDatabase()
    {
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(new List<Prayer>()));

        await _service.GetAllPrayersAsync();

        await _db.Received(1).GetAllAsync<Prayer>();
    }

    [Fact]
    public async Task GetAllPrayersAsync_SecondCall_UsesCacheNotDatabase()
    {
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(new List<Prayer>()));

        await _service.GetAllPrayersAsync();
        await _service.GetAllPrayersAsync();

        await _db.Received(1).GetAllAsync<Prayer>();
    }

    [Fact]
    public async Task GetAllPrayersAsync_ReturnsAllPrayers()
    {
        var prayers = new List<Prayer>
        {
            new() { Id = 1, Title = "Pray for health", IsAnswered = false },
            new() { Id = 2, Title = "Pray for job",    IsAnswered = true }
        };
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(prayers));

        var result = await _service.GetAllPrayersAsync();

        Assert.Equal(2, result.Count);
    }

    // ── GetAllActivePrayersAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetAllActivePrayersAsync_ExcludesAnsweredPrayers()
    {
        var prayers = new List<Prayer>
        {
            new() { Id = 1, Title = "Active",   IsAnswered = false },
            new() { Id = 2, Title = "Answered", IsAnswered = true  }
        };
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(prayers));

        var result = await _service.GetAllActivePrayersAsync();

        Assert.Single(result);
        Assert.Equal("Active", result[0].Title);
    }

    [Fact]
    public async Task GetAllActivePrayersAsync_WhenAllAnswered_ReturnsEmpty()
    {
        var prayers = new List<Prayer>
        {
            new() { Id = 1, IsAnswered = true },
            new() { Id = 2, IsAnswered = true }
        };
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(prayers));

        var result = await _service.GetAllActivePrayersAsync();

        Assert.Empty(result);
    }

    // ── GetPrayersByCardAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetPrayersByCardAsync_FirstCall_QueriesDatabase()
    {
        _db.GetPrayersByCardIdAsync(1).Returns(Task.FromResult(new List<Prayer>()));

        await _service.GetPrayersByCardAsync(1);

        await _db.Received(1).GetPrayersByCardIdAsync(1);
    }

    [Fact]
    public async Task GetPrayersByCardAsync_SecondCallSameCard_UsesCacheNotDatabase()
    {
        _db.GetPrayersByCardIdAsync(1).Returns(Task.FromResult(new List<Prayer>()));

        await _service.GetPrayersByCardAsync(1);
        await _service.GetPrayersByCardAsync(1);

        await _db.Received(1).GetPrayersByCardIdAsync(1);
    }

    [Fact]
    public async Task GetPrayersByCardAsync_DifferentCards_QueriesEachOnce()
    {
        _db.GetPrayersByCardIdAsync(1).Returns(Task.FromResult(new List<Prayer>()));
        _db.GetPrayersByCardIdAsync(2).Returns(Task.FromResult(new List<Prayer>()));

        await _service.GetPrayersByCardAsync(1);
        await _service.GetPrayersByCardAsync(2);

        await _db.Received(1).GetPrayersByCardIdAsync(1);
        await _db.Received(1).GetPrayersByCardIdAsync(2);
    }

    // ── SavePrayerAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task SavePrayerAsync_NewPrayer_InsertsIntoDatabase()
    {
        var prayer = new Prayer { Id = 0, Title = "New Prayer" };
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        await _service.SavePrayerAsync(prayer);

        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p => p.Title == "New Prayer"));
    }

    [Fact]
    public async Task SavePrayerAsync_InvalidatesBothCaches()
    {
        // Populate both caches
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(new List<Prayer>()));
        _db.GetPrayersByCardIdAsync(1).Returns(Task.FromResult(new List<Prayer>()));
        await _service.GetAllPrayersAsync();
        await _service.GetPrayersByCardAsync(1);

        // Save a prayer — busts both caches
        var prayer = new Prayer { Id = 0 };
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));
        await _service.SavePrayerAsync(prayer);

        // Each should re-query
        await _service.GetAllPrayersAsync();
        await _service.GetPrayersByCardAsync(1);

        await _db.Received(2).GetAllAsync<Prayer>();
        await _db.Received(2).GetPrayersByCardIdAsync(1);
    }

    // ── DeletePrayerAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task DeletePrayerAsync_DeletesFromDatabase()
    {
        var prayer = new Prayer { Id = 7, Title = "To Delete" };
        _db.DeleteAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        await _service.DeletePrayerAsync(prayer);

        await _db.Received(1).DeleteAsync(Arg.Is<Prayer>(p => p.Id == 7));
    }

    [Fact]
    public async Task DeletePrayerAsync_InvalidatesBothCaches()
    {
        // Populate both caches
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(new List<Prayer>()));
        _db.GetPrayersByCardIdAsync(1).Returns(Task.FromResult(new List<Prayer>()));
        await _service.GetAllPrayersAsync();
        await _service.GetPrayersByCardAsync(1);

        // Delete a prayer — busts both caches
        var prayer = new Prayer { Id = 1 };
        _db.DeleteAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));
        await _service.DeletePrayerAsync(prayer);

        await _service.GetAllPrayersAsync();
        await _service.GetPrayersByCardAsync(1);

        await _db.Received(2).GetAllAsync<Prayer>();
        await _db.Received(2).GetPrayersByCardIdAsync(1);
    }

    // ── InvalidateCache ───────────────────────────────────────────────────────

    [Fact]
    public async Task InvalidateCache_ForcesNewQueryOnNextCall()
    {
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(new List<Prayer>()));
        await _service.GetAllPrayersAsync();

        _service.InvalidateCache();

        await _service.GetAllPrayersAsync();
        await _db.Received(2).GetAllAsync<Prayer>();
    }
}

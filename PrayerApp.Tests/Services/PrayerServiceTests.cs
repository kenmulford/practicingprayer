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
        _service = new PrayerService(_db);
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

    // ── GetOverduePrayersAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetOverduePrayersAsync_NeverPrayed_IsIncluded()
    {
        var prayer = new Prayer { Id = 1, IsAnswered = false };
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(new List<Prayer> { prayer }));
        _db.GetAllAsync<PrayerInteraction>().Returns(Task.FromResult(new List<PrayerInteraction>()));

        var result = await _service.GetOverduePrayersAsync();

        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    [Fact]
    public async Task GetOverduePrayersAsync_PrayedWithinThreshold_NotIncluded()
    {
        var prayer = new Prayer { Id = 1, IsAnswered = false };
        var interaction = new PrayerInteraction { PrayerId = 1, InteractionAt = DateTime.Now.AddDays(-5) };
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(new List<Prayer> { prayer }));
        _db.GetAllAsync<PrayerInteraction>().Returns(Task.FromResult(new List<PrayerInteraction> { interaction }));

        var result = await _service.GetOverduePrayersAsync(dayThreshold: 30);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetOverduePrayersAsync_PrayedBeforeThreshold_IsIncluded()
    {
        var prayer = new Prayer { Id = 1, IsAnswered = false };
        var interaction = new PrayerInteraction { PrayerId = 1, InteractionAt = DateTime.Now.AddDays(-60) };
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(new List<Prayer> { prayer }));
        _db.GetAllAsync<PrayerInteraction>().Returns(Task.FromResult(new List<PrayerInteraction> { interaction }));

        var result = await _service.GetOverduePrayersAsync(dayThreshold: 30);

        Assert.Single(result);
    }

    [Fact]
    public async Task GetOverduePrayersAsync_AnsweredPrayer_NotIncluded()
    {
        var prayer = new Prayer { Id = 1, IsAnswered = true };
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(new List<Prayer> { prayer }));
        _db.GetAllAsync<PrayerInteraction>().Returns(Task.FromResult(new List<PrayerInteraction>()));

        var result = await _service.GetOverduePrayersAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetOverduePrayersAsync_OrderedOldestFirst_NeverPrayedAtFront()
    {
        var pNever = new Prayer { Id = 1, IsAnswered = false };
        var pOld   = new Prayer { Id = 2, IsAnswered = false };
        var pOlder = new Prayer { Id = 3, IsAnswered = false };
        var iOld   = new PrayerInteraction { PrayerId = 2, InteractionAt = DateTime.Now.AddDays(-40) };
        var iOlder = new PrayerInteraction { PrayerId = 3, InteractionAt = DateTime.Now.AddDays(-60) };
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(new List<Prayer> { pNever, pOld, pOlder }));
        _db.GetAllAsync<PrayerInteraction>().Returns(Task.FromResult(new List<PrayerInteraction> { iOld, iOlder }));

        var result = await _service.GetOverduePrayersAsync(dayThreshold: 30);

        Assert.Equal(3, result.Count);
        Assert.Equal(1, result[0].Id); // never prayed → DateTime.MinValue sorts first
        Assert.Equal(3, result[1].Id); // oldest prayed next
        Assert.Equal(2, result[2].Id); // least old last
    }

    [Fact]
    public async Task GetOverduePrayersAsync_CustomThreshold_UsedAsFilter()
    {
        var prayer = new Prayer { Id = 1, IsAnswered = false };
        var interaction = new PrayerInteraction { PrayerId = 1, InteractionAt = DateTime.Now.AddDays(-10) };
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(new List<Prayer> { prayer }));
        _db.GetAllAsync<PrayerInteraction>().Returns(Task.FromResult(new List<PrayerInteraction> { interaction }));

        var overdue7  = await _service.GetOverduePrayersAsync(dayThreshold: 7);
        var overdue30 = await _service.GetOverduePrayersAsync(dayThreshold: 30);

        Assert.Single(overdue7);  // 10 days > 7 day threshold → overdue
        Assert.Empty(overdue30);  // 10 days < 30 day threshold → not overdue
    }
}

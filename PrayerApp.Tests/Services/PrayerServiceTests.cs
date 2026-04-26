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
    public async Task GetOverduePrayersAsync_NeverPrayed_IsExcluded()
    {
        var prayer = new Prayer { Id = 1, IsAnswered = false };
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(new List<Prayer> { prayer }));
        _db.GetLatestInteractionByPrayerAsync().Returns(Task.FromResult(new List<LatestInteractionResult>()));

        var result = await _service.GetOverduePrayersAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetOverduePrayersAsync_PrayedWithinThreshold_NotIncluded()
    {
        var prayer = new Prayer { Id = 1, IsAnswered = false };
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(new List<Prayer> { prayer }));
        _db.GetLatestInteractionByPrayerAsync().Returns(Task.FromResult(new List<LatestInteractionResult>
        {
            new() { PrayerId = 1, LatestInteractionAt = DateTime.Now.AddDays(-5) }
        }));

        var result = await _service.GetOverduePrayersAsync(dayThreshold: 30);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetOverduePrayersAsync_PrayedBeforeThreshold_IsIncluded()
    {
        var prayer = new Prayer { Id = 1, IsAnswered = false };
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(new List<Prayer> { prayer }));
        _db.GetLatestInteractionByPrayerAsync().Returns(Task.FromResult(new List<LatestInteractionResult>
        {
            new() { PrayerId = 1, LatestInteractionAt = DateTime.Now.AddDays(-60) }
        }));

        var result = await _service.GetOverduePrayersAsync(dayThreshold: 30);

        Assert.Single(result);
    }

    [Fact]
    public async Task GetOverduePrayersAsync_AnsweredPrayer_NotIncluded()
    {
        var prayer = new Prayer { Id = 1, IsAnswered = true };
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(new List<Prayer> { prayer }));
        _db.GetLatestInteractionByPrayerAsync().Returns(Task.FromResult(new List<LatestInteractionResult>()));

        var result = await _service.GetOverduePrayersAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetOverduePrayersAsync_OrderedOldestFirst()
    {
        var pNever = new Prayer { Id = 1, IsAnswered = false };
        var pOld   = new Prayer { Id = 2, IsAnswered = false };
        var pOlder = new Prayer { Id = 3, IsAnswered = false };
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(new List<Prayer> { pNever, pOld, pOlder }));
        _db.GetLatestInteractionByPrayerAsync().Returns(Task.FromResult(new List<LatestInteractionResult>
        {
            new() { PrayerId = 2, LatestInteractionAt = DateTime.Now.AddDays(-40) },
            new() { PrayerId = 3, LatestInteractionAt = DateTime.Now.AddDays(-60) }
        }));

        var result = await _service.GetOverduePrayersAsync(dayThreshold: 30);

        Assert.Equal(2, result.Count);   // never-prayed (Id=1) excluded
        Assert.Equal(3, result[0].Id);   // oldest prayed first
        Assert.Equal(2, result[1].Id);   // least old last
    }

    [Fact]
    public async Task GetOverduePrayersAsync_CustomThreshold_UsedAsFilter()
    {
        var prayer = new Prayer { Id = 1, IsAnswered = false };
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(new List<Prayer> { prayer }));
        _db.GetLatestInteractionByPrayerAsync().Returns(Task.FromResult(new List<LatestInteractionResult>
        {
            new() { PrayerId = 1, LatestInteractionAt = DateTime.Now.AddDays(-10) }
        }));

        var overdue7  = await _service.GetOverduePrayersAsync(dayThreshold: 7);
        var overdue30 = await _service.GetOverduePrayersAsync(dayThreshold: 30);

        Assert.Single(overdue7);  // 10 days > 7 day threshold → overdue
        Assert.Empty(overdue30);  // 10 days < 30 day threshold → not overdue
    }

    // ── GetLastInteractionDateAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetLastInteractionDateAsync_NoInteractions_ReturnsNull()
    {
        _db.GetMaxInteractionDateAsync().Returns(Task.FromResult<DateTime?>(null));

        var result = await _service.GetLastInteractionDateAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLastInteractionDateAsync_HasInteractions_ReturnsMaxDate()
    {
        var expected = new DateTime(2026, 3, 15, 10, 30, 0);
        _db.GetMaxInteractionDateAsync().Returns(Task.FromResult<DateTime?>(expected));

        var result = await _service.GetLastInteractionDateAsync();

        Assert.Equal(expected, result);
    }

    // ── DeletePrayerAsync cascade ───────────────────────────────────────────

    [Fact]
    public async Task DeletePrayerAsync_DeletesInteractionsAndJunctionRows()
    {
        var prayer = new Prayer { Id = 42 };
        _db.DeleteInteractionsByPrayerIdAsync(42).Returns(Task.FromResult(3));
        _db.DeleteJunctionRowsByRequestIdAsync(42).Returns(Task.FromResult(1));
        _db.DeleteAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        await _service.DeletePrayerAsync(prayer);

        await _db.Received(1).DeleteInteractionsByPrayerIdAsync(42);
        await _db.Received(1).DeleteJunctionRowsByRequestIdAsync(42);
        await _db.Received(1).DeleteAsync(Arg.Is<Prayer>(p => p.Id == 42));
    }

    // ── GetActivePrayerCountByCardAsync — batching contract ───────────────────
    //
    // The Cards page calls this once per card on every refresh. Bulk-counting was
    // causing an N+1 query storm + UI freeze on Galaxy Ultra. The fix routes through
    // the all-prayers cache so a batch of N calls collapses to 1 DB read.

    [Fact]
    public async Task GetActivePrayerCountByCardAsync_BatchedCalls_HitDatabaseOnce()
    {
        var prayers = new List<Prayer>
        {
            new() { Id = 1, PrayerCardId = 1, IsAnswered = false },
            new() { Id = 2, PrayerCardId = 1, IsAnswered = true  },
            new() { Id = 3, PrayerCardId = 2, IsAnswered = false },
            new() { Id = 4, PrayerCardId = 3, IsAnswered = false },
            new() { Id = 5, PrayerCardId = 3, IsAnswered = false },
        };
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(prayers));

        var c1 = await _service.GetActivePrayerCountByCardAsync(1);
        var c2 = await _service.GetActivePrayerCountByCardAsync(2);
        var c3 = await _service.GetActivePrayerCountByCardAsync(3);
        var c1Again = await _service.GetActivePrayerCountByCardAsync(1);

        Assert.Equal(1, c1);
        Assert.Equal(1, c2);
        Assert.Equal(2, c3);
        Assert.Equal(1, c1Again);
        await _db.Received(1).GetAllAsync<Prayer>();
        // Critically: never the per-card path on the count-only flow.
        await _db.DidNotReceive().GetPrayersByCardIdAsync(Arg.Any<int>());
    }

    [Fact]
    public async Task GetActivePrayerCountByCardAsync_AfterPrayerSave_ReReadsDatabase()
    {
        // Staleness guard: SavePrayerAsync invalidates _allCache. The next count call
        // must re-read from DB, not return a stale count.
        var initial = new List<Prayer>
        {
            new() { Id = 1, PrayerCardId = 1, IsAnswered = false },
        };
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(initial));
        Assert.Equal(1, await _service.GetActivePrayerCountByCardAsync(1));

        // Save a new prayer on card 1 → cache invalidated.
        var newPrayer = new Prayer { Id = 2, PrayerCardId = 1, IsAnswered = false };
        await _service.SavePrayerAsync(newPrayer);

        // Next read must hit the DB again, this time returning both rows.
        var afterSave = new List<Prayer>
        {
            new() { Id = 1, PrayerCardId = 1, IsAnswered = false },
            new() { Id = 2, PrayerCardId = 1, IsAnswered = false },
        };
        _db.GetAllAsync<Prayer>().Returns(Task.FromResult(afterSave));
        Assert.Equal(2, await _service.GetActivePrayerCountByCardAsync(1));

        await _db.Received(2).GetAllAsync<Prayer>();
    }
}

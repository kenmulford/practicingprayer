using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;

namespace PrayerApp.Tests.Services;

public class TagServiceTests
{
    private readonly IDBService _db;
    private readonly TagService _service;

    public TagServiceTests()
    {
        _db = Substitute.For<IDBService>();
        PrayerTag.SetDBService(_db);
        PrayerCardTag.SetDBService(_db);
        _service = new TagService(_db);
    }

    // ── GetTagsAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTagsAsync_ReturnsSortedByName()
    {
        var tags = new List<PrayerTag>
        {
            new() { Id = 1, Name = "Work" },
            new() { Id = 2, Name = "Family" },
            new() { Id = 3, Name = "Urgent" }
        };
        _db.GetAllAsync<PrayerTag>().Returns(Task.FromResult(tags));

        var result = await _service.GetTagsAsync();

        Assert.Equal("Family", result[0].Name);
        Assert.Equal("Urgent", result[1].Name);
        Assert.Equal("Work",   result[2].Name);
    }

    [Fact]
    public async Task GetTagsAsync_SecondCall_UsesCacheNotDatabase()
    {
        _db.GetAllAsync<PrayerTag>().Returns(Task.FromResult(new List<PrayerTag>()));

        await _service.GetTagsAsync();
        await _service.GetTagsAsync();

        await _db.Received(1).GetAllAsync<PrayerTag>();
    }

    // ── GetTagsByCardIdAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetTagsByCardIdAsync_ReturnsTagsForSpecifiedCard()
    {
        var allTags = new List<PrayerTag>
        {
            new() { Id = 1, Name = "Family" },
            new() { Id = 2, Name = "Work" }
        };
        var cardTags = new List<PrayerCardTag>
        {
            new() { PrayerCardId = 5, PrayerTagId = 1 }
        };
        _db.GetAllAsync<PrayerTag>().Returns(Task.FromResult(allTags));
        _db.GetByCardIdAsync(5).Returns(Task.FromResult(cardTags));

        var result = await _service.GetTagsByCardIdAsync(5);

        Assert.Single(result);
        Assert.Equal("Family", result[0].Name);
    }

    [Fact]
    public async Task GetTagsByCardIdAsync_CardWithNoTags_ReturnsEmpty()
    {
        _db.GetAllAsync<PrayerTag>().Returns(Task.FromResult(new List<PrayerTag>
        {
            new() { Id = 1, Name = "Work" }
        }));
        _db.GetByCardIdAsync(99).Returns(Task.FromResult(new List<PrayerCardTag>()));

        var result = await _service.GetTagsByCardIdAsync(99);

        Assert.Empty(result);
    }

    // ── AddTagToCardAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task AddTagToCardAsync_InsertsJunctionRecord()
    {
        _db.InsertAsync(Arg.Any<PrayerCardTag>()).Returns(Task.FromResult(1));

        await _service.AddTagToCardAsync(prayerCardId: 3, prayerTagId: 7);

        await _db.Received(1).InsertAsync(Arg.Is<PrayerCardTag>(
            ct => ct.PrayerCardId == 3 && ct.PrayerTagId == 7));
    }

    [Fact]
    public async Task AddTagToCardAsync_InvalidatesCache()
    {
        // Populate tag cache
        _db.GetAllAsync<PrayerTag>().Returns(Task.FromResult(new List<PrayerTag>()));
        await _service.GetTagsAsync();

        _db.InsertAsync(Arg.Any<PrayerCardTag>()).Returns(Task.FromResult(1));
        await _service.AddTagToCardAsync(1, 2);

        await _service.GetTagsAsync();
        await _db.Received(2).GetAllAsync<PrayerTag>();
    }

    // ── RemoveTagFromCardAsync ────────────────────────────────────────────────

    [Fact]
    public async Task RemoveTagFromCardAsync_DeletesJunctionRecord()
    {
        var cardTags = new List<PrayerCardTag>
        {
            new() { Id = 10, PrayerCardId = 3, PrayerTagId = 7 }
        };
        _db.GetByCardIdAsync(3).Returns(Task.FromResult(cardTags));
        _db.DeleteAsync(Arg.Any<PrayerCardTag>()).Returns(Task.FromResult(1));

        await _service.RemoveTagFromCardAsync(prayerCardId: 3, prayerTagId: 7);

        await _db.Received(1).DeleteAsync(Arg.Is<PrayerCardTag>(ct => ct.Id == 10));
    }

    [Fact]
    public async Task RemoveTagFromCardAsync_TagNotPresent_DoesNotDelete()
    {
        _db.GetByCardIdAsync(3).Returns(Task.FromResult(new List<PrayerCardTag>()));

        var result = await _service.RemoveTagFromCardAsync(prayerCardId: 3, prayerTagId: 99);

        Assert.Equal(0, result);
        await _db.DidNotReceive().DeleteAsync(Arg.Any<PrayerCardTag>());
    }

    // ── SaveTagAsync / DeleteTagAsync ─────────────────────────────────────────

    [Fact]
    public async Task SaveTagAsync_NewTag_InsertsIntoDatabase()
    {
        var tag = new PrayerTag { Id = 0, Name = "Health" };
        _db.InsertAsync(Arg.Any<PrayerTag>()).Returns(Task.FromResult(1));

        await _service.SaveTagAsync(tag);

        await _db.Received(1).InsertAsync(Arg.Is<PrayerTag>(t => t.Name == "Health"));
    }

    [Fact]
    public async Task SaveTagAsync_InvalidatesCache()
    {
        _db.GetAllAsync<PrayerTag>().Returns(Task.FromResult(new List<PrayerTag>()));
        await _service.GetTagsAsync();

        var tag = new PrayerTag { Id = 0, Name = "New" };
        _db.InsertAsync(Arg.Any<PrayerTag>()).Returns(Task.FromResult(1));
        await _service.SaveTagAsync(tag);

        await _service.GetTagsAsync();
        await _db.Received(2).GetAllAsync<PrayerTag>();
    }

    [Fact]
    public async Task DeleteTagAsync_DeletesFromDatabase()
    {
        var tag = new PrayerTag { Id = 4, Name = "Old" };
        _db.DeleteAsync(Arg.Any<PrayerTag>()).Returns(Task.FromResult(1));
        _db.GetByTagIdAsync(4).Returns(Task.FromResult(new List<PrayerCardTag>()));
        _db.GetByIdAsync<PrayerTag>(4).Returns(Task.FromResult(tag));

        await _service.DeleteTagAsync(tag.Id);

        await _db.Received(1).DeleteAsync(Arg.Is<PrayerTag>(t => t.Id == 4));
    }

    [Fact]
    public async Task DeleteTagAsync_InvalidatesCache()
    {
        _db.GetAllAsync<PrayerTag>().Returns(Task.FromResult(new List<PrayerTag>()));
        await _service.GetTagsAsync();

        var tag = new PrayerTag { Id = 1 };
        _db.DeleteAsync(Arg.Any<PrayerTag>()).Returns(Task.FromResult(1));
        _db.GetByTagIdAsync(1).Returns(Task.FromResult(new List<PrayerCardTag>()));
        _db.GetByIdAsync<PrayerTag>(1).Returns(Task.FromResult(tag));
        await _service.DeleteTagAsync(tag.Id);

        await _service.GetTagsAsync();
        await _db.Received(2).GetAllAsync<PrayerTag>();
    }

    // ── GetPrayerIdsByTagIdsAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetPrayerIdsByTagIdsAsync_ReturnsUnionOfCardIds()
    {
        // Tag 1 is on cards 10 and 20; tag 2 is on card 10 only
        _db.GetByTagIdAsync(1).Returns(Task.FromResult(new List<PrayerCardTag>
        {
            new() { PrayerCardId = 10, PrayerTagId = 1 },
            new() { PrayerCardId = 20, PrayerTagId = 1 }
        }));
        _db.GetByTagIdAsync(2).Returns(Task.FromResult(new List<PrayerCardTag>
        {
            new() { PrayerCardId = 10, PrayerTagId = 2 }
        }));

        var result = await _service.GetPrayerIdsByTagIdsAsync(new[] { 1, 2 });

        // Union: 10 and 20 (no duplicates)
        Assert.Equal(2, result.Count);
        Assert.Contains(10, result);
        Assert.Contains(20, result);
    }

    [Fact]
    public async Task GetPrayerIdsByTagIdsAsync_NoMatchingTags_ReturnsEmpty()
    {
        _db.GetByTagIdAsync(Arg.Any<int>()).Returns(Task.FromResult(new List<PrayerCardTag>()));

        var result = await _service.GetPrayerIdsByTagIdsAsync(new[] { 99 });

        Assert.Empty(result);
    }
}

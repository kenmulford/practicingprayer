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

    // ── GetTagsByRequestIdAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetTagsByRequestIdAsync_ReturnsTagsForSpecifiedRequest()
    {
        var allTags = new List<PrayerTag>
        {
            new() { Id = 1, Name = "Family" },
            new() { Id = 2, Name = "Work" }
        };
        var requestTags = new List<PrayerCardTag>
        {
            new() { PrayerRequestId = 5, PrayerTagId = 1 }
        };
        _db.GetAllAsync<PrayerTag>().Returns(Task.FromResult(allTags));
        _db.GetByRequestIdAsync(5).Returns(Task.FromResult(requestTags));

        var result = await _service.GetTagsByRequestIdAsync(5);

        Assert.Single(result);
        Assert.Equal("Family", result[0].Name);
    }

    [Fact]
    public async Task GetTagsByRequestIdAsync_RequestWithNoTags_ReturnsEmpty()
    {
        _db.GetAllAsync<PrayerTag>().Returns(Task.FromResult(new List<PrayerTag>
        {
            new() { Id = 1, Name = "Work" }
        }));
        _db.GetByRequestIdAsync(99).Returns(Task.FromResult(new List<PrayerCardTag>()));

        var result = await _service.GetTagsByRequestIdAsync(99);

        Assert.Empty(result);
    }

    // ── AddTagToRequestAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task AddTagToRequestAsync_InsertsJunctionRecord()
    {
        _db.InsertAsync(Arg.Any<PrayerCardTag>()).Returns(Task.FromResult(1));

        await _service.AddTagToRequestAsync(prayerRequestId: 3, prayerTagId: 7);

        await _db.Received(1).InsertAsync(Arg.Is<PrayerCardTag>(
            ct => ct.PrayerRequestId == 3 && ct.PrayerTagId == 7));
    }

    [Fact]
    public async Task AddTagToRequestAsync_InvalidatesCache()
    {
        _db.GetAllAsync<PrayerTag>().Returns(Task.FromResult(new List<PrayerTag>()));
        await _service.GetTagsAsync();

        _db.InsertAsync(Arg.Any<PrayerCardTag>()).Returns(Task.FromResult(1));
        await _service.AddTagToRequestAsync(1, 2);

        await _service.GetTagsAsync();
        await _db.Received(2).GetAllAsync<PrayerTag>();
    }

    // ── RemoveTagFromRequestAsync ─────────────────────────────────────────────

    [Fact]
    public async Task RemoveTagFromRequestAsync_DeletesJunctionRecord()
    {
        var requestTags = new List<PrayerCardTag>
        {
            new() { Id = 10, PrayerRequestId = 3, PrayerTagId = 7 }
        };
        _db.GetByRequestIdAsync(3).Returns(Task.FromResult(requestTags));
        _db.DeleteAsync(Arg.Any<PrayerCardTag>()).Returns(Task.FromResult(1));

        await _service.RemoveTagFromRequestAsync(prayerRequestId: 3, prayerTagId: 7);

        await _db.Received(1).DeleteAsync(Arg.Is<PrayerCardTag>(ct => ct.Id == 10));
    }

    [Fact]
    public async Task RemoveTagFromRequestAsync_TagNotPresent_DoesNotDelete()
    {
        _db.GetByRequestIdAsync(3).Returns(Task.FromResult(new List<PrayerCardTag>()));

        var result = await _service.RemoveTagFromRequestAsync(prayerRequestId: 3, prayerTagId: 99);

        Assert.Equal(0, result);
        await _db.DidNotReceive().DeleteAsync(Arg.Any<PrayerCardTag>());
    }

    // ── GetRequestIdsByTagIdsAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetRequestIdsByTagIdsAsync_ReturnsUnionOfRequestIds()
    {
        // Tag 1 is on requests 10 and 20; tag 2 is on request 10 only
        _db.GetByTagIdAsync(1).Returns(Task.FromResult(new List<PrayerCardTag>
        {
            new() { PrayerRequestId = 10, PrayerTagId = 1 },
            new() { PrayerRequestId = 20, PrayerTagId = 1 }
        }));
        _db.GetByTagIdAsync(2).Returns(Task.FromResult(new List<PrayerCardTag>
        {
            new() { PrayerRequestId = 10, PrayerTagId = 2 }
        }));

        var result = await _service.GetRequestIdsByTagIdsAsync(new[] { 1, 2 });

        Assert.Equal(2, result.Count);
        Assert.Contains(10, result);
        Assert.Contains(20, result);
    }

    [Fact]
    public async Task GetRequestIdsByTagIdsAsync_NoMatchingTags_ReturnsEmpty()
    {
        _db.GetByTagIdAsync(Arg.Any<int>()).Returns(Task.FromResult(new List<PrayerCardTag>()));

        var result = await _service.GetRequestIdsByTagIdsAsync(new[] { 99 });

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRequestIdsByTagIdsAsync_SkipsLegacyCardLevelRows()
    {
        // Legacy rows have PrayerRequestId == 0 and should be excluded
        _db.GetByTagIdAsync(1).Returns(Task.FromResult(new List<PrayerCardTag>
        {
            new() { PrayerCardId = 5, PrayerRequestId = 0, PrayerTagId = 1 }, // legacy
            new() { PrayerRequestId = 42, PrayerTagId = 1 }                  // current
        }));

        var result = await _service.GetRequestIdsByTagIdsAsync(new[] { 1 });

        Assert.Single(result);
        Assert.Contains(42, result);
    }

    // ── SeedSystemTagsAsync ─────────────────────────────────────────────

    [Fact]
    public async Task SeedSystemTagsAsync_CreatesRecentlyNotifiedTag_WhenMissing()
    {
        _db.GetAllAsync<PrayerTag>().Returns(Task.FromResult(new List<PrayerTag>()));
        _db.InsertAsync(Arg.Any<PrayerTag>()).Returns(Task.FromResult(1));

        await _service.SeedSystemTagsAsync();

        await _db.Received(1).InsertAsync(Arg.Is<PrayerTag>(t =>
            t.Name == TagService.RecentlyNotifiedTagName && t.IsSystem == true));
    }

    [Fact]
    public async Task SeedSystemTagsAsync_SkipsInsert_WhenTagAlreadyExists()
    {
        var existing = new PrayerTag { Id = 1, Name = TagService.RecentlyNotifiedTagName, IsSystem = true };
        _db.GetAllAsync<PrayerTag>().Returns(Task.FromResult(new List<PrayerTag> { existing }));

        await _service.SeedSystemTagsAsync();

        await _db.DidNotReceive().InsertAsync(Arg.Any<PrayerTag>());
    }

    // ── GetSystemTagAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetSystemTagAsync_ReturnsTag_WhenExists()
    {
        var systemTag = new PrayerTag { Id = 5, Name = TagService.RecentlyNotifiedTagName, IsSystem = true };
        _db.GetAllAsync<PrayerTag>().Returns(Task.FromResult(new List<PrayerTag>
        {
            new() { Id = 1, Name = "Work" },
            systemTag
        }));

        var result = await _service.GetSystemTagAsync(TagService.RecentlyNotifiedTagName);

        Assert.NotNull(result);
        Assert.Equal(5, result!.Id);
        Assert.True(result.IsSystem);
    }

    [Fact]
    public async Task GetSystemTagAsync_ReturnsNull_WhenNotFound()
    {
        _db.GetAllAsync<PrayerTag>().Returns(Task.FromResult(new List<PrayerTag>
        {
            new() { Id = 1, Name = "Work" }
        }));

        var result = await _service.GetSystemTagAsync(TagService.RecentlyNotifiedTagName);

        Assert.Null(result);
    }

    // ── DeleteTagAsync — system tag protection ────────────────────────────

    [Fact]
    public async Task DeleteTagAsync_SystemTag_RefusesToDelete()
    {
        var systemTag = new PrayerTag { Id = 1, Name = TagService.RecentlyNotifiedTagName, IsSystem = true };
        _db.GetByTagIdAsync(1).Returns(Task.FromResult(new List<PrayerCardTag>()));
        _db.GetByIdAsync<PrayerTag>(1).Returns(Task.FromResult(systemTag));

        await _service.DeleteTagAsync(1);

        await _db.DidNotReceive().DeleteAsync(Arg.Any<PrayerTag>());
    }

    // ── ReassignColorAsync ──────────────────────────────────────────────

    [Fact]
    public async Task ReassignColorAsync_UpdatesMatchingTags()
    {
        var tag1 = new PrayerTag { Id = 1, Name = "Work", Color = "#FF5500" };
        var tag2 = new PrayerTag { Id = 2, Name = "Family", Color = "#00FF00" };
        var tag3 = new PrayerTag { Id = 3, Name = "Urgent", Color = "#FF5500" };
        _db.GetAllAsync<PrayerTag>().Returns(Task.FromResult(new List<PrayerTag> { tag1, tag2, tag3 }));
        _db.UpdateAsync(Arg.Any<PrayerTag>()).Returns(Task.FromResult(1));

        await _service.ReassignColorAsync("#FF5500", "#B84040");

        Assert.Equal("#B84040", tag1.Color);
        Assert.Equal("#00FF00", tag2.Color); // unchanged
        Assert.Equal("#B84040", tag3.Color);
        await _db.Received(2).UpdateAsync(Arg.Any<PrayerTag>());
    }

    [Fact]
    public async Task ReassignColorAsync_NoMatches_DoesNotUpdate()
    {
        var tag = new PrayerTag { Id = 1, Name = "Work", Color = "#00FF00" };
        _db.GetAllAsync<PrayerTag>().Returns(Task.FromResult(new List<PrayerTag> { tag }));

        await _service.ReassignColorAsync("#FF5500", "#B84040");

        Assert.Equal("#00FF00", tag.Color);
        await _db.DidNotReceive().UpdateAsync(Arg.Any<PrayerTag>());
    }
}

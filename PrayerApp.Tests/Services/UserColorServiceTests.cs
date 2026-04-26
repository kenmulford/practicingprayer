using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;

namespace PrayerApp.Tests.Services;

public class UserColorServiceTests
{
    private readonly IDBService _db;
    private readonly UserColorService _service;

    public UserColorServiceTests()
    {
        _db = Substitute.For<IDBService>();
        _service = new UserColorService(_db);
    }

    // ── GetColorsAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetColorsAsync_ReturnsColorsOrderedByCreatedAt()
    {
        var colors = new List<UserColor>
        {
            new() { Id = 1, HexValue = "#FF0000", CreatedAt = new DateTime(2025, 3, 3) },
            new() { Id = 2, HexValue = "#00FF00", CreatedAt = new DateTime(2025, 1, 1) },
            new() { Id = 3, HexValue = "#0000FF", CreatedAt = new DateTime(2025, 2, 2) },
        };
        _db.GetAllAsync<UserColor>().Returns(Task.FromResult(colors));

        var result = await _service.GetColorsAsync();

        Assert.Equal(3, result.Count);
        Assert.Equal("#00FF00", result[0].HexValue);
        Assert.Equal("#0000FF", result[1].HexValue);
        Assert.Equal("#FF0000", result[2].HexValue);
    }

    [Fact]
    public async Task GetColorsAsync_Empty_ReturnsEmptyList()
    {
        _db.GetAllAsync<UserColor>().Returns(Task.FromResult(new List<UserColor>()));

        var result = await _service.GetColorsAsync();

        Assert.Empty(result);
    }

    // ── SaveColorAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SaveColorAsync_NewColor_InsertsAndReturns()
    {
        _db.GetAllAsync<UserColor>().Returns(Task.FromResult(new List<UserColor>()));
        _db.InsertAsync(Arg.Any<UserColor>()).Returns(Task.FromResult(1));

        var result = await _service.SaveColorAsync("#aaBB00");

        Assert.Equal("#AABB00", result.HexValue); // normalized to uppercase
        await _db.Received(1).InsertAsync(Arg.Is<UserColor>(c => c.HexValue == "#AABB00"));
    }

    [Fact]
    public async Task SaveColorAsync_DuplicateHex_ReturnsExistingWithoutInsert()
    {
        var existing = new UserColor { Id = 5, HexValue = "#B84040" };
        _db.GetAllAsync<UserColor>().Returns(Task.FromResult(new List<UserColor> { existing }));

        var result = await _service.SaveColorAsync("#b84040"); // lowercase input

        Assert.Equal(5, result.Id);
        Assert.Equal("#B84040", result.HexValue);
        await _db.DidNotReceive().InsertAsync(Arg.Any<UserColor>());
    }

    [Fact]
    public async Task SaveColorAsync_DuplicateCheck_IsCaseInsensitive()
    {
        var existing = new UserColor { Id = 1, HexValue = "#AABB00" };
        _db.GetAllAsync<UserColor>().Returns(Task.FromResult(new List<UserColor> { existing }));

        var result = await _service.SaveColorAsync("#AaBb00");

        Assert.Same(existing, result);
        await _db.DidNotReceive().InsertAsync(Arg.Any<UserColor>());
    }

    // ── DeleteColorAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteColorAsync_CustomColor_DeletesFromDatabase()
    {
        var color = new UserColor { Id = 3, HexValue = "#123456", IsDefault = false };
        _db.GetByIdAsync<UserColor>(3).Returns(Task.FromResult(color));
        _db.DeleteAsync(Arg.Any<UserColor>()).Returns(Task.FromResult(1));

        await _service.DeleteColorAsync(3);

        await _db.Received(1).DeleteAsync(Arg.Is<UserColor>(c => c.Id == 3));
    }

    [Fact]
    public async Task DeleteColorAsync_DefaultColor_ProtectedFromDeletion()
    {
        var color = new UserColor { Id = 1, HexValue = "#B84040", IsDefault = true };
        _db.GetByIdAsync<UserColor>(1).Returns(Task.FromResult(color));

        await _service.DeleteColorAsync(1);

        await _db.DidNotReceive().DeleteAsync(Arg.Any<UserColor>());
    }

    [Fact]
    public async Task DeleteColorAsync_NonExistentId_DoesNotDelete()
    {
        _db.GetByIdAsync<UserColor>(99).Returns(Task.FromResult<UserColor>(null!));

        await _service.DeleteColorAsync(99);

        await _db.DidNotReceive().DeleteAsync(Arg.Any<UserColor>());
    }

    // ── GetFirstDefaultHex ───────────────────────────────────────────────

    [Fact]
    public void GetFirstDefaultHex_ReturnsFirstPaletteColor()
    {
        var hex = _service.GetFirstDefaultHex();

        Assert.Equal("#B84040", hex);
    }

    // ── SeedDefaultsAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task SeedDefaultsAsync_EmptyTable_InsertsEightDefaults()
    {
        _db.GetAllAsync<UserColor>().Returns(Task.FromResult(new List<UserColor>()));
        _db.InsertAsync(Arg.Any<UserColor>()).Returns(Task.FromResult(1));

        await _service.SeedDefaultsAsync();

        await _db.Received(8).InsertAsync(Arg.Any<UserColor>());
    }

    [Fact]
    public async Task SeedDefaultsAsync_AlreadySeeded_DoesNotInsert()
    {
        _db.GetAllAsync<UserColor>().Returns(Task.FromResult(new List<UserColor>
        {
            new() { Id = 1, HexValue = "#B84040" }
        }));

        await _service.SeedDefaultsAsync();

        await _db.DidNotReceive().InsertAsync(Arg.Any<UserColor>());
    }

    [Fact]
    public async Task SeedDefaultsAsync_InsertsUppercaseHexValues()
    {
        _db.GetAllAsync<UserColor>().Returns(Task.FromResult(new List<UserColor>()));
        var insertedValues = new List<string>();
        _db.InsertAsync(Arg.Any<UserColor>()).Returns(ci =>
        {
            insertedValues.Add(ci.Arg<UserColor>().HexValue);
            return Task.FromResult(1);
        });

        await _service.SeedDefaultsAsync();

        foreach (var hex in insertedValues)
        {
            Assert.StartsWith("#", hex);
            Assert.Equal(hex, hex.ToUpperInvariant());
        }
    }

    // ── Tag deletion does NOT cascade to UserColor ──────────────────────────

    [Fact]
    public async Task DeleteTag_DoesNotDeleteUserColor()
    {
        // This test verifies at the TagService level that deleting a tag
        // does not interact with UserColor at all.
        var tagDb = Substitute.For<IDBService>();
        var tagMessenger = Substitute.For<CommunityToolkit.Mvvm.Messaging.IMessenger>();
        PrayerTag.SetDBService(tagDb);
        PrayerCardTag.SetDBService(tagDb);
        var tagService = new TagService(tagDb, tagMessenger);

        var tag = new PrayerTag { Id = 1, Name = "Work", Color = "#FF5500" };
        tagDb.GetByIdAsync<PrayerTag>(1).Returns(Task.FromResult(tag));
        tagDb.GetByTagIdAsync(1).Returns(Task.FromResult(new List<PrayerCardTag>()));
        tagDb.DeleteAsync(Arg.Any<PrayerTag>()).Returns(Task.FromResult(1));

        await tagService.DeleteTagAsync(1);

        // Only PrayerTag was deleted — no UserColor interaction
        await tagDb.Received(1).DeleteAsync(Arg.Is<PrayerTag>(t => t.Id == 1));
        await tagDb.DidNotReceive().DeleteAsync(Arg.Any<UserColor>());
        await tagDb.DidNotReceive().GetAllAsync<UserColor>();
        await tagDb.DidNotReceive().GetByIdAsync<UserColor>(Arg.Any<int>());
    }
}

using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;

namespace PrayerApp.Tests.Services;

public class PrayerInteractionServiceTests
{
    private readonly IDBService _db;
    private readonly PrayerInteractionService _service;

    public PrayerInteractionServiceTests()
    {
        _db = Substitute.For<IDBService>();
        PrayerInteraction.SetDBService(_db);
        _service = new PrayerInteractionService();
    }

    [Fact]
    public async Task LogInteractionAsync_InsertsInteractionRecord()
    {
        _db.InsertAsync(Arg.Any<PrayerInteraction>()).Returns(Task.FromResult(1));

        await _service.LogInteractionAsync(prayerId: 42);

        await _db.Received(1).InsertAsync(Arg.Any<PrayerInteraction>());
    }

    [Fact]
    public async Task LogInteractionAsync_SetsCorrectPrayerId()
    {
        PrayerInteraction? captured = null;
        _db.InsertAsync(Arg.Do<PrayerInteraction>(i => captured = i))
           .Returns(Task.FromResult(1));

        await _service.LogInteractionAsync(prayerId: 42);

        Assert.NotNull(captured);
        Assert.Equal(42, captured.PrayerId);
    }

    [Fact]
    public async Task LogInteractionAsync_SetsInteractionTypeToPrayed()
    {
        PrayerInteraction? captured = null;
        _db.InsertAsync(Arg.Do<PrayerInteraction>(i => captured = i))
           .Returns(Task.FromResult(1));

        await _service.LogInteractionAsync(prayerId: 1);

        Assert.NotNull(captured);
        Assert.Equal("Prayed", captured.InteractionType);
    }

    [Fact]
    public async Task LogInteractionAsync_CalledTwice_InsertsTwice()
    {
        _db.InsertAsync(Arg.Any<PrayerInteraction>()).Returns(Task.FromResult(1));

        await _service.LogInteractionAsync(prayerId: 1);
        await _service.LogInteractionAsync(prayerId: 2);

        await _db.Received(2).InsertAsync(Arg.Any<PrayerInteraction>());
    }
}

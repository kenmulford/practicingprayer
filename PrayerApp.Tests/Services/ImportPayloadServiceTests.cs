using PrayerApp.Services;

namespace PrayerApp.Tests.Services;

public class ImportPayloadServiceTests
{
    private readonly ImportPayloadService _sut = new();

    [Fact]
    public void ConsumePayload_WhenNothingStaged_ReturnsNull()
    {
        Assert.Null(_sut.ConsumePayload());
    }

    [Fact]
    public void StagePayload_ThenConsume_ReturnsStagedValue()
    {
        _sut.StagePayload("hello");

        Assert.Equal("hello", _sut.ConsumePayload());
    }

    [Fact]
    public void Consume_IsOneShot_SecondCallReturnsNull()
    {
        _sut.StagePayload("hello");

        _ = _sut.ConsumePayload();

        Assert.Null(_sut.ConsumePayload());
    }

    [Fact]
    public void StagePayload_TwiceWithoutConsume_LatestWins()
    {
        // Repeated extension fires while the modal is still open: latest payload
        // is what the user just selected — it should overwrite the unread first.
        _sut.StagePayload("first");
        _sut.StagePayload("second");

        Assert.Equal("second", _sut.ConsumePayload());
    }

    [Fact]
    public async Task StagePayload_ConcurrentWrites_ResultIsOneOfTheStagedValues()
    {
        // Smoke check on the lock — interleaved writers must not produce a torn
        // or null value when a reader fires after both writers complete.
        var values = Enumerable.Range(0, 100).Select(i => $"v{i}").ToArray();

        await Task.WhenAll(values.Select(v => Task.Run(() => _sut.StagePayload(v))));
        var consumed = _sut.ConsumePayload();

        Assert.NotNull(consumed);
        Assert.Contains(consumed, values);
    }
}

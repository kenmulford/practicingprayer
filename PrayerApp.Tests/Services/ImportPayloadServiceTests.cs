using PrayerApp.Models;
using PrayerApp.Services;

namespace PrayerApp.Tests.Services;

public class ImportPayloadServiceTests
{
    private readonly ImportPayloadService _sut = new();

    private static ParseResult SampleStaged(string title = "card") =>
        new(new[] { new ParsedPrayer("p1", null) }, title);

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

    // ── Structured channel (deep-link / .prayercard inbound) ───────────────

    [Fact]
    public void ConsumeStructured_WhenNothingStaged_ReturnsNull()
    {
        Assert.Null(_sut.ConsumeStructured());
    }

    [Fact]
    public void StageStructured_ThenConsume_ReturnsStagedValue()
    {
        var staged = SampleStaged("My Card");

        _sut.StageStructured(staged);

        Assert.Same(staged, _sut.ConsumeStructured());
    }

    [Fact]
    public void ConsumeStructured_IsOneShot_SecondCallReturnsNull()
    {
        _sut.StageStructured(SampleStaged());

        _ = _sut.ConsumeStructured();

        Assert.Null(_sut.ConsumeStructured());
    }

    [Fact]
    public void StageStructured_TwiceWithoutConsume_LatestWins()
    {
        var first = SampleStaged("first");
        var second = SampleStaged("second");

        _sut.StageStructured(first);
        _sut.StageStructured(second);

        Assert.Same(second, _sut.ConsumeStructured());
    }

    [Fact]
    public void StageStructured_DoesNotAffectRawSlot()
    {
        // Independence: the structured slot and raw slot are separate channels.
        // Staging structured must not clobber a previously-staged raw payload.
        _sut.StagePayload("raw");
        _sut.StageStructured(SampleStaged());

        Assert.Equal("raw", _sut.ConsumePayload());
    }

    [Fact]
    public void StagePayload_DoesNotAffectStructuredSlot()
    {
        // Independence in the other direction.
        var staged = SampleStaged();
        _sut.StageStructured(staged);
        _sut.StagePayload("raw");

        Assert.Same(staged, _sut.ConsumeStructured());
    }

    [Fact]
    public void ConsumeStructured_DoesNotDrainRawSlot()
    {
        // Reading one slot must not drain the other.
        _sut.StagePayload("raw");
        _sut.StageStructured(SampleStaged());

        _ = _sut.ConsumeStructured();

        Assert.Equal("raw", _sut.ConsumePayload());
    }

    [Fact]
    public void ConsumePayload_DoesNotDrainStructuredSlot()
    {
        var staged = SampleStaged();
        _sut.StagePayload("raw");
        _sut.StageStructured(staged);

        _ = _sut.ConsumePayload();

        Assert.Same(staged, _sut.ConsumeStructured());
    }
}

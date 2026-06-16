using PrayerApp.Services;

namespace PrayerApp.Tests.Services;

public class PrayerSelectionServiceTests
{
    [Fact]
    public void Consume_AfterSet_ReturnsTheSet()
    {
        var sut = new PrayerSelectionService();
        sut.Set(new[] { 3, 1, 2 });

        var result = sut.Consume();

        Assert.Equal(new[] { 3, 1, 2 }, result);
    }

    [Fact]
    public void Consume_ClearsTheSet_SecondConsumeIsEmpty()
    {
        var sut = new PrayerSelectionService();
        sut.Set(new[] { 10, 20 });

        var first = sut.Consume();
        var second = sut.Consume();

        Assert.Equal(new[] { 10, 20 }, first);
        Assert.Empty(second);
    }

    [Fact]
    public void Consume_WithoutSet_ReturnsEmpty()
    {
        var sut = new PrayerSelectionService();

        Assert.Empty(sut.Consume());
    }

    [Fact]
    public void Set_SnapshotsInput_LaterMutationDoesNotLeak()
    {
        var sut = new PrayerSelectionService();
        var source = new List<int> { 1, 2 };
        sut.Set(source);
        source.Add(3); // mutate after Set

        Assert.Equal(new[] { 1, 2 }, sut.Consume());
    }
}

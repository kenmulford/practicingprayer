using Microsoft.Maui.Controls;
using PrayerApp.Behaviors;

namespace PrayerApp.Tests.Behaviors;

/// <summary>
/// xUnit coverage for <see cref="ChipStripHeightBehavior"/> — the iOS-only bound
/// on the tag-filter chip strips (issue #154). The desktop test host
/// (<c>net10.0</c>, no iOS runtime) exercises the platform-scoping contract of the
/// pure <see cref="ChipStripHeightBehavior.ScaledHeight(double)"/>
/// helper. The actual iOS Dynamic-Type scaling
/// (<c>UIFontMetrics.DefaultMetrics.GetScaledValue</c>) is verified on-device at
/// default AND largest font scale — there is no desktop iOS layout harness, the
/// same boundary the sibling iOS behaviors (<see cref="KeyboardScrollPaddingBehavior"/>)
/// document.
/// </summary>
public class ChipStripHeightBehaviorTests
{
    [Fact]
    public void ScaledHeight_OffIos_ReturnsUnsetSentinel()
    {
        // The test host is net10.0 (not iOS). Off iOS the behavior must NOT impose
        // a HeightRequest — Android keeps its content-sizing (unchanged). MAUI's
        // "no explicit height, content-size me" sentinel is -1, so ScaledHeight must
        // return -1 on every non-iOS platform regardless of the base value.
        Assert.Equal(-1.0, ChipStripHeightBehavior.ScaledHeight(28.0));
        Assert.Equal(-1.0, ChipStripHeightBehavior.ScaledHeight(40.0));
    }

    [Fact]
    public void Behavior_AttachAndDetach_DoesNotThrow_OffIos()
    {
        // Off iOS the attach/detach lifecycle is a no-op; it must leave the host
        // CollectionView's HeightRequest at MAUI's default (-1) so Android is
        // unchanged.
        var cv = new CollectionView();
        var sut = new ChipStripHeightBehavior();

        cv.Behaviors.Add(sut);
        Assert.Equal(-1.0, cv.HeightRequest);

        cv.Behaviors.Remove(sut);
        Assert.Equal(-1.0, cv.HeightRequest);
    }
}

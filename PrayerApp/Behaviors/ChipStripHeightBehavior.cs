using CommunityToolkit.Maui.Behaviors;
#if IOS
using System.Runtime.InteropServices;
using Foundation;
using UIKit;
#endif

namespace PrayerApp.Behaviors;

/// <summary>
/// Bounds a horizontal tag-filter chip <see cref="CollectionView"/>'s height on
/// iOS to a single, Dynamic-Type-aware chip row (issue #154). On iOS a
/// height-unconstrained horizontal <see cref="CollectionView"/> measures
/// <i>unbounded</i> in its vertical context (dotnet/maui#9135), so the chip strip
/// fills the page and starves the cards/list below it. This behavior gives the
/// strip an explicit <see cref="VisualElement.HeightRequest"/> sized to one chip
/// row.
/// </summary>
/// <remarks>
/// <para>
/// <b>Font-scale-aware, not a fixed pixel height.</b> The height is
/// <see cref="BaseHeight"/> scaled through
/// <c>UIFontMetrics.DefaultMetrics.GetScaledValue</c> — the OS API documented as
/// returning "a layout height that is scaled from the current Dynamic Type
/// settings". At the normal font size the strip is short; as the OS font size /
/// Dynamic Type grows, the bound grows with it (the row gets taller — it does NOT
/// clip). A naive fixed <c>HeightRequest</c> would re-introduce the large-font
/// clipping that issue #48 deliberately removed, so it is avoided here.
/// </para>
/// <para>
/// <b>iOS-scoped.</b> Off iOS, <see cref="ScaledHeight"/> returns
/// <c>-1</c> (MAUI's "no explicit height — content-size me" sentinel), so Android
/// keeps its existing content-sizing behavior unchanged.
/// </para>
/// <para>
/// The bound is re-applied when the OS content-size category changes, so a live
/// Dynamic Type change while the page is open re-sizes the strip without a
/// relaunch.
/// </para>
/// <para>Citations:
///   https://learn.microsoft.com/dotnet/api/uikit.uifontmetrics.getscaledvalue?view=net-ios-26.4-10.0 ("a layout height that is scaled from the current Dynamic Type settings")
///   https://github.com/dotnet/maui/issues/9135 (horizontal CollectionView takes huge vertical space on iOS)
///   https://learn.microsoft.com/dotnet/communitytoolkit/maui/behaviors/ (BaseBehavior pattern)
/// </para>
/// </remarks>
public class ChipStripHeightBehavior : BaseBehavior<CollectionView>
{
    /// <summary>
    /// The single-row chip-strip height (device-independent units) at the normal
    /// OS font scale. Sized to the chip template's intrinsic row height:
    /// <c>TagFilterChipTemplate</c> Border <c>Padding="10,4"</c> (8 vertical) plus
    /// the <c>FontCaption</c> (12) label line, with a little breathing room. The
    /// iOS bound is this value run through Dynamic-Type scaling; off iOS it is
    /// unused.
    /// </summary>
    private const double BaseHeight = 28.0;

    /// <summary>
    /// Computes the iOS chip-strip <see cref="VisualElement.HeightRequest"/> for a
    /// given base row height. On iOS the value is scaled from the current Dynamic
    /// Type setting via <c>UIFontMetrics</c>; on every other platform it returns
    /// <c>-1</c> — MAUI's sentinel for "no explicit height, size to content" —
    /// leaving non-iOS layout untouched.
    /// </summary>
    public static double ScaledHeight(double baseHeight)
    {
#if IOS
        // GetScaledValue scales the row height from the current Dynamic Type
        // setting; NFloat -> double is implicit, double -> NFloat is explicit.
        return (double)UIFontMetrics.DefaultMetrics.GetScaledValue(new NFloat(baseHeight));
#else
        return -1.0;
#endif
    }

#if IOS
    private NSObject? _contentSizeChangedToken;
#endif

    protected override void OnAttachedTo(CollectionView bindable)
    {
        base.OnAttachedTo(bindable);
#if IOS
        ApplyHeight(bindable);
        _contentSizeChangedToken = UIApplication.Notifications.ObserveContentSizeCategoryChanged(
            (_, _) => ApplyHeight(bindable));
#endif
    }

    protected override void OnDetachingFrom(CollectionView bindable)
    {
#if IOS
        _contentSizeChangedToken?.Dispose();
        _contentSizeChangedToken = null;
        bindable.HeightRequest = -1.0; // restore content-sizing
#endif
        base.OnDetachingFrom(bindable);
    }

#if IOS
    private void ApplyHeight(CollectionView bindable) =>
        bindable.HeightRequest = ScaledHeight(BaseHeight);
#endif
}

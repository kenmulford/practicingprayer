using CommunityToolkit.Maui.Behaviors;
#if IOS
using Foundation;
using UIKit;
#endif

namespace PrayerApp.Behaviors;

/// <summary>
/// Pads a <see cref="ScrollView"/>'s bottom by the keyboard's frame height while
/// the keyboard is shown, then restores the baseline padding on hide. iOS-only
/// — the platform's default <c>KeyboardAutoManagerScroll</c> under-corrects
/// inside <c>UIModalPresentationStyle.PageSheet</c> modals
/// (<see href="https://github.com/dotnet/maui/issues/21726"/>), and our
/// per-input <see cref="KeyboardAvoidanceBehavior"/> alone cannot grow the
/// scrollable region. This behavior fills that gap.
/// </summary>
/// <remarks>
/// <para>On non-iOS platforms this compiles to a no-op.</para>
/// <para>Citations:
///   https://learn.microsoft.com/dotnet/communitytoolkit/maui/behaviors/
///   https://github.com/dotnet/maui/issues/21726
/// </para>
/// </remarks>
public class KeyboardScrollPaddingBehavior : BaseBehavior<ScrollView>
{
#if IOS
    private Thickness _baselinePadding;
    private NSObject? _willShowToken;
    private NSObject? _willHideToken;
#endif

    protected override void OnAttachedTo(ScrollView bindable)
    {
        base.OnAttachedTo(bindable);
#if IOS
        _baselinePadding = bindable.Padding;
        _willShowToken = UIKeyboard.Notifications.ObserveWillShow(OnKeyboardWillShow);
        _willHideToken = UIKeyboard.Notifications.ObserveWillHide(OnKeyboardWillHide);
#endif
    }

    protected override void OnDetachingFrom(ScrollView bindable)
    {
#if IOS
        _willShowToken?.Dispose();
        _willShowToken = null;
        _willHideToken?.Dispose();
        _willHideToken = null;
        bindable.Padding = _baselinePadding;
#endif
        base.OnDetachingFrom(bindable);
    }

#if IOS
    private void OnKeyboardWillShow(object? sender, UIKeyboardEventArgs args)
    {
        if (View is null) return;
        var bottom = _baselinePadding.Bottom + args.FrameEnd.Height;
        View.Padding = new Thickness(
            _baselinePadding.Left,
            _baselinePadding.Top,
            _baselinePadding.Right,
            bottom);
    }

    private void OnKeyboardWillHide(object? sender, UIKeyboardEventArgs args)
    {
        if (View is null) return;
        View.Padding = _baselinePadding;
    }
#endif
}

#if ANDROID
using Android.Content.Res;
using Android.Views;
using Microsoft.Maui.Platform;
#endif

namespace PrayerApp.Behaviors;

/// <summary>
/// Applies a Material ripple foreground to <see cref="Border"/> tap targets on Android.
/// </summary>
public class AndroidRippleBehavior : Behavior<Border>
{
    protected override void OnAttachedTo(Border bindable)
    {
        base.OnAttachedTo(bindable);
        bindable.HandlerChanged += OnHandlerChanged;
    }

    protected override void OnDetachingFrom(Border bindable)
    {
        bindable.HandlerChanged -= OnHandlerChanged;
        base.OnDetachingFrom(bindable);
    }

    static void OnHandlerChanged(object? sender, EventArgs e)
    {
#if ANDROID
        if (sender is not Border border)
        {
            return;
        }

        var platformView = border.ToPlatform();
        if (platformView is not View view)
        {
            return;
        }

        var context = view.Context;
        if (context is null)
        {
            return;
        }

        var typedValue = new TypedValue();
        if (!context.Theme.ResolveAttribute(global::Android.Resource.Attribute.SelectableItemBackground, typedValue, true))
        {
            return;
        }

        var ripple = context.GetDrawable(typedValue.ResourceId);
        if (ripple is null)
        {
            return;
        }

        view.Foreground = ripple;
        view.Clickable = true;
        view.Focusable = true;
#endif
    }
}

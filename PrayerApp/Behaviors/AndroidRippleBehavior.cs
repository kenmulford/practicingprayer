#if ANDROID
using Android.Content.Res;
using Android.OS;
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
        ClearRipple(bindable);
        base.OnDetachingFrom(bindable);
    }

    static void OnHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is not Border border)
        {
            return;
        }

        ApplyRipple(border);
    }

    static void ApplyRipple(Border border)
    {
#if ANDROID
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

        // View.Foreground ripple requires API 23+.
        if ((int)Build.VERSION.SdkInt < (int)BuildVersionCodes.M)
        {
            return;
        }

        var typedValue = new TypedValue();
        if (!context.Theme!.ResolveAttribute(global::Android.Resource.Attribute.SelectableItemBackground, typedValue, true))
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

    static void ClearRipple(Border border)
    {
#if ANDROID
        if ((int)Build.VERSION.SdkInt < (int)BuildVersionCodes.M)
        {
            return;
        }

        var platformView = border.ToPlatform();
        if (platformView is View view)
        {
            view.Foreground = null;
        }
#endif
    }
}

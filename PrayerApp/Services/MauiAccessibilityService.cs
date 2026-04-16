namespace PrayerApp.Services;

/// <summary>
/// Production implementation of IAccessibilityService.
/// Delegates to SemanticScreenReader.Announce on the main thread.
/// Registered as Singleton — stateless wrapper.
/// </summary>
public class MauiAccessibilityService : IAccessibilityService
{
    public void Announce(string message)
    {
        if (MainThread.IsMainThread)
            SemanticScreenReader.Announce(message);
        else
            MainThread.BeginInvokeOnMainThread(() => SemanticScreenReader.Announce(message));
    }

    public void NotifyLayoutChanged()
    {
#if IOS
        MainThread.BeginInvokeOnMainThread(() =>
            UIKit.UIAccessibility.PostNotification(
                UIKit.UIAccessibilityPostNotification.LayoutChanged, null));
#elif ANDROID
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(30)) return;
            var activity = Platform.CurrentActivity;
            if (activity == null) return;
            var manager = (Android.Views.Accessibility.AccessibilityManager?)
                activity.GetSystemService(Android.Content.Context.AccessibilityService);
            if (manager?.IsEnabled != true) return;
            var e = new Android.Views.Accessibility.AccessibilityEvent();
            e.EventType = Android.Views.Accessibility.EventTypes.WindowContentChanged;
            manager.SendAccessibilityEvent(e);
        });
#endif
    }
}

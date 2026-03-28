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
#endif
    }
}

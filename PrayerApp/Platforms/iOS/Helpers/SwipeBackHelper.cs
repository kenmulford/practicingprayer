using UIKit;

namespace PrayerApp.Platforms.iOS.Helpers;

/// <summary>
/// Disables/enables the iOS interactive pop gesture recognizer (swipe-back)
/// on pages with unsaved changes guards. Shell.Navigating does not fire for
/// iOS swipe-back gestures (dotnet/maui#15813, #7351, #6410), so the only
/// way to prevent data loss is to disable the gesture on edit pages and
/// force users to use the back button (which fires Shell.Navigating → Pop).
/// </summary>
public static class SwipeBackHelper
{
    public static void DisableSwipeBack(Page page)
    {
        var navController = page.FindNavigationController();
        if (navController?.InteractivePopGestureRecognizer != null)
            navController.InteractivePopGestureRecognizer.Enabled = false;
    }

    public static void EnableSwipeBack(Page page)
    {
        var navController = page.FindNavigationController();
        if (navController?.InteractivePopGestureRecognizer != null)
            navController.InteractivePopGestureRecognizer.Enabled = true;
    }
}

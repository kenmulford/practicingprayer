using UIKit;

namespace PrayerApp.Platforms.iOS.Helpers;

/// <summary>
/// Walks the UIResponder chain from a MAUI Page's native view to find
/// platform-specific controllers. Used by SwipeBackHelper (finds
/// UINavigationController) and ModalPageSheetHandler (finds UIViewController).
/// </summary>
public static class UIResponderExtensions
{
    public static UINavigationController? FindNavigationController(this Page page)
    {
        var responder = (page.Handler?.PlatformView as UIView)?.NextResponder;
        while (responder != null)
        {
            if (responder is UINavigationController navController)
                return navController;
            if (responder is UIViewController vc && vc.NavigationController != null)
                return vc.NavigationController;
            responder = responder.NextResponder;
        }
        return null;
    }

    public static UIViewController? FindViewController(this Page page)
    {
        var responder = (page.Handler?.PlatformView as UIView)?.NextResponder;
        while (responder != null)
        {
            if (responder is UIViewController vc)
                return vc;
            responder = responder.NextResponder;
        }
        return null;
    }
}

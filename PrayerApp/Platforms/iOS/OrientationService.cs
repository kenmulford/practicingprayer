using PrayerApp.Services;
using UIKit;

namespace PrayerApp.Platforms.iOS;

public class OrientationService : IOrientationService
{
    public static UIInterfaceOrientationMask AllowedOrientations { get; private set; }
        = UIInterfaceOrientationMask.Portrait;

    public void LockLandscape()
    {
        AllowedOrientations = UIInterfaceOrientationMask.LandscapeRight;
        RequestUpdate(UIInterfaceOrientationMask.LandscapeRight);
    }

    public void LockPortrait()
    {
        AllowedOrientations = UIInterfaceOrientationMask.Portrait;
        RequestUpdate(UIInterfaceOrientationMask.Portrait);
    }

    public void Unlock()
    {
        AllowedOrientations = UIInterfaceOrientationMask.AllButUpsideDown;
        RequestUpdate(UIInterfaceOrientationMask.AllButUpsideDown);
    }

    private static void RequestUpdate(UIInterfaceOrientationMask mask)
    {
        var geometryPreferences = new UIWindowSceneGeometryPreferencesIOS(mask);
        foreach (var scene in UIApplication.SharedApplication.ConnectedScenes)
        {
            if (scene is UIWindowScene windowScene)
                windowScene.RequestGeometryUpdate(geometryPreferences, _ => { });
        }
    }
}

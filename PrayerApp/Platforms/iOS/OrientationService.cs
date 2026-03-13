using PrayerApp.Services;
using UIKit;

namespace PrayerApp.Platforms.iOS;

public class OrientationService : IOrientationService
{
    public void LockLandscape()
    {
        UIDevice.CurrentDevice.SetValueForKey(
            new Foundation.NSNumber((int)UIInterfaceOrientation.LandscapeRight),
            new Foundation.NSString("orientation"));
        UINavigationController.AttemptRotationToDeviceOrientation();
    }

    public void Unlock()
    {
        UIDevice.CurrentDevice.SetValueForKey(
            new Foundation.NSNumber((int)UIInterfaceOrientation.Unknown),
            new Foundation.NSString("orientation"));
        UINavigationController.AttemptRotationToDeviceOrientation();
    }
}

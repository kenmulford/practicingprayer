using Android.Content.PM;
using PrayerApp.Services;

namespace PrayerApp.Platforms.Android;

public class OrientationService : IOrientationService
{
    public void LockLandscape()
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity is not null)
            activity.RequestedOrientation = ScreenOrientation.Landscape;
    }

    public void LockPortrait()
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity is not null)
            activity.RequestedOrientation = ScreenOrientation.Portrait;
    }

    public void Unlock()
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity is not null)
            activity.RequestedOrientation = ScreenOrientation.Unspecified;
    }
}

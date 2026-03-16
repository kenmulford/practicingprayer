using Foundation;
using UIKit;

namespace PrayerApp
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        public override UIInterfaceOrientationMask GetSupportedInterfaceOrientations(
            UIApplication application, UIWindow forWindow)
            => Platforms.iOS.OrientationService.AllowedOrientations;
    }
}

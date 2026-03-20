using Foundation;
using UIKit;

namespace PrayerApp
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp()
        {
            // Global exception handlers — capture .NET exception details before
            // they become opaque NSExceptions in crash reports (BUG-28 diagnostics).
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                var msg = e.ExceptionObject is Exception ex
                    ? $"[UnhandledException] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"
                    : $"[UnhandledException] {e.ExceptionObject}";
                Console.Error.WriteLine(msg);
                Preferences.Set("LastCrash", msg);
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                var msg = $"[UnobservedTaskException] {e.Exception.GetType().Name}: {e.Exception.Message}\n{e.Exception.StackTrace}";
                Console.Error.WriteLine(msg);
                Preferences.Set("LastCrash", msg);
            };

            return MauiProgram.CreateMauiApp();
        }

        [Export("application:supportedInterfaceOrientationsForWindow:")]
        public UIInterfaceOrientationMask GetSupportedInterfaceOrientations(
            UIApplication application, UIWindow forWindow)
            => Platforms.iOS.OrientationService.AllowedOrientations;
    }
}

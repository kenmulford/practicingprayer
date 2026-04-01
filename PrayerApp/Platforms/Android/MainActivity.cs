using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace PrayerApp
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
        ScreenOrientation = ScreenOrientation.Portrait,
        WindowSoftInputMode = SoftInput.AdjustResize,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    [IntentFilter(
        new[] { Android.Content.Intent.ActionView },
        Categories = new[] {
            Android.Content.Intent.CategoryDefault,
            Android.Content.Intent.CategoryBrowsable
        },
        DataScheme = "https",
        DataHost = "practicingprayerapp.com",
        DataPathPrefix = "/share",
        AutoVerify = true)]
    public class MainActivity : MauiAppCompatActivity
    {
    }
}

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
    // Deep link handler: https://practicingprayerapp.com/share/*
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
    // File handler: .prayercard files shared via messaging apps
    [IntentFilter(
        new[] { Android.Content.Intent.ActionView },
        Categories = new[] {
            Android.Content.Intent.CategoryDefault,
            Android.Content.Intent.CategoryBrowsable
        },
        DataScheme = "content",
        DataMimeType = "application/x-prayercard")]
    // Selection-toolbar handler: text selected in any app -> "Practicing Prayer" action
    [IntentFilter(
        new[] { Android.Content.Intent.ActionProcessText },
        Categories = new[] { Android.Content.Intent.CategoryDefault },
        DataMimeType = "text/plain")]
    public class MainActivity : MauiAppCompatActivity
    {
    }
}

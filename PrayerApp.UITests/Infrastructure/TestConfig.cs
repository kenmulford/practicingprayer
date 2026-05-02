using System.Runtime.InteropServices;
using OpenQA.Selenium.Appium;

namespace PrayerApp.UITests.Infrastructure;

/// <summary>
/// Platform-aware configuration for Appium test sessions.
/// Auto-detects Windows (Android) vs macOS (iOS) and builds
/// the appropriate AppiumOptions.
/// </summary>
public static class TestConfig
{
    public static readonly bool IsAndroid = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static readonly bool IsIOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public static readonly Uri AppiumServerUri = new(
        Environment.GetEnvironmentVariable("APPIUM_SERVER_URL") ?? "http://127.0.0.1:4723");

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan SessionTimeout = TimeSpan.FromSeconds(60);

    // ── Named delay constants (milliseconds) ─────────────────────
    // Centralised so values can be tuned per-platform in one place.

    /// <summary>UI settle after tap/click.</summary>
    public const int DelayAfterTap = 300;
    /// <summary>Navigation after a save completes.</summary>
    public const int DelayAfterSave = 1000;
    /// <summary>Shell tab / page transition.</summary>
    public const int DelayAfterNavigation = 500;
    /// <summary>Alert / modal dismiss animation.</summary>
    public const int DelayAfterDismiss = 300;
    /// <summary>IsDirty property-change propagation.</summary>
    public const int DelayDirtyRegistration = 500;
    /// <summary>Full app restart in RecreateDriver.</summary>
    public const int DelayAppRelaunch = 5000;
    /// <summary>CollectionView item materialisation.</summary>
    public const int DelayCollectionRender = 1500;
    /// <summary>Modal/action sheet present or dismiss animation.</summary>
    public const int DelayModalAnimation = 1000;

    /// <summary>The Android app package name (from csproj ApplicationId).</summary>
    public const string AndroidPackage = "com.multithreadedllc.prayercards";

    /// <summary>The Android MainActivity class name (MAUI emits a CRC-prefixed Java class).</summary>
    public const string AndroidMainActivity = "crc6425c6d21f3599989c.MainActivity";

    /// <summary>App-data-relative path to the SQLite DB on Android. Used by TestDataSeed.</summary>
    public const string AndroidAppDbRelativePath = "files/prayer_app.db";

    /// <summary>Staging path on Android used when pushing a seed DB (writable by adb).</summary>
    public const string AndroidTmpSeedPath = "/data/local/tmp/prayer_app_seed.db";

    /// <summary>The iOS bundle identifier.</summary>
    public const string IOSBundleId = "com.multithreadedllc.prayercards";

    /// <summary>App-container-relative path to the SQLite DB on iOS. Used by TestDataSeed.</summary>
    /// <remarks>
    /// MAUI's <c>FileSystem.AppDataDirectory</c> maps to <c>Library/</c> on iOS
    /// (not <c>Documents/</c>).
    /// </remarks>
    public const string IOSAppDbRelativePath = "Library/prayer_app.db";

    public static AppiumOptions GetOptions()
    {
        if (IsAndroid)
            return GetAndroidOptions();
        if (IsIOS)
            return GetIOSOptions();

        throw new PlatformNotSupportedException("UITests must run on Windows (Android) or macOS (iOS).");
    }

    private static AppiumOptions GetAndroidOptions()
    {
        var options = new AppiumOptions();
        options.PlatformName = "Android";
        options.AutomationName = "UiAutomator2";

        // Prefer appPackage/appActivity (app must be pre-installed via adb).
        // This avoids Appium's Java-dependent APK signature verification.
        // Fall back to APK path if PRAYER_APK_PATH is explicitly set.
        var apkPath = Environment.GetEnvironmentVariable("PRAYER_APK_PATH");
        if (!string.IsNullOrEmpty(apkPath))
        {
            options.App = apkPath;
        }
        else
        {
            options.AddAdditionalAppiumOption("appPackage", AndroidPackage);
            options.AddAdditionalAppiumOption("appActivity", AndroidMainActivity);
            options.AddAdditionalAppiumOption("noReset", true);
        }

        options.DeviceName = Environment.GetEnvironmentVariable("ANDROID_AVD") ?? "pixel_9_-_api_36_0";
        options.AddAdditionalAppiumOption("appWaitActivity", "crc*");
        options.AddAdditionalAppiumOption("autoGrantPermissions", true);
        options.AddAdditionalAppiumOption("newCommandTimeout", 300);

        return options;
    }

    private static AppiumOptions GetIOSOptions()
    {
        var options = new AppiumOptions();
        options.PlatformName = "iOS";
        options.AutomationName = "XCUITest";
        options.AddAdditionalAppiumOption("bundleId", IOSBundleId);
        // Preserve app data (incl. the DB seeded by TestDataSeed) across driver creation.
        options.AddAdditionalAppiumOption("noReset", true);
        // Do NOT set autoDismissAlerts: it auto-taps Cancel, trapping us in
        // Discard/Cancel dialogs. Tests dismiss alerts via DismissAlertIfPresent.
        // Hardware keyboard hides software keyboard — prevents SendKeys from
        // hitting dictation/emoji buttons. Only works when Appium boots the
        // simulator itself (not pre-booted). Shut down simulator before test run.
        options.AddAdditionalAppiumOption("connectHardwareKeyboard", true);
        options.AddAdditionalAppiumOption("newCommandTimeout", 300);

        // iPad: mobile: hideKeyboard works reliably on tablets (has "Done" button).
        // iPhone keyboard has no dismiss button, causing cascade failures.
        options.DeviceName = Environment.GetEnvironmentVariable("IOS_SIMULATOR") ?? "iPad (A16)";
        options.PlatformVersion = Environment.GetEnvironmentVariable("IOS_VERSION") ?? "26.4";

        return options;
    }
}

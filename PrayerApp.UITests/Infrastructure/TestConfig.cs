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

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan LongTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The Android app package name (from csproj ApplicationId).</summary>
    public const string AndroidPackage = "com.multithreadedllc.prayercards";

    /// <summary>The iOS bundle identifier.</summary>
    public const string IOSBundleId = "com.multithreadedllc.prayercards";

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

        // APK path: env var overrides the default release build location
        var apkPath = Environment.GetEnvironmentVariable("PRAYER_APK_PATH");
        if (string.IsNullOrEmpty(apkPath))
        {
            var solutionDir = FindSolutionDirectory();
            apkPath = Path.Combine(solutionDir, "PrayerApp", "bin", "Release",
                "net10.0-android", $"{AndroidPackage}-Signed.apk");
        }

        options.App = apkPath;
        options.DeviceName = Environment.GetEnvironmentVariable("ANDROID_AVD") ?? "pixel_9_-_api_36_0";
        options.AddAdditionalAppiumOption("appWaitActivity", "crc*");
        options.AddAdditionalAppiumOption("autoGrantPermissions", true);
        options.AddAdditionalAppiumOption("newCommandTimeout", 120);

        return options;
    }

    private static AppiumOptions GetIOSOptions()
    {
        var options = new AppiumOptions();
        options.PlatformName = "iOS";
        options.AutomationName = "XCUITest";
        options.AddAdditionalAppiumOption("bundleId", IOSBundleId);
        options.AddAdditionalAppiumOption("autoAcceptAlerts", true);
        options.AddAdditionalAppiumOption("newCommandTimeout", 120);

        // Simulator name — override via env var
        options.DeviceName = Environment.GetEnvironmentVariable("IOS_SIMULATOR") ?? "iPhone 17";
        options.PlatformVersion = Environment.GetEnvironmentVariable("IOS_VERSION") ?? "26.0";

        return options;
    }

    private static string FindSolutionDirectory()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.sln").Length > 0)
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return Directory.GetCurrentDirectory();
    }
}

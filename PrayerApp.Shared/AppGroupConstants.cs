namespace PrayerApp.Shared;

/// <summary>
/// Single source of truth for App Group identifiers + payload/log file names
/// shared between the main PrayerApp and the iOS Share Extension.
/// Plist files (Entitlements.plist on both sides) carry their own copies of
/// AppGroupId because they are owned by the build, not the runtime.
/// </summary>
public static class AppGroupConstants
{
    public const string AppGroupId = "group.com.multithreadedllc.prayercards";
    public const string PayloadFileName = "pending-import.json";
    public const string LogFileName = "import-log.txt";
    public const int MaxLogLines = 200;

    // Custom URL scheme used by the iOS Share Extension as a host-app wakeup
    // signal. Mirror in PrayerApp/Platforms/iOS/Info.plist (CFBundleURLSchemes)
    // — plist entries can't reference C# constants.
    public const string HostAppScheme = "practicingprayer";
}

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
}

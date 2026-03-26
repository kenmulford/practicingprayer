namespace PrayerApp;

/// <summary>
/// Centralized route name constants. Used by ViewModels for navigation
/// and AppShell for route registration. Avoids nameof() dependency on
/// View types, enabling VM compilation in the test project.
/// </summary>
public static class Routes
{
    public const string PrayerCardPage = "PrayerCardPage";
    public const string PrayerDetailPage = "PrayerDetailPage";
    public const string PrayerTimePage = "PrayerTimePage";
    public const string TagDetailPage = "TagDetailPage";
    public const string AppSettingsPage = "AppSettingsPage";
    public const string BackupPage = "BackupPage";
    public const string AboutPage = "AboutPage";
    public const string HelpPage = "HelpPage";
}

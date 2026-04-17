namespace PrayerApp.Services;

/// <summary>
/// Host-side shim for the MAUI-backed <c>Settings</c> static class. The real
/// <c>PrayerApp.Services.Settings</c> (in the main project) uses MAUI
/// <c>Preferences</c>, which aren't available in this test project. Linking
/// <c>DBService.cs</c> requires a type to resolve for any <c>Settings.X</c>
/// references it contains; this shim provides the minimal surface needed so
/// the seed builder compiles.
///
/// At seed-build time we run in the UITest host process; these values are
/// set but have no effect. The real app on the emulator has its own
/// Preferences store, untouched by this shim.
/// </summary>
internal static class Settings
{
    /// <summary>Written by <c>DBService.EnsureCardBoxMigrationAsync</c>. Unused on host.</summary>
    public static int ArchivedFolderId { get; set; }
}

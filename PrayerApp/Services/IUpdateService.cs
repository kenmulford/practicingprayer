namespace PrayerApp.Services;

/// <summary>
/// Checks whether a newer version of the app is available and initiates
/// the platform update flow. iOS always returns a no-op.
/// </summary>
public interface IUpdateService
{
    Task CheckForUpdateAsync();
}

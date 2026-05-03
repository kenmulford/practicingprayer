namespace PrayerApp.Services;

/// <summary>
/// Resolves the file system path for the iOS App Group container shared
/// between the main app and the iOS Share Extension. Abstracts the
/// NSFileManager call so tests can substitute a temp directory.
/// </summary>
public interface IAppGroupContainerProvider
{
    /// <summary>
    /// Returns the absolute path to the App Group container, or null if
    /// the container could not be resolved (entitlement misconfiguration).
    /// </summary>
    string? ResolveContainerPath();
}

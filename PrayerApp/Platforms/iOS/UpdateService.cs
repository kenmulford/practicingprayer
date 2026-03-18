using PrayerApp.Services;

namespace PrayerApp.Platforms.iOS;

/// <summary>
/// iOS no-op: App Store updates are handled by the OS; no in-app update
/// API equivalent to Play Core exists on iOS without a remote config server.
/// </summary>
public class UpdateService : IUpdateService
{
    public Task CheckForUpdateAsync() => Task.CompletedTask;
}

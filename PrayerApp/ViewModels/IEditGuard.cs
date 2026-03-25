namespace PrayerApp.ViewModels;

/// <summary>
/// Implemented by detail page ViewModels to enable unsaved changes guards.
/// AppShell checks this interface on back navigation and prompts the user
/// if IsDirty is true.
/// </summary>
public interface IEditGuard
{
    bool IsDirty { get; }
    Task<bool> CanLeaveAsync();
}

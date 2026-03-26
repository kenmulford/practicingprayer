namespace PrayerApp.Services;

/// <summary>
/// Abstracts screen reader announcements for testability.
/// ViewModels inject this instead of calling SemanticScreenReader directly.
/// </summary>
public interface IAccessibilityService
{
    void Announce(string message);
}

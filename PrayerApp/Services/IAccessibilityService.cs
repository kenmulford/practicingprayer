namespace PrayerApp.Services;

/// <summary>
/// Abstracts screen reader announcements for testability.
/// ViewModels inject this instead of calling SemanticScreenReader directly.
/// </summary>
public interface IAccessibilityService
{
    void Announce(string message);

    /// <summary>
    /// Notifies the platform that the screen layout has changed, prompting
    /// screen readers and accessibility tools to re-scan the element tree.
    /// On iOS this posts UIAccessibility.LayoutChangedNotification.
    /// </summary>
    void NotifyLayoutChanged();
}

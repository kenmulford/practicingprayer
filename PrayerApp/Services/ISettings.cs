namespace PrayerApp.Services;

/// <summary>
/// Abstraction over app preferences for testability.
/// ViewModels inject this instead of referencing the static Settings class.
/// </summary>
public interface ISettings
{
    bool FirstRun { get; set; }
    int AutoModeIntervalSeconds { get; set; }
    int OverdueDayThreshold { get; set; }
    bool OnboardingComplete { get; set; }
    int DefaultNotifyHour { get; set; }
    int DefaultNotifyMinute { get; set; }
    bool AllowNotifications { get; set; }
    bool QuickAddTipDismissed { get; set; }
    bool PrayerTimeLandscape { get; set; }
    bool CollectionsBannerDismissed { get; set; }

    /// <summary>
    /// The CardBox.Id of the Archived folder. Written once during DB migration.
    /// All "is archived?" checks use card.BoxId == ArchivedFolderId.
    /// </summary>
    int ArchivedFolderId { get; set; }
}

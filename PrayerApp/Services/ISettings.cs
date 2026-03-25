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
}

namespace PrayerApp.Services;

/// <summary>
/// Abstraction over <c>Plugin.LocalNotification.LocalNotificationCenter.Current</c>
/// so that <see cref="NotificationService"/> can be unit-tested without a device.
/// </summary>
public interface ILocalNotificationCenter
{
    Task<bool> RequestNotificationPermission();
    Task<bool> AreNotificationsEnabled();
    void CancelAll();
    Task ShowAsync(int notificationId, string title, string description,
                   DateTime notifyTime, NotifyRepeat repeat, TimeSpan? repeatInterval);
    void Cancel(params int[] notificationIds);
}

/// <summary>
/// Platform-agnostic repeat schedule — maps 1:1 to
/// <c>Plugin.LocalNotification.NotificationRepeat</c> in the wrapper.
/// </summary>
public enum NotifyRepeat
{
    No,
    Daily,
    Weekly,
    TimeInterval
}

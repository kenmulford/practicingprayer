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
    /// <summary>
    /// Clears all pending notifications from both Plugin.LocalNotification and
    /// native iOS UNUserNotificationCenter. Used for startup reconciliation to
    /// eliminate orphaned notifications from deleted prayers or prior app versions.
    /// </summary>
    void ClearAllPending();
    Task ShowAsync(int notificationId, string title, string description,
                   DateTime notifyTime, NotifyRepeat repeat, TimeSpan? repeatInterval);
    Task ScheduleMonthlyAsync(int notificationId, string title, string description,
                               int dayOfMonth, int hour, int minute);
    void Cancel(params int[] notificationIds);
    void CancelMonthly(int notificationId);

    /// <summary>Raised when the user taps a notification. The int argument is the notification ID (= prayer ID).</summary>
    event EventHandler<int>? NotificationTapped;
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

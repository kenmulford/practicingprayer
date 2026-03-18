using Plugin.LocalNotification;

namespace PrayerApp.Services;

/// <summary>
/// Production implementation of <see cref="ILocalNotificationCenter"/>.
/// Delegates to the <c>Plugin.LocalNotification</c> static singleton and translates
/// our <see cref="NotifyRepeat"/> enum to the plugin's <c>NotificationRepeat</c>.
/// </summary>
public class LocalNotificationCenterWrapper : ILocalNotificationCenter
{
    public Task<bool> RequestNotificationPermission() =>
        LocalNotificationCenter.Current.RequestNotificationPermission();

    public Task<bool> AreNotificationsEnabled() =>
        LocalNotificationCenter.Current.AreNotificationsEnabled();

    public void CancelAll() =>
        LocalNotificationCenter.Current.CancelAll();

    public void Cancel(params int[] notificationIds) =>
        LocalNotificationCenter.Current.Cancel(notificationIds);

    public Task ShowAsync(int notificationId, string title, string description,
                          DateTime notifyTime, NotifyRepeat repeat, TimeSpan? repeatInterval)
    {
        var request = new NotificationRequest
        {
            NotificationId = notificationId,
            Title = title,
            Description = description,
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = notifyTime,
                RepeatType = repeat switch
                {
                    NotifyRepeat.Daily         => NotificationRepeat.Daily,
                    NotifyRepeat.Weekly        => NotificationRepeat.Weekly,
                    NotifyRepeat.TimeInterval  => NotificationRepeat.TimeInterval,
                    _                          => NotificationRepeat.No
                },
                NotifyRepeatInterval = repeatInterval
            }
        };

        return LocalNotificationCenter.Current.Show(request);
    }
}

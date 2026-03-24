using Plugin.LocalNotification;

namespace PrayerApp.Services;

/// <summary>
/// Production implementation of <see cref="ILocalNotificationCenter"/>.
/// Delegates to the <c>Plugin.LocalNotification</c> static singleton and translates
/// our <see cref="NotifyRepeat"/> enum to the plugin's <c>NotificationRepeat</c>.
/// </summary>
public class LocalNotificationCenterWrapper : ILocalNotificationCenter
{
    public event EventHandler<int>? NotificationTapped;

    public LocalNotificationCenterWrapper()
    {
        LocalNotificationCenter.Current.NotificationActionTapped += OnNotificationActionTapped;
    }

    private void OnNotificationActionTapped(Plugin.LocalNotification.EventArgs.NotificationActionEventArgs e)
    {
        var notificationId = e.Request?.NotificationId ?? 0;
        if (notificationId > 0)
            NotificationTapped?.Invoke(this, notificationId);
    }

    public Task<bool> RequestNotificationPermission() =>
        LocalNotificationCenter.Current.RequestNotificationPermission();

    public Task<bool> AreNotificationsEnabled() =>
        LocalNotificationCenter.Current.AreNotificationsEnabled();

    public void CancelAll() =>
        LocalNotificationCenter.Current.CancelAll();

    public void Cancel(params int[] notificationIds) =>
        LocalNotificationCenter.Current.Cancel(notificationIds);

    public void CancelMonthly(int notificationId)
    {
        Cancel(Enumerable.Range(0, 12).Select(i => notificationId * 100 + i).ToArray());
    }

    public async Task ScheduleMonthlyAsync(int notificationId, string title, string description,
                                            int dayOfMonth, int hour, int minute)
    {
        // Schedule the next 12 monthly occurrences as individual one-shot notifications
        // via the Plugin API. This keeps all notifications in the same system (Plugin)
        // rather than mixing Plugin + direct UNUserNotificationCenter calls on iOS.
        var now = DateTime.Now;
        for (int i = 0; i < 12; i++)
        {
            var baseDate = new DateTime(now.Year, now.Month, 1).AddMonths(i);
            var day = Math.Min(dayOfMonth, DateTime.DaysInMonth(baseDate.Year, baseDate.Month));
            var target = new DateTime(baseDate.Year, baseDate.Month, day, hour, minute, 0);
            if (target <= now) continue;

            await ShowAsync(notificationId * 100 + i, title, description,
                            target, NotifyRepeat.No, null);
        }
    }

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

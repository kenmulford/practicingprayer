using System.Threading.Tasks;
using Plugin.LocalNotification;
using PrayerApp.Models;

namespace PrayerApp.Services;

public class NotificationService : INotificationService
{
    public Task<bool> RequestPermissionAsync()
    {
        return LocalNotificationCenter.Current.RequestNotificationPermission();
    }

    public Task<bool> AreNotificationsEnabledAsync()
    {
        return LocalNotificationCenter.Current.AreNotificationsEnabled();
    }

    public Task ClearAllAsync()
    {
        LocalNotificationCenter.Current.CancelAll();
        return Task.CompletedTask;
    }

    public async Task ScheduleAsync(Prayer prayer)
    {
        if (!Settings.AllowNotifications) return;

        // Schedule at 9 AM; if today's 9 AM has passed, use tomorrow's.
        var notifyTime = DateTime.Now.Date.AddHours(9);
        if (notifyTime <= DateTime.Now)
            notifyTime = notifyTime.AddDays(1);

        NotificationRepeat repeatType;
        TimeSpan? repeatInterval = null;

        switch (prayer.PrayerFrequency)
        {
            case PrayerFrequency.Daily:
                repeatType = NotificationRepeat.Daily;
                break;
            case PrayerFrequency.Weekly:
                repeatType = NotificationRepeat.Weekly;
                break;
            case PrayerFrequency.Monthly:
                repeatType = NotificationRepeat.TimeInterval;
                repeatInterval = TimeSpan.FromDays(30);
                break;
            case PrayerFrequency.Yearly:
                repeatType = NotificationRepeat.TimeInterval;
                repeatInterval = TimeSpan.FromDays(365);
                break;
            default: // OneTime
                repeatType = NotificationRepeat.No;
                break;
        }

        var request = new NotificationRequest
        {
            NotificationId = prayer.Id,
            Title = "Practicing Prayer",
            Description = prayer.Title,
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = notifyTime,
                RepeatType = repeatType,
                NotifyRepeatInterval = repeatInterval
            }
        };

        await LocalNotificationCenter.Current.Show(request);
    }

    public Task CancelAsync(int notificationId)
    {
        LocalNotificationCenter.Current.Cancel(notificationId);
        return Task.CompletedTask;
    }
}

using PrayerApp.Helpers;
using PrayerApp.Models;

namespace PrayerApp.Services;

public class NotificationService : INotificationService
{
    private readonly ILocalNotificationCenter _center;
    private readonly Func<bool> _isNotificationsAllowed;

    public NotificationService(ILocalNotificationCenter center, Func<bool> isNotificationsAllowed)
    {
        _center = center;
        _isNotificationsAllowed = isNotificationsAllowed;
    }

    public Task<bool> RequestPermissionAsync() =>
        _center.RequestNotificationPermission();

    public Task<bool> AreNotificationsEnabledAsync() =>
        _center.AreNotificationsEnabled();

    public Task ClearAllAsync()
    {
        _center.CancelAll();
        return Task.CompletedTask;
    }

    public Task CancelAsync(int notificationId)
    {
        _center.Cancel(notificationId);
        return Task.CompletedTask;
    }

    public async Task ScheduleAsync(Prayer prayer)
    {
        if (!_isNotificationsAllowed()) return;

        // Schedule at 9 AM; if today's 9 AM has passed, use tomorrow's.
        var notifyTime = DateTime.Now.Date.AddHours(NotificationHelper.NotificationHour);
        if (notifyTime <= DateTime.Now)
            notifyTime = notifyTime.AddDays(1);

        NotifyRepeat repeatType;
        TimeSpan? repeatInterval = null;

        switch (prayer.PrayerFrequency)
        {
            case PrayerFrequency.Daily:
                repeatType = NotifyRepeat.Daily;
                break;
            case PrayerFrequency.Weekly:
                repeatType = NotifyRepeat.Weekly;
                break;
            case PrayerFrequency.Monthly:
                repeatType = NotifyRepeat.TimeInterval;
                repeatInterval = TimeSpan.FromDays(30);
                break;
            case PrayerFrequency.Yearly:
                repeatType = NotifyRepeat.TimeInterval;
                repeatInterval = TimeSpan.FromDays(365);
                break;
            default: // OneTime
                repeatType = NotifyRepeat.No;
                break;
        }

        await _center.ShowAsync(
            prayer.Id,
            "Practicing Prayer",
            prayer.Title,
            notifyTime,
            repeatType,
            repeatInterval);
    }
}

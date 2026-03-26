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

    public Task CancelAsync(int notificationId, PrayerFrequency frequency = PrayerFrequency.OneTime)
    {
        if (frequency == PrayerFrequency.Monthly)
            _center.CancelMonthly(notificationId);
        else
            _center.Cancel(notificationId);
        return Task.CompletedTask;
    }

    public async Task ScheduleAsync(Prayer prayer)
    {
        try
        {
        if (!_isNotificationsAllowed())
        {
            System.Diagnostics.Debug.WriteLine($"[Notify] ScheduleAsync SKIPPED — notifications not allowed");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[Notify] ScheduleAsync: id={prayer.Id}, freq={prayer.PrayerFrequency}, " +
            $"hour={prayer.NotifyHour}, min={prayer.NotifyMinute}, " +
            $"dayOfWeek={prayer.NotifyDayOfWeek}, dayOfMonth={prayer.NotifyDayOfMonth}, " +
            $"canNotify={prayer.CanNotify}");

        // Cancel any existing notification for this prayer before rescheduling.
        // Cancel both monthly and non-monthly variants since we don't know the
        // previous frequency — the prayer may have changed from Daily to Monthly.
        _center.Cancel(prayer.Id);
        _center.CancelMonthly(prayer.Id);
        System.Diagnostics.Debug.WriteLine($"[Notify] Cancelled existing notifications for id={prayer.Id}");

        var hour = prayer.NotifyHour;
        var minute = prayer.NotifyMinute;

        // Monthly uses native calendar-based scheduling
        if (prayer.PrayerFrequency == PrayerFrequency.Monthly)
        {
            var dayOfMonth = prayer.NotifyDayOfMonth > 0
                ? prayer.NotifyDayOfMonth
                : prayer.CreatedAt.Day;
            System.Diagnostics.Debug.WriteLine($"[Notify] Monthly: dayOfMonth={dayOfMonth} (raw={prayer.NotifyDayOfMonth}, createdAt={prayer.CreatedAt.Day})");
            await _center.ScheduleMonthlyAsync(
                prayer.Id, "Practicing Prayer", prayer.Title,
                dayOfMonth, hour, minute);
            return;
        }

        // Daily/Weekly/OneTime/Yearly — use existing plugin path
        DateTime notifyTime;
        if (prayer.PrayerFrequency == PrayerFrequency.Weekly && prayer.NotifyDayOfWeek >= 0)
        {
            notifyTime = GetNextDayOfWeek((DayOfWeek)prayer.NotifyDayOfWeek, hour, minute);
        }
        else
        {
            notifyTime = DateTime.Now.Date.AddHours(hour).AddMinutes(minute);
            if (notifyTime <= DateTime.Now)
                notifyTime = notifyTime.AddDays(1);
        }

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
            case PrayerFrequency.Yearly:
            {
                // Schedule one-shot at next anniversary to avoid leap-year drift.
                // Compute from today's date at the specified time, then find next year.
                var candidate = DateTime.Now.Date.AddHours(hour).AddMinutes(minute);
                if (candidate <= DateTime.Now)
                    candidate = candidate.AddYears(1);
                notifyTime = candidate;
                repeatType = NotifyRepeat.No;
                break;
            }
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
        catch (Exception ex)
        {
            // Don't let notification failures block prayer saves
            System.Diagnostics.Debug.WriteLine($"[Notify] ScheduleAsync FAILED: {ex.Message}");
        }
    }

    internal static DateTime GetNextDayOfWeek(DayOfWeek targetDay, int hour, int minute)
    {
        var now = DateTime.Now;
        var today = now.Date.AddHours(hour).AddMinutes(minute);
        var daysUntil = ((int)targetDay - (int)now.DayOfWeek + 7) % 7;
        if (daysUntil == 0 && today <= now)
            daysUntil = 7;
        return today.AddDays(daysUntil);
    }
}

using PrayerApp.Models;

namespace PrayerApp.Helpers;

public static class NotificationHelper
{
    public const int NotificationHour = 9;
    private static readonly TimeSpan _recentWindow = TimeSpan.FromHours(24);

    /// <summary>
    /// Returns IDs of prayers that should have received a notification within the
    /// last 24 hours, based on their schedule (CreatedAt + PrayerFrequency).
    /// </summary>
    public static IReadOnlyList<int> GetRecentlyNotifiedPrayerIds(
        IEnumerable<Prayer> prayers, DateTime now)
    {
        var result = new List<int>();

        foreach (var prayer in prayers)
        {
            if (!prayer.CanNotify || prayer.IsAnswered)
                continue;

            var firstFire = GetFirstFireTime(prayer.CreatedAt, prayer.NotifyHour, prayer.NotifyMinute);
            if (firstFire > now)
                continue;

            var mostRecent = GetMostRecentFireTime(firstFire, prayer.PrayerFrequency, now,
                                                    prayer.NotifyHour, prayer.NotifyMinute);
            if (mostRecent.HasValue && (now - mostRecent.Value) <= _recentWindow)
                result.Add(prayer.Id);
        }

        return result;
    }

    /// <summary>
    /// The first notification fires at the prayer's notify time on the day after creation.
    /// If created before that time, it fires on the same day.
    /// </summary>
    internal static DateTime GetFirstFireTime(DateTime createdAt, int hour = NotificationHour, int minute = 0)
    {
        var sameDayTime = createdAt.Date.AddHours(hour).AddMinutes(minute);
        return createdAt < sameDayTime ? sameDayTime : sameDayTime.AddDays(1);
    }

    /// <summary>
    /// Computes the most recent notification fire time at or before <paramref name="now"/>.
    /// </summary>
    internal static DateTime? GetMostRecentFireTime(
        DateTime firstFire, PrayerFrequency frequency, DateTime now,
        int hour = NotificationHour, int minute = 0)
    {
        if (firstFire > now)
            return null;

        switch (frequency)
        {
            case PrayerFrequency.Daily:
            {
                var todayTime = now.Date.AddHours(hour).AddMinutes(minute);
                return now >= todayTime ? todayTime : todayTime.AddDays(-1);
            }

            case PrayerFrequency.Weekly:
                return MostRecentByInterval(firstFire, TimeSpan.FromDays(7), now);

            case PrayerFrequency.Monthly:
                return MostRecentMonthly(firstFire, now, hour, minute);

            case PrayerFrequency.Yearly:
                return MostRecentByInterval(firstFire, TimeSpan.FromDays(365), now);

            case PrayerFrequency.OneTime:
                return firstFire;

            default:
                return null;
        }
    }

    private static DateTime MostRecentByInterval(DateTime firstFire, TimeSpan interval, DateTime now)
    {
        var elapsed = now - firstFire;
        var periods = (int)(elapsed.TotalSeconds / interval.TotalSeconds);
        var candidate = firstFire + TimeSpan.FromSeconds(periods * interval.TotalSeconds);

        // Ensure we don't overshoot
        if (candidate > now)
            candidate -= interval;

        return candidate;
    }

    /// <summary>
    /// For monthly notifications, find the most recent fire on the same day-of-month.
    /// </summary>
    private static DateTime? MostRecentMonthly(DateTime firstFire, DateTime now, int hour, int minute)
    {
        var dayOfMonth = firstFire.Day;
        // Check this month and last month
        for (int offset = 0; offset >= -1; offset--)
        {
            var candidate = now.AddMonths(offset);
            var day = Math.Min(dayOfMonth, DateTime.DaysInMonth(candidate.Year, candidate.Month));
            var fireTime = new DateTime(candidate.Year, candidate.Month, day, hour, minute, 0);
            if (fireTime >= firstFire && fireTime <= now)
                return fireTime;
        }
        return null;
    }
}

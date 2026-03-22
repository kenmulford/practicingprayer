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

            var firstFire = GetFirstFireTime(prayer.CreatedAt);
            if (firstFire > now)
                continue;

            var mostRecent = GetMostRecentFireTime(firstFire, prayer.PrayerFrequency, now);
            if (mostRecent.HasValue && (now - mostRecent.Value) <= _recentWindow)
                result.Add(prayer.Id);
        }

        return result;
    }

    /// <summary>
    /// The first notification fires at 9 AM on the day after creation.
    /// If created before 9 AM, it fires at 9 AM the same day.
    /// </summary>
    internal static DateTime GetFirstFireTime(DateTime createdAt)
    {
        var sameDay9AM = createdAt.Date.AddHours(NotificationHour);
        return createdAt < sameDay9AM ? sameDay9AM : sameDay9AM.AddDays(1);
    }

    /// <summary>
    /// Computes the most recent notification fire time at or before <paramref name="now"/>.
    /// </summary>
    internal static DateTime? GetMostRecentFireTime(
        DateTime firstFire, PrayerFrequency frequency, DateTime now)
    {
        if (firstFire > now)
            return null;

        switch (frequency)
        {
            case PrayerFrequency.Daily:
            {
                var today9AM = now.Date.AddHours(NotificationHour);
                return now >= today9AM ? today9AM : today9AM.AddDays(-1);
            }

            case PrayerFrequency.Weekly:
                return MostRecentByInterval(firstFire, TimeSpan.FromDays(7), now);

            case PrayerFrequency.Monthly:
                return MostRecentByInterval(firstFire, TimeSpan.FromDays(30), now);

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
}

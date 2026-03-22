using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;

namespace PrayerApp.Tests.Services;

public class NotificationServiceTests
{
    private readonly ILocalNotificationCenter _center;
    private readonly NotificationService _service;
    private readonly NotificationService _serviceDisabled;

    public NotificationServiceTests()
    {
        _center = Substitute.For<ILocalNotificationCenter>();
        _service         = new NotificationService(_center, () => true);
        _serviceDisabled = new NotificationService(_center, () => false);
    }

    // ── Delegation — simple pass-through methods ───────────────────────────────

    [Fact]
    public async Task RequestPermissionAsync_DelegatesToCenter()
    {
        _center.RequestNotificationPermission().Returns(true);

        var result = await _service.RequestPermissionAsync();

        Assert.True(result);
        await _center.Received(1).RequestNotificationPermission();
    }

    [Fact]
    public async Task AreNotificationsEnabledAsync_DelegatesToCenter()
    {
        _center.AreNotificationsEnabled().Returns(false);

        var result = await _service.AreNotificationsEnabledAsync();

        Assert.False(result);
        await _center.Received(1).AreNotificationsEnabled();
    }

    [Fact]
    public async Task ClearAllAsync_CallsCancelAll()
    {
        await _service.ClearAllAsync();

        _center.Received(1).CancelAll();
    }

    [Fact]
    public async Task CancelAsync_CallsCancelWithCorrectId()
    {
        await _service.CancelAsync(42);

        _center.Received(1).Cancel(42);
    }

    // ── ScheduleAsync — notifications disabled ────────────────────────────────

    [Fact]
    public async Task ScheduleAsync_WhenNotificationsDisabled_DoesNotCallShow()
    {
        var prayer = new Prayer { Id = 1, Title = "Test", PrayerFrequency = PrayerFrequency.Daily };

        await _serviceDisabled.ScheduleAsync(prayer);

        await _center.DidNotReceive().ShowAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<DateTime>(), Arg.Any<NotifyRepeat>(), Arg.Any<TimeSpan?>());
    }

    // ── ScheduleAsync — repeat type mapping ───────────────────────────────────

    [Fact]
    public async Task ScheduleAsync_DailyFrequency_SchedulesWithDailyRepeat()
    {
        var prayer = new Prayer { Id = 1, Title = "Morning", PrayerFrequency = PrayerFrequency.Daily };

        await _service.ScheduleAsync(prayer);

        await _center.Received(1).ShowAsync(
            1, "Practicing Prayer", "Morning",
            Arg.Any<DateTime>(), NotifyRepeat.Daily, null);
    }

    [Fact]
    public async Task ScheduleAsync_WeeklyFrequency_SchedulesWithWeeklyRepeat()
    {
        var prayer = new Prayer { Id = 2, Title = "Weekly Check", PrayerFrequency = PrayerFrequency.Weekly };

        await _service.ScheduleAsync(prayer);

        await _center.Received(1).ShowAsync(
            2, "Practicing Prayer", "Weekly Check",
            Arg.Any<DateTime>(), NotifyRepeat.Weekly, null);
    }

    [Fact]
    public async Task ScheduleAsync_MonthlyFrequency_SchedulesWithTimeInterval30Days()
    {
        var prayer = new Prayer { Id = 3, Title = "Monthly", PrayerFrequency = PrayerFrequency.Monthly };

        await _service.ScheduleAsync(prayer);

        await _center.Received(1).ShowAsync(
            3, "Practicing Prayer", "Monthly",
            Arg.Any<DateTime>(), NotifyRepeat.TimeInterval, TimeSpan.FromDays(30));
    }

    [Fact]
    public async Task ScheduleAsync_YearlyFrequency_SchedulesWithTimeInterval365Days()
    {
        var prayer = new Prayer { Id = 4, Title = "Yearly", PrayerFrequency = PrayerFrequency.Yearly };

        await _service.ScheduleAsync(prayer);

        await _center.Received(1).ShowAsync(
            4, "Practicing Prayer", "Yearly",
            Arg.Any<DateTime>(), NotifyRepeat.TimeInterval, TimeSpan.FromDays(365));
    }

    [Fact]
    public async Task ScheduleAsync_OneTimeFrequency_SchedulesWithNoRepeat()
    {
        var prayer = new Prayer { Id = 5, Title = "One Time", PrayerFrequency = PrayerFrequency.OneTime };

        await _service.ScheduleAsync(prayer);

        await _center.Received(1).ShowAsync(
            5, "Practicing Prayer", "One Time",
            Arg.Any<DateTime>(), NotifyRepeat.No, null);
    }

    // ── ScheduleAsync — notification time ────────────────────────────────────

    [Fact]
    public async Task ScheduleAsync_SchedulesAt9AM_TodayOrTomorrow()
    {
        var prayer = new Prayer { Id = 6, Title = "Time Test", PrayerFrequency = PrayerFrequency.Daily };

        await _service.ScheduleAsync(prayer);

        await _center.Received(1).ShowAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Is<DateTime>(d => d.Hour == 9 && d.Minute == 0 && d > DateTime.Now),
            Arg.Any<NotifyRepeat>(), Arg.Any<TimeSpan?>());
    }
}

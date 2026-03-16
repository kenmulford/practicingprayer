using System.Threading.Tasks;
using PrayerApp.Models;

public interface INotificationService
{
    Task<bool> RequestPermissionAsync();
    Task<bool> AreNotificationsEnabledAsync();
    Task ClearAllAsync();
    Task ScheduleAsync(Prayer prayer);
    Task CancelAsync(int notificationId);
}

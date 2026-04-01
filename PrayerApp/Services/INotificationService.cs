using System.Threading.Tasks;
using PrayerApp.Models;

public interface INotificationService
{
    Task<bool> RequestPermissionAsync();
    Task<bool> AreNotificationsEnabledAsync();
    Task ClearAllAsync();
    Task ScheduleAsync(Prayer prayer);
    Task CancelAsync(int notificationId, PrayerFrequency frequency = PrayerFrequency.OneTime);
    Task RenewMonthlyNotificationsAsync(IEnumerable<Prayer> activePrayers);
    /// <summary>
    /// Clears all pending notifications (plugin + native iOS) then reschedules
    /// only prayers with CanNotify=true. Eliminates orphaned notifications from
    /// deleted prayers, prior app versions, or database restores.
    /// </summary>
    Task ReconcileNotificationsAsync(IEnumerable<Prayer> activePrayers);
}

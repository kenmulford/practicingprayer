using System.Threading.Tasks;
using Plugin.LocalNotification;

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
}

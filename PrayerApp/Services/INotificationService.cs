using System.Threading.Tasks;

public interface INotificationService
{
    Task<bool> RequestPermissionAsync();
    Task<bool> AreNotificationsEnabledAsync();
    Task ClearAllAsync();
}

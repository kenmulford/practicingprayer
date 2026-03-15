namespace PrayerApp.Services;

public interface IBackupService
{
    Task<bool> ExportAsync();
    Task<bool> ImportAsync();
}

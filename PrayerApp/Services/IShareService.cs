namespace PrayerApp.Services;

public interface IShareService
{
    Task ShareTextAsync(string title, string text);
    Task ShareFileAsync(string title, string filePath, string mimeType);
}

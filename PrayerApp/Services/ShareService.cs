namespace PrayerApp.Services;

public class ShareService : IShareService
{
    public Task ShareTextAsync(string title, string text)
        => Share.RequestAsync(new ShareTextRequest { Title = title, Text = text });

    public Task ShareFileAsync(string title, string filePath, string mimeType)
        => Share.RequestAsync(new ShareFileRequest
        {
            Title = title,
            File = new ShareFile(filePath, mimeType)
        });
}

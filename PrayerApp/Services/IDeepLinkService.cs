using PrayerApp.Models;

namespace PrayerApp.Services;

public interface IDeepLinkService
{
    Task ShareRequestAsync(Prayer prayer);
    Task ShareCardAsync(PrayerCard card, IEnumerable<Prayer> prayers);
    Task HandleAsync(string uri);
    Task HandleFileAsync(Stream fileStream);
}

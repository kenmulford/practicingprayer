using PrayerApp.Models;

namespace PrayerApp.Services;

public interface IDeepLinkService
{
    string BuildRequestShareText(Prayer prayer);
    string BuildCardShareText(PrayerCard card, IEnumerable<Prayer> prayers);
    Task HandleAsync(string uri);
}

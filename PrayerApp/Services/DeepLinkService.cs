using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PrayerApp.Models;

namespace PrayerApp.Services;

public class DeepLinkService : IDeepLinkService
{
    private const string BaseUrl = "https://practicingprayerapp.com/share";
    private const string Footer = "(Shared via Practicing Prayer)";

    private readonly ICardService _cardService;
    private readonly INavigationService _nav;

    public DeepLinkService(ICardService cardService, INavigationService nav)
    {
        _cardService = cardService;
        _nav = nav;
    }

    public string BuildRequestShareText(Prayer prayer)
    {
        var uri = $"{BaseUrl}/r?title={Uri.EscapeDataString(prayer.Title)}";
        if (!string.IsNullOrWhiteSpace(prayer.Details))
            uri += $"&notes={Uri.EscapeDataString(prayer.Details)}";

        var fallback = string.IsNullOrWhiteSpace(prayer.Details)
            ? prayer.Title
            : $"{prayer.Title}\n{prayer.Details}";

        return $"{uri}\n\n{fallback}\n\n{Footer}";
    }

    public string BuildCardShareText(PrayerCard card, IEnumerable<Prayer> prayers)
    {
        var requestList = prayers.Select(p => new { title = p.Title, notes = p.Details ?? "" }).ToArray();
        var json = JsonSerializer.Serialize(requestList);
        var base64 = ToBase64Url(Encoding.UTF8.GetBytes(json));

        var uri = $"{BaseUrl}/c?title={Uri.EscapeDataString(card.Title)}&requests={base64}";

        var sb = new StringBuilder();
        sb.AppendLine(uri);
        sb.AppendLine();
        sb.AppendLine(card.Title);
        foreach (var p in prayers)
            sb.AppendLine($"- {p.Title}");
        sb.AppendLine();
        sb.Append(Footer);

        return sb.ToString();
    }

    public async Task HandleAsync(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            return;

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
            return;

        if (parsed.Host != "practicingprayerapp.com")
            return;

        var path = parsed.AbsolutePath.TrimEnd('/');
        var query = System.Web.HttpUtility.ParseQueryString(parsed.Query);

        if (path == "/share/r")
            await HandleRequestAsync(query);
        else if (path == "/share/c")
            await HandleCardAsync(query);
    }

    private async Task HandleRequestAsync(System.Collections.Specialized.NameValueCollection query)
    {
        var title = query["title"];
        if (string.IsNullOrWhiteSpace(title))
            return;

        var sharedCard = await _cardService.GetOrCreateSharedCardAsync();

        var prayer = new Prayer
        {
            PrayerCardId = sharedCard.Id,
            Title = title,
            Details = query["notes"],
            IsImported = true,
            CanNotify = false
        };
        await prayer.SaveAsync();

        await _nav.GoToAsync(Routes.PrayerCardsTab);
    }

    private async Task HandleCardAsync(System.Collections.Specialized.NameValueCollection query)
    {
        var title = query["title"];
        if (string.IsNullOrWhiteSpace(title))
            return;

        var requestsBase64 = query["requests"];
        if (string.IsNullOrWhiteSpace(requestsBase64))
            return;

        SharedRequest[]? requests;
        try
        {
            var json = Encoding.UTF8.GetString(FromBase64Url(requestsBase64));
            requests = JsonSerializer.Deserialize<SharedRequest[]>(json);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            System.Diagnostics.Debug.WriteLine($"[DeepLink] Invalid shared card data: {ex.Message}");
            return;
        }

        if (requests is null || requests.Length == 0)
            return;

        var card = new PrayerCard
        {
            Title = title,
            IsImported = true
        };
        await card.SaveAsync();
        _cardService.InvalidateCache();

        foreach (var req in requests)
        {
            var prayer = new Prayer
            {
                PrayerCardId = card.Id,
                Title = req.Title ?? "",
                Details = string.IsNullOrEmpty(req.Notes) ? null : req.Notes,
                IsImported = true,
                CanNotify = false
            };
            await prayer.SaveAsync();
        }

        await _nav.GoToAsync(Routes.PrayerCardsTab);
    }

    private static string ToBase64Url(byte[] data)
        => Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static byte[] FromBase64Url(string base64Url)
    {
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }

    private record SharedRequest(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("notes")] string? Notes);
}

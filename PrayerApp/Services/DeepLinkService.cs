using System.IO.Compression;
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
    private readonly IPrayerService _prayerService;
    private readonly INavigationService _nav;

    public DeepLinkService(ICardService cardService, IPrayerService prayerService, INavigationService nav)
    {
        _cardService = cardService;
        _prayerService = prayerService;
        _nav = nav;
    }

    public string BuildRequestShareText(Prayer prayer)
    {
        var title = NormalizeQuotes(prayer.Title);
        var details = prayer.Details is not null ? NormalizeQuotes(prayer.Details) : null;

        var json = JsonSerializer.Serialize(new { title, notes = details ?? "" });
        var compressed = GzipCompress(Encoding.UTF8.GetBytes(json));
        var base64 = "z." + ToBase64Url(compressed);
        var uri = $"{BaseUrl}/r?v=2&d={base64}";

        var fallback = string.IsNullOrWhiteSpace(details)
            ? title
            : $"{title}\n{details}";

        return $"{uri}\n\n{fallback}\n\n{Footer}";
    }

    public string BuildCardShareText(PrayerCard card, IEnumerable<Prayer> prayers)
    {
        var title = NormalizeQuotes(card.Title);
        var requestList = prayers.Select(p => new { title = NormalizeQuotes(p.Title), notes = p.Details is not null ? NormalizeQuotes(p.Details) : "" }).ToArray();
        var json = JsonSerializer.Serialize(new { title, requests = requestList });
        var compressed = GzipCompress(Encoding.UTF8.GetBytes(json));
        var base64 = "z." + ToBase64Url(compressed);

        var uri = $"{BaseUrl}/c?v=2&d={base64}";

        var prayerCount = requestList.Length;
        var sb = new StringBuilder();
        sb.AppendLine(uri);
        sb.AppendLine();
        sb.AppendLine(title);
        sb.AppendLine($"+ {prayerCount} request{(prayerCount == 1 ? "" : "s")}");
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
        string? title, detail;

        // Future-proof: if a newer format version arrives, tell user to update
        var version = query["v"];
        if (version != null && int.TryParse(version, out var v) && v > 2)
        {
            await _nav.DisplayAlertAsync("Update Required",
                "This prayer was shared from a newer version of Practicing Prayer. Please update the app to import it.", "OK");
            return;
        }

        // New format: compact Base64 payload in ?d= parameter
        var payload = query["d"];
        if (!string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                byte[] jsonBytes;
                if (payload.StartsWith("z."))
                    jsonBytes = GzipDecompress(FromBase64Url(payload[2..]));
                else
                    jsonBytes = FromBase64Url(payload);

                var json = Encoding.UTF8.GetString(jsonBytes);
                var data = JsonSerializer.Deserialize<SharedRequest>(json);
                title = NormalizeQuotes(data?.Title);
                detail = string.IsNullOrEmpty(data?.Notes) ? null : NormalizeQuotes(data.Notes);
            }
            catch (Exception ex) when (ex is FormatException or JsonException or InvalidDataException)
            {
                System.Diagnostics.Debug.WriteLine($"[DeepLink] Invalid shared request data: {ex.Message}");
                await _nav.DisplayAlertAsync("Unable to Import", "The shared prayer link appears to be invalid or incomplete.", "OK");
                return;
            }
        }
        else
        {
            // Legacy format: ?title=&notes= query params (backward compatibility)
            title = query["title"];
            detail = query["notes"];
        }

        if (string.IsNullOrWhiteSpace(title))
            return;

        var preview = string.IsNullOrWhiteSpace(detail) ? title : $"{title}\n\n{detail}";
        var confirmed = await _nav.DisplayConfirmAsync(
            "Prayer Shared With You",
            $"{preview}\n\nSave to your prayer journal?",
            "Save", "Decline");
        if (!confirmed) return;

        var sharedCard = await _cardService.GetOrCreateSharedCardAsync();

        var prayer = new Prayer
        {
            PrayerCardId = sharedCard.Id,
            Title = title,
            Details = detail,
            IsImported = true,
            CanNotify = false
        };
        await prayer.SaveAsync();

        _cardService.InvalidateCache();
        _prayerService.InvalidateCache();
        await _nav.GoToAsync(Routes.PrayerCardsTab + "?imported=true");
    }

    private async Task HandleCardAsync(System.Collections.Specialized.NameValueCollection query)
    {
        string? title;
        SharedRequest[]? requests;

        // Future-proof: if a newer format version arrives, tell user to update
        var version = query["v"];
        if (version != null && int.TryParse(version, out var v) && v > 2)
        {
            await _nav.DisplayAlertAsync("Update Required",
                "This prayer was shared from a newer version of Practicing Prayer. Please update the app to import it.", "OK");
            return;
        }

        // New format: compact Base64 payload in ?d= parameter
        var payload = query["d"];
        if (!string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                byte[] jsonBytes;
                if (payload.StartsWith("z."))
                    jsonBytes = GzipDecompress(FromBase64Url(payload[2..]));
                else
                    jsonBytes = FromBase64Url(payload);

                var json = Encoding.UTF8.GetString(jsonBytes);
                var data = JsonSerializer.Deserialize<SharedCard>(json);
                title = NormalizeQuotes(data?.Title);
                requests = data?.Requests;
            }
            catch (Exception ex) when (ex is FormatException or JsonException or InvalidDataException)
            {
                System.Diagnostics.Debug.WriteLine($"[DeepLink] Invalid shared card data: {ex.Message}");
                await _nav.DisplayAlertAsync("Unable to Import", "The shared prayer card link appears to be invalid or incomplete.", "OK");
                return;
            }
        }
        else
        {
            // Legacy format: ?title=&requests= query params (backward compatibility)
            title = query["title"];
            var requestsBase64 = query["requests"];
            if (string.IsNullOrWhiteSpace(requestsBase64))
                return;

            try
            {
                var json = Encoding.UTF8.GetString(FromBase64Url(requestsBase64));
                requests = JsonSerializer.Deserialize<SharedRequest[]>(json);
            }
            catch (Exception ex) when (ex is FormatException or JsonException or InvalidDataException)
            {
                System.Diagnostics.Debug.WriteLine($"[DeepLink] Invalid shared card data: {ex.Message}");
                await _nav.DisplayAlertAsync("Unable to Import", "The shared prayer card link appears to be invalid or incomplete.", "OK");
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(title))
            return;

        // Filter out empty-title requests (data quality guard)
        requests = requests?.Where(r => !string.IsNullOrWhiteSpace(r?.Title)).ToArray();

        if (requests is null || requests.Length == 0)
            return;

        var prayerCount = requests.Length;
        var preview = $"\"{title}\" with {prayerCount} prayer{(prayerCount == 1 ? "" : "s")}";
        var confirmed = await _nav.DisplayConfirmAsync(
            "Prayer Card Shared With You",
            $"{preview}\n\nSave to your prayer journal?",
            "Save", "Decline");
        if (!confirmed) return;

        var card = new PrayerCard
        {
            Title = title,
            IsImported = true
        };
        await card.SaveAsync();

        foreach (var req in requests)
        {
            var prayer = new Prayer
            {
                PrayerCardId = card.Id,
                Title = NormalizeQuotes(req.Title) ?? "",
                Details = string.IsNullOrEmpty(req.Notes) ? null : NormalizeQuotes(req.Notes),
                IsImported = true,
                CanNotify = false
            };
            await prayer.SaveAsync();
        }

        _cardService.InvalidateCache();
        _prayerService.InvalidateCache();
        await _nav.GoToAsync(Routes.PrayerCardsTab + "?imported=true");
    }

    private static byte[] GzipCompress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
            gzip.Write(data);
        return output.ToArray();
    }

    private static byte[] GzipDecompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
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

    /// <summary>Replace smart/curly quotes with ASCII equivalents so URLs and text stay clean.</summary>
    private static string? NormalizeQuotes(string? text)
    {
        if (text is null) return null;
        return text
            .Replace('\u2018', '\'')  // left single quote
            .Replace('\u2019', '\'')  // right single quote
            .Replace('\u201C', '"')   // left double quote
            .Replace('\u201D', '"');   // right double quote
    }

    private record SharedRequest(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("notes")] string? Notes);

    private record SharedCard(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("requests")] SharedRequest[]? Requests);
}

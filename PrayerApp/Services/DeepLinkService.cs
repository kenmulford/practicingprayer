using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.Messaging;
using PrayerApp.Messages;
using PrayerApp.Models;
using static PrayerApp.Helpers.TextNormalization;

namespace PrayerApp.Services;

public class DeepLinkService : IDeepLinkService
{
    private const string BaseUrl = "https://practicingprayerapp.com/share";
    private const string Footer = "(Shared via Practicing Prayer)";
    private const int MaxUrlLength = 1800;

    private readonly ICardService _cardService;
    private readonly IPrayerService _prayerService;
    private readonly INavigationService _nav;
    private readonly IShareService _shareService;
    private readonly IMessenger _messenger;
    private readonly Func<string> _getCacheDir;

    public DeepLinkService(ICardService cardService, IPrayerService prayerService,
        INavigationService nav, IShareService shareService, IMessenger messenger)
        : this(cardService, prayerService, nav, shareService, messenger, () => FileSystem.CacheDirectory)
    {
    }

    // Test-only constructor: avoids MAUI FileSystem dependency
    internal DeepLinkService(ICardService cardService, IPrayerService prayerService,
        INavigationService nav, IShareService shareService, IMessenger messenger, Func<string> getCacheDir)
    {
        _cardService = cardService;
        _prayerService = prayerService;
        _nav = nav;
        _shareService = shareService;
        _messenger = messenger;
        _getCacheDir = getCacheDir;
    }

    // ── Outbound (sharing) ──────────────────────────────────────────────────

    public async Task ShareRequestAsync(Prayer prayer)
    {
        var title = NormalizeQuotes(prayer.Title);
        var details = prayer.Details is not null ? NormalizeQuotes(prayer.Details) : null;

        var json = JsonSerializer.Serialize(new { title, notes = details ?? "" });
        var compressed = GzipCompress(Encoding.UTF8.GetBytes(json));
        var base64 = "z." + ToBase64Url(compressed);
        var uri = $"{BaseUrl}/r?v=2&d={base64}";

        if (uri.Length <= MaxUrlLength)
        {
            var fallback = string.IsNullOrWhiteSpace(details)
                ? title
                : $"{title}\n{details}";
            var text = $"{uri}\n\n{fallback}\n\n{Footer}";
            await _shareService.ShareTextAsync(prayer.Title, text);
        }
        else
        {
            await ShareAsFileAsync(compressed, title ?? "shared-prayer");
        }
    }

    public async Task ShareCardAsync(PrayerCard card, IEnumerable<Prayer> prayers)
    {
        var title = NormalizeQuotes(card.Title);
        var requestList = prayers.Select(p => new
        {
            title = NormalizeQuotes(p.Title),
            notes = p.Details is not null ? NormalizeQuotes(p.Details) : ""
        }).ToArray();
        var json = JsonSerializer.Serialize(new { title, requests = requestList });
        var compressed = GzipCompress(Encoding.UTF8.GetBytes(json));
        var base64 = "z." + ToBase64Url(compressed);
        var uri = $"{BaseUrl}/c?v=2&d={base64}";

        var prayerCount = requestList.Length;

        if (uri.Length <= MaxUrlLength)
        {
            var sb = new StringBuilder();
            sb.AppendLine(uri);
            sb.AppendLine();
            sb.AppendLine(title);
            sb.AppendLine($"+ {prayerCount} request{(prayerCount == 1 ? "" : "s")}");
            sb.AppendLine();
            sb.Append(Footer);
            await _shareService.ShareTextAsync(card.Title, sb.ToString());
        }
        else
        {
            await ShareAsFileAsync(compressed, title ?? "shared-prayer");
        }
    }

    private async Task ShareAsFileAsync(byte[] compressed, string title)
    {
        var cacheDir = _getCacheDir();
        var safeName = SanitizeFileName(title);
        var filePath = Path.Combine(cacheDir, $"{safeName}.prayercard");

        // Clean old .prayercard files from cache (same pattern as BackupService)
        foreach (var old in Directory.GetFiles(cacheDir, "*.prayercard"))
            File.Delete(old);

        await File.WriteAllBytesAsync(filePath, compressed);
        await _shareService.ShareFileAsync($"Share: {title}", filePath, "application/x-prayercard");
    }

    // ── Inbound (receiving via URL) ─────────────────────────────────────────

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

        // Future-proof: if a newer format version arrives, tell user to update
        if (IsUnsupportedVersion(query))
        {
            await _nav.DisplayAlertAsync("Update Required",
                "This prayer was shared from a newer version of Practicing Prayer. Please update the app to import it.", "OK");
            return;
        }

        if (path == "/share/r")
            await HandleRequestAsync(query);
        else if (path == "/share/c")
            await HandleCardAsync(query);
    }

    // ── Inbound (receiving via file) ────────────────────────────────────────

    public async Task HandleFileAsync(Stream fileStream)
    {
        try
        {
            using var ms = new MemoryStream();
            await fileStream.CopyToAsync(ms);
            var compressed = ms.ToArray();
            var jsonBytes = GzipDecompress(compressed);
            var json = Encoding.UTF8.GetString(jsonBytes);

            // Determine card vs request by checking for "requests" key
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("requests", out _))
            {
                var data = JsonSerializer.Deserialize<SharedCard>(json);
                if (data is not null)
                    await ImportCardAsync(data);
            }
            else
            {
                var data = JsonSerializer.Deserialize<SharedRequest>(json);
                if (data is not null)
                    await ImportRequestAsync(data);
            }
        }
        catch (Exception ex) when (ex is FormatException or JsonException or InvalidDataException)
        {
            System.Diagnostics.Debug.WriteLine($"[DeepLink] Invalid .prayercard file: {ex.Message}");
            await _nav.DisplayAlertAsync("Unable to Import",
                "The shared prayer card file appears to be invalid.", "OK");
        }
    }

    // ── URL handlers ────────────────────────────────────────────────────────

    private async Task HandleRequestAsync(System.Collections.Specialized.NameValueCollection query)
    {
        SharedRequest? data = null;

        var payload = query["d"];
        if (!string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                var json = Encoding.UTF8.GetString(DecodePayload(payload));
                data = JsonSerializer.Deserialize<SharedRequest>(json);
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
            var title = query["title"];
            var notes = query["notes"];
            if (!string.IsNullOrWhiteSpace(title))
                data = new SharedRequest(title, notes);
        }

        if (data is not null)
            await ImportRequestAsync(data);
    }

    private async Task HandleCardAsync(System.Collections.Specialized.NameValueCollection query)
    {
        SharedCard? data = null;

        var payload = query["d"];
        if (!string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                var json = Encoding.UTF8.GetString(DecodePayload(payload));
                data = JsonSerializer.Deserialize<SharedCard>(json);
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
            var title = query["title"];
            var requestsBase64 = query["requests"];
            if (!string.IsNullOrWhiteSpace(requestsBase64))
            {
                try
                {
                    var json = Encoding.UTF8.GetString(FromBase64Url(requestsBase64));
                    var requests = JsonSerializer.Deserialize<SharedRequest[]>(json);
                    if (!string.IsNullOrWhiteSpace(title))
                        data = new SharedCard(title, requests);
                }
                catch (Exception ex) when (ex is FormatException or JsonException or InvalidDataException)
                {
                    System.Diagnostics.Debug.WriteLine($"[DeepLink] Invalid shared card data: {ex.Message}");
                    await _nav.DisplayAlertAsync("Unable to Import", "The shared prayer card link appears to be invalid or incomplete.", "OK");
                    return;
                }
            }
        }

        if (data is not null)
            await ImportCardAsync(data);
    }

    // ── Shared import logic ─────────────────────────────────────────────────

    private async Task ImportRequestAsync(SharedRequest data)
    {
        var title = NormalizeQuotes(data.Title);
        var detail = string.IsNullOrEmpty(data.Notes) ? null : NormalizeQuotes(data.Notes);

        if (string.IsNullOrWhiteSpace(title))
            return;

        var isDuplicate = await IsDuplicateRequestAsync(data);
        var preview = string.IsNullOrWhiteSpace(detail) ? title : $"{title}\n\n{detail}";
        var confirmed = await ConfirmImportAsync(
            "Prayer Shared With You",
            preview,
            isDuplicate ? "You've already saved this prayer." : null);
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
        _messenger.Send(new BulkChangedMessage());
        await _nav.GoToAsync(Routes.PrayerCardsTabImported(sharedCard.Id));
    }

    private async Task ImportCardAsync(SharedCard data)
    {
        var title = NormalizeQuotes(data.Title);

        if (string.IsNullOrWhiteSpace(title))
            return;

        // Filter out empty-title requests (data quality guard)
        var requests = data.Requests?.Where(r => !string.IsNullOrWhiteSpace(r?.Title)).ToArray();
        if (requests is null || requests.Length == 0)
            return;

        var isDuplicate = await IsDuplicateCardAsync(data);
        var prayerCount = requests.Length;
        var preview = $"\"{title}\" with {prayerCount} prayer{(prayerCount == 1 ? "" : "s")}";
        var confirmed = await ConfirmImportAsync(
            "Prayer Card Shared With You",
            preview,
            isDuplicate ? "You've already imported this card." : null);
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
        _messenger.Send(new BulkChangedMessage());
        await _nav.GoToAsync(Routes.PrayerCardsTabImported(card.Id));
    }

    // ── Duplicate detection ──────────────────────────────────────────────────

    /// <summary>
    /// Shows an import confirmation dialog with a "Save" button, or "Save Again"
    /// plus a warning line when <paramref name="duplicateNotice"/> is non-null.
    /// </summary>
    private Task<bool> ConfirmImportAsync(string alertTitle, string preview, string? duplicateNotice)
    {
        var isDuplicate = duplicateNotice is not null;
        var prompt = isDuplicate
            ? $"{preview}\n\n{duplicateNotice}"
            : $"{preview}\n\nSave to your prayer journal?";
        var confirmText = isDuplicate ? "Save Again" : "Save";
        return _nav.DisplayConfirmAsync(alertTitle, prompt, confirmText, "Decline");
    }

    /// <summary>
    /// Checks whether an imported card with the same title and prayer title set
    /// already exists. Only considers non-system cards flagged IsImported=true.
    /// Match is case-insensitive, trimmed, smart-quote normalized.
    /// </summary>
    private async Task<bool> IsDuplicateCardAsync(SharedCard data)
    {
        var incomingTitle = NormalizeForMatch(data.Title);
        var incomingPrayerTitles = (data.Requests ?? [])
            .Where(r => !string.IsNullOrWhiteSpace(r?.Title))
            .Select(r => NormalizeForMatch(r!.Title))
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();

        var cards = await _cardService.GetCardsAsync();
        var candidates = cards.Where(c =>
            c.IsImported && !c.IsSystem && NormalizeForMatch(c.Title) == incomingTitle);

        foreach (var card in candidates)
        {
            var prayers = await _prayerService.GetPrayersByCardAsync(card.Id);
            var existingTitles = prayers
                .Where(p => !string.IsNullOrWhiteSpace(p.Title))
                .Select(p => NormalizeForMatch(p.Title))
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList();

            if (existingTitles.SequenceEqual(incomingPrayerTitles))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Checks whether an imported prayer with the same title and notes already
    /// exists in the "Shared with me" card. Match is case-insensitive, trimmed,
    /// smart-quote normalized. Returns false if the shared card doesn't exist
    /// yet (no previous imports).
    /// </summary>
    private async Task<bool> IsDuplicateRequestAsync(SharedRequest data)
    {
        var cards = await _cardService.GetCardsAsync();
        var sharedCard = cards.FirstOrDefault(c =>
            c.IsSystem && c.Title == PrayerCard.TitleSharedWithMe);
        if (sharedCard is null)
            return false;

        var incomingTitle = NormalizeForMatch(data.Title);
        var incomingNotes = NormalizeForMatch(data.Notes);

        var prayers = await _prayerService.GetPrayersByCardAsync(sharedCard.Id);
        return prayers.Any(p =>
            NormalizeForMatch(p.Title) == incomingTitle &&
            NormalizeForMatch(p.Details) == incomingNotes);
    }

    private static string NormalizeForMatch(string? s)
        => (NormalizeQuotes(s) ?? "").Trim().ToLowerInvariant();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool IsUnsupportedVersion(System.Collections.Specialized.NameValueCollection query)
    {
        var version = query["v"];
        return version != null && int.TryParse(version, out var v) && v > 2;
    }

    private static byte[] DecodePayload(string payload)
    {
        // Strip trailing non-base64url characters that some messaging apps
        // inject when they URL-encode the fallback share text into the d= param
        // (email, Slack, Discord, Teams — SMS apps stop at literal whitespace)
        payload = TakeBase64UrlPrefix(payload);

        if (payload.StartsWith("z."))
            return GzipDecompress(FromBase64Url(payload[2..]));
        return FromBase64Url(payload);
    }

    private static string TakeBase64UrlPrefix(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            bool valid = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                         (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.';
            if (!valid) return s[..i];
        }
        return s;
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

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "shared-prayer" : sanitized;
    }

    private record SharedRequest(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("notes")] string? Notes);

    private record SharedCard(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("requests")] SharedRequest[]? Requests);
}

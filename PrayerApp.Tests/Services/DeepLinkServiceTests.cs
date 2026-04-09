using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;

namespace PrayerApp.Tests.Services;

public class DeepLinkServiceTests
{
    private readonly IDBService _db;
    private readonly ICardService _cardService;
    private readonly IPrayerService _prayerService;
    private readonly INavigationService _nav;
    private readonly DeepLinkService _service;

    public DeepLinkServiceTests()
    {
        _db = Substitute.For<IDBService>();
        PrayerCard.SetDBService(_db);
        Prayer.SetDBService(_db);

        _cardService = Substitute.For<ICardService>();
        _prayerService = Substitute.For<IPrayerService>();
        _nav = Substitute.For<INavigationService>();
        // Default: user accepts import confirmation
        _nav.DisplayConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);
        _service = new DeepLinkService(_cardService, _prayerService, _nav);
    }

    // ── BuildRequestShareText ────────────────────────────────────────────────

    [Fact]
    public void BuildRequestShareText_IncludesUriAndFallback()
    {
        var prayer = new Prayer { Title = "Heal my friend", Details = "Surgery next week" };

        var result = _service.BuildRequestShareText(prayer);

        Assert.Contains("https://practicingprayerapp.com/share/r?", result);
        Assert.Contains("Heal my friend", result);
        Assert.Contains("Surgery next week", result);
        Assert.Contains("Shared via Practicing Prayer", result);
    }

    [Fact]
    public void BuildRequestShareText_UrlIsSafe_NoRawSpecialChars()
    {
        var prayer = new Prayer { Title = "John & Jane\u2019s prayer", Details = "Details with spaces" };

        var result = _service.BuildRequestShareText(prayer);

        var lines = result.Split('\n');
        var uriLine = lines.First(l => l.StartsWith("https://"));
        // URL should be compact Base64 — no raw spaces, quotes, or ampersands
        Assert.DoesNotContain(" ", uriLine);
        Assert.DoesNotContain("'", uriLine);
        Assert.DoesNotContain("&", uriLine.Split('?').Last().Split('=').First()); // no & in param name
        // Fallback text should have readable ASCII apostrophes
        Assert.Contains("John & Jane's prayer", result);
    }

    [Fact]
    public void BuildRequestShareText_UsesBase64Payload_NotQueryParams()
    {
        var prayer = new Prayer { Title = "Mom\u2019s surgery", Details = "Please pray for healing" };

        var result = _service.BuildRequestShareText(prayer);
        var uriLine = result.Split('\n').First(l => l.StartsWith("https://"));

        // URL should use v=2 and compressed Base64 payload, not long query params
        Assert.Contains("/share/r?v=2&d=z.", uriLine);
        Assert.DoesNotContain("title=", uriLine);
        Assert.DoesNotContain("notes=", uriLine);
        // No raw smart quotes or percent-encoded multibyte chars in URL
        Assert.DoesNotContain("\u2019", uriLine);
        Assert.DoesNotContain("%E2%80%99", uriLine);
    }

    [Fact]
    public void BuildRequestShareText_NormalizesSmartQuotes_InFallbackText()
    {
        var prayer = new Prayer { Title = "Mom\u2019s surgery" };

        var result = _service.BuildRequestShareText(prayer);

        // Fallback body should have readable ASCII apostrophes
        var fallbackLines = result.Split('\n').Where(l => !l.StartsWith("https://")).ToList();
        var fallback = string.Join('\n', fallbackLines);
        Assert.Contains("Mom's surgery", fallback);
    }

    [Fact]
    public async Task HandleAsync_Request_Base64Payload_RoundTrips()
    {
        var sharedCard = new PrayerCard { Id = 10, Title = "Shared with me", IsSystem = true };
        _cardService.GetOrCreateSharedCardAsync().Returns(Task.FromResult(sharedCard));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        // Build a share URL the new way (Base64 payload)
        var prayer = new Prayer { Title = "Mom\u2019s surgery", Details = "Please pray" };
        var shareText = _service.BuildRequestShareText(prayer);
        var uriLine = shareText.Split('\n').First(l => l.StartsWith("https://"));

        await _service.HandleAsync(uriLine);

        // Smart quote should be normalized to ASCII on save
        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p =>
            p.Title == "Mom's surgery" &&
            p.Details == "Please pray"));
    }

    [Fact]
    public async Task HandleAsync_Request_LegacyQueryParams_StillWorks()
    {
        var sharedCard = new PrayerCard { Id = 10, Title = "Shared with me", IsSystem = true };
        _cardService.GetOrCreateSharedCardAsync().Returns(Task.FromResult(sharedCard));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        // Old-format URL with title/notes query params still works
        await _service.HandleAsync("https://practicingprayerapp.com/share/r?title=Test%20Prayer&notes=Details");

        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p =>
            p.Title == "Test Prayer" &&
            p.Details == "Details"));
    }

    [Fact]
    public void BuildRequestShareText_NullDetails_StillBuildsValidUrl()
    {
        var prayer = new Prayer { Title = "Simple prayer", Details = null };

        var result = _service.BuildRequestShareText(prayer);

        var uriLine = result.Split('\n').First(l => l.StartsWith("https://"));
        Assert.Contains("/share/r?v=2&d=z.", uriLine);
        // Fallback should not include details
        Assert.DoesNotContain("null", result.ToLowerInvariant());
    }

    // ── BuildCardShareText ───────────────────────────────────────────────────

    [Fact]
    public void BuildCardShareText_ShowsTitleAndRequestCount()
    {
        var card = new PrayerCard { Title = "Family Prayers" };
        var prayers = new List<Prayer>
        {
            new() { Title = "Mom\u2019s health" },
            new() { Title = "Dad\u2019s job" }
        };

        var result = _service.BuildCardShareText(card, prayers);

        Assert.Contains("https://practicingprayerapp.com/share/c?v=2&d=z.", result);
        Assert.Contains("Family Prayers", result);
        Assert.Contains("+ 2 requests", result);
        Assert.Contains("Shared via Practicing Prayer", result);
        // Individual prayers should NOT be in fallback (keeps message clean)
        Assert.DoesNotContain("Mom", result.Split('\n').Where(l => l.StartsWith("- ")).FirstOrDefault() ?? "");
    }

    [Fact]
    public void BuildCardShareText_EmptyPrayers_StillIncludesLink()
    {
        var card = new PrayerCard { Title = "Empty Card" };
        var prayers = new List<Prayer>();

        var result = _service.BuildCardShareText(card, prayers);

        Assert.Contains("https://practicingprayerapp.com/share/c?", result);
        Assert.Contains("Empty Card", result);
    }

    // ── HandleAsync — Request ────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Request_CreatesInSharedCard()
    {
        var sharedCard = new PrayerCard { Id = 10, Title = "Shared with me", IsSystem = true };
        _cardService.GetOrCreateSharedCardAsync().Returns(Task.FromResult(sharedCard));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        // Build a share URL using the service, then handle it (roundtrip test)
        var prayer = new Prayer { Title = "Test Prayer", Details = "Please pray" };
        var shareText = _service.BuildRequestShareText(prayer);
        var uriLine = shareText.Split('\n').First(l => l.StartsWith("https://"));

        await _service.HandleAsync(uriLine);

        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p =>
            p.Title == "Test Prayer" &&
            p.Details == "Please pray" &&
            p.PrayerCardId == 10 &&
            p.IsImported == true &&
            p.CanNotify == false));
    }

    [Fact]
    public async Task HandleAsync_Request_NavigatesToCardsTabWithImportedFlag()
    {
        var sharedCard = new PrayerCard { Id = 10, Title = "Shared with me", IsSystem = true };
        _cardService.GetOrCreateSharedCardAsync().Returns(Task.FromResult(sharedCard));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        var prayer = new Prayer { Title = "Test" };
        var shareText = _service.BuildRequestShareText(prayer);
        var uriLine = shareText.Split('\n').First(l => l.StartsWith("https://"));

        await _service.HandleAsync(uriLine);

        await _nav.Received(1).GoToAsync(Routes.PrayerCardsTab + "?imported=true");
    }

    [Fact]
    public async Task HandleAsync_Request_InvalidatesCaches()
    {
        var sharedCard = new PrayerCard { Id = 10, Title = "Shared with me", IsSystem = true };
        _cardService.GetOrCreateSharedCardAsync().Returns(Task.FromResult(sharedCard));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        var prayer = new Prayer { Title = "Test" };
        var shareText = _service.BuildRequestShareText(prayer);
        var uriLine = shareText.Split('\n').First(l => l.StartsWith("https://"));

        await _service.HandleAsync(uriLine);

        _cardService.Received(1).InvalidateCache();
        _prayerService.Received(1).InvalidateCache();
    }

    // ── HandleAsync — Card ───────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Card_CreatesCardWithPrayers()
    {
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));
        _cardService.InvalidateCache();

        // Build a valid card share URI with base64url-encoded requests JSON
        var requestsJson = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { title = "Prayer 1", notes = "Details 1" },
            new { title = "Prayer 2", notes = "" }
        });
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(requestsJson))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var uri = $"https://practicingprayerapp.com/share/c?title=Shared%20Card&requests={base64}";

        await _service.HandleAsync(uri);

        await _db.Received(1).InsertAsync(Arg.Is<PrayerCard>(c =>
            c.Title == "Shared Card" &&
            c.IsImported == true));
        await _db.Received(2).InsertAsync(Arg.Any<Prayer>());
    }

    [Fact]
    public async Task HandleAsync_Card_NavigatesToCardsTab()
    {
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        var requestsJson = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { title = "Prayer 1", notes = "" }
        });
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(requestsJson))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        await _service.HandleAsync($"https://practicingprayerapp.com/share/c?title=Test&requests={base64}");

        await _nav.Received(1).GoToAsync(Routes.PrayerCardsTab + "?imported=true");
    }

    // ── HandleAsync — Invalid URIs ───────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_InvalidUri_NoOp()
    {
        await _service.HandleAsync("https://example.com/other");

        await _db.DidNotReceive().InsertAsync(Arg.Any<Prayer>());
        await _db.DidNotReceive().InsertAsync(Arg.Any<PrayerCard>());
    }

    [Fact]
    public async Task HandleAsync_EmptyUri_NoOp()
    {
        await _service.HandleAsync("");

        await _db.DidNotReceive().InsertAsync(Arg.Any<Prayer>());
    }

    [Fact]
    public async Task HandleAsync_MissingTitle_NoOp()
    {
        await _service.HandleAsync("https://practicingprayerapp.com/share/r?notes=something");

        await _db.DidNotReceive().InsertAsync(Arg.Any<Prayer>());
    }

    [Fact]
    public async Task HandleAsync_Request_Declined_DoesNotSave()
    {
        _nav.DisplayConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        await _service.HandleAsync("https://practicingprayerapp.com/share/r?title=Test");

        await _db.DidNotReceive().InsertAsync(Arg.Any<Prayer>());
    }

    [Fact]
    public async Task HandleAsync_Card_Declined_DoesNotSave()
    {
        _nav.DisplayConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        var requestsJson = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { title = "Prayer 1", notes = "" }
        });
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(requestsJson))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        await _service.HandleAsync($"https://practicingprayerapp.com/share/c?title=Test&requests={base64}");

        await _db.DidNotReceive().InsertAsync(Arg.Any<PrayerCard>());
        await _db.DidNotReceive().InsertAsync(Arg.Any<Prayer>());
    }

    [Fact]
    public async Task HandleAsync_Card_InvalidBase64_NoOp()
    {
        await _service.HandleAsync("https://practicingprayerapp.com/share/c?title=Test&requests=!!!invalid!!!");

        // Should not create a card when the requests payload is corrupt
        await _db.DidNotReceive().InsertAsync(Arg.Any<PrayerCard>());
        await _db.DidNotReceive().InsertAsync(Arg.Any<Prayer>());
    }

    [Fact]
    public async Task HandleAsync_Card_InvalidJson_NoOp()
    {
        // Valid base64 encoding of "not valid json"
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("not valid json"))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        await _service.HandleAsync($"https://practicingprayerapp.com/share/c?title=Test&requests={base64}");

        await _db.DidNotReceive().InsertAsync(Arg.Any<PrayerCard>());
        await _db.DidNotReceive().InsertAsync(Arg.Any<Prayer>());
    }

    // ── Phase 2: GZip Compression ───────────────────────────────────────────

    [Fact]
    public void BuildRequestShareText_GzipFormat_ContainsZPrefix()
    {
        var prayer = new Prayer { Title = "Test prayer", Details = "Some details" };

        var result = _service.BuildRequestShareText(prayer);

        var uriLine = result.Split('\n').First(l => l.StartsWith("https://"));
        // URL should contain v=2 version param and z.-prefixed compressed payload
        Assert.Contains("v=2", uriLine);
        Assert.Contains("d=z.", uriLine);
    }

    [Fact]
    public async Task HandleAsync_Request_GzipPayload_RoundTrips()
    {
        var sharedCard = new PrayerCard { Id = 10, Title = "Shared with me", IsSystem = true };
        _cardService.GetOrCreateSharedCardAsync().Returns(Task.FromResult(sharedCard));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        // Build a compressed share URL, then handle it (roundtrip)
        var prayer = new Prayer { Title = "Mom\u2019s surgery", Details = "Please pray for healing" };
        var shareText = _service.BuildRequestShareText(prayer);
        var uriLine = shareText.Split('\n').First(l => l.StartsWith("https://"));

        await _service.HandleAsync(uriLine);

        // Smart quote normalized to ASCII, details preserved
        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p =>
            p.Title == "Mom's surgery" &&
            p.Details == "Please pray for healing" &&
            p.PrayerCardId == 10 &&
            p.IsImported == true &&
            p.CanNotify == false));
    }

    [Fact]
    public async Task HandleAsync_Card_GzipPayload_RoundTrips()
    {
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        var card = new PrayerCard { Title = "Family Prayers" };
        var prayers = new List<Prayer>
        {
            new() { Title = "Mom\u2019s health", Details = "Ongoing treatment" },
            new() { Title = "Dad\u2019s job", Details = null }
        };

        var shareText = _service.BuildCardShareText(card, prayers);
        var uriLine = shareText.Split('\n').First(l => l.StartsWith("https://"));

        await _service.HandleAsync(uriLine);

        await _db.Received(1).InsertAsync(Arg.Is<PrayerCard>(c =>
            c.Title == "Family Prayers" &&
            c.IsImported == true));
        // Verify individual prayer content is correctly decoded (smart quotes normalized)
        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p =>
            p.Title == "Mom's health" && p.Details == "Ongoing treatment" && p.IsImported == true));
        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p =>
            p.Title == "Dad's job" && p.IsImported == true));
    }

    [Fact]
    public async Task HandleAsync_Request_UncompressedPayload_StillWorks()
    {
        // Manually construct an old-format URL without z. prefix (backward compat)
        var sharedCard = new PrayerCard { Id = 10, Title = "Shared with me", IsSystem = true };
        _cardService.GetOrCreateSharedCardAsync().Returns(Task.FromResult(sharedCard));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        var json = System.Text.Json.JsonSerializer.Serialize(new { title = "Old format prayer", notes = "Details here" });
        var rawBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        await _service.HandleAsync($"https://practicingprayerapp.com/share/r?d={rawBase64}");

        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p =>
            p.Title == "Old format prayer" &&
            p.Details == "Details here"));
    }

    [Fact]
    public async Task HandleAsync_Request_GzipPayload_CorruptData_ShowsAlert()
    {
        // z. prefix but garbage data after it
        var corruptBase64 = Convert.ToBase64String(new byte[] { 0xFF, 0xFE, 0xFD, 0xFC })
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        await _service.HandleAsync($"https://practicingprayerapp.com/share/r?v=2&d=z.{corruptBase64}");

        await _nav.Received(1).DisplayAlertAsync(
            "Unable to Import",
            Arg.Any<string>(),
            "OK");
        await _db.DidNotReceive().InsertAsync(Arg.Any<Prayer>());
    }

    [Fact]
    public async Task HandleAsync_Request_FutureVersion_ShowsUpdateAlert()
    {
        // A v=3 link from a future app version should prompt the user to update
        var json = System.Text.Json.JsonSerializer.Serialize(new { title = "Future", notes = "" });
        var rawBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        await _service.HandleAsync($"https://practicingprayerapp.com/share/r?v=3&d=z.{rawBase64}");

        await _nav.Received(1).DisplayAlertAsync(
            "Update Required",
            Arg.Is<string>(s => s.Contains("newer version")),
            "OK");
        await _db.DidNotReceive().InsertAsync(Arg.Any<Prayer>());
    }
}

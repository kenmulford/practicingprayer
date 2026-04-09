using System.IO.Compression;
using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;

namespace PrayerApp.Tests.Services;

public class DeepLinkServiceTests : IDisposable
{
    private readonly IDBService _db;
    private readonly ICardService _cardService;
    private readonly IPrayerService _prayerService;
    private readonly INavigationService _nav;
    private readonly IShareService _shareService;
    private readonly DeepLinkService _service;
    private readonly string _tempDir;

    public DeepLinkServiceTests()
    {
        _db = Substitute.For<IDBService>();
        PrayerCard.SetDBService(_db);
        Prayer.SetDBService(_db);

        _cardService = Substitute.For<ICardService>();
        _prayerService = Substitute.For<IPrayerService>();
        _nav = Substitute.For<INavigationService>();
        _shareService = Substitute.For<IShareService>();
        // Default: user accepts import confirmation
        _nav.DisplayConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);
        _tempDir = Path.Combine(Path.GetTempPath(), $"deeplink-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _service = new DeepLinkService(_cardService, _prayerService, _nav, _shareService, () => _tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── Helper: build a compressed URL the same way the service does ────────

    private static string BuildCompressedUrl(string path, string json)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            gzip.Write(bytes);
        }
        var base64 = Convert.ToBase64String(output.ToArray())
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return $"https://practicingprayerapp.com/share/{path}?v=2&d=z.{base64}";
    }

    // ── ShareRequestAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task ShareRequestAsync_SmallRequest_SharesAsText()
    {
        var prayer = new Prayer { Title = "Heal my friend", Details = "Surgery next week" };

        await _service.ShareRequestAsync(prayer);

        await _shareService.Received(1).ShareTextAsync(
            Arg.Any<string>(),
            Arg.Is<string>(text =>
                text.Contains("https://practicingprayerapp.com/share/r?v=2&d=z.") &&
                text.Contains("Heal my friend") &&
                text.Contains("Surgery next week") &&
                text.Contains("Shared via Practicing Prayer")));
    }

    [Fact]
    public async Task ShareRequestAsync_NormalizesSmartQuotes()
    {
        var prayer = new Prayer { Title = "Mom\u2019s surgery" };

        await _service.ShareRequestAsync(prayer);

        await _shareService.Received(1).ShareTextAsync(
            Arg.Any<string>(),
            Arg.Is<string>(text => text.Contains("Mom's surgery")));
    }

    [Fact]
    public async Task ShareRequestAsync_NullDetails_SharesWithoutDetails()
    {
        var prayer = new Prayer { Title = "Simple prayer", Details = null };

        await _service.ShareRequestAsync(prayer);

        await _shareService.Received(1).ShareTextAsync(
            Arg.Any<string>(),
            Arg.Is<string>(text =>
                text.Contains("/share/r?v=2&d=z.") &&
                !text.Contains("null", StringComparison.OrdinalIgnoreCase)));
    }

    // ── ShareCardAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task ShareCardAsync_SmallCard_SharesAsText()
    {
        var card = new PrayerCard { Title = "Family Prayers" };
        var prayers = new List<Prayer>
        {
            new() { Title = "Mom\u2019s health" },
            new() { Title = "Dad\u2019s job" }
        };

        await _service.ShareCardAsync(card, prayers);

        await _shareService.Received(1).ShareTextAsync(
            Arg.Any<string>(),
            Arg.Is<string>(text =>
                text.Contains("https://practicingprayerapp.com/share/c?v=2&d=z.") &&
                text.Contains("Family Prayers") &&
                text.Contains("+ 2 requests") &&
                text.Contains("Shared via Practicing Prayer")));
    }

    [Fact]
    public async Task ShareCardAsync_LargeCard_SharesAsFile()
    {
        var card = new PrayerCard { Title = "Big Card" };
        // Generate prayers with unique text that doesn't compress well (GZip excels
        // at repetitive patterns, so we need varied content to exceed 1800 chars)
        var rng = new Random(42); // deterministic seed
        var prayers = Enumerable.Range(1, 100).Select(i =>
        {
            var guid = new Guid(rng.Next(), 0, 0, new byte[8]).ToString();
            return new Prayer
            {
                Title = $"Unique prayer {guid} for person {i}",
                Details = $"Detailed notes {guid} about situation {i}: {Guid.NewGuid()}"
            };
        }).ToList();

        await _service.ShareCardAsync(card, prayers);

        await _shareService.Received(1).ShareFileAsync(
            Arg.Is<string>(t => t.Contains("Big Card")),
            Arg.Is<string>(path => path.EndsWith(".prayercard")),
            "application/x-prayercard");
        // Should NOT have called ShareTextAsync
        await _shareService.DidNotReceive().ShareTextAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    // ── HandleAsync — Request ───────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Request_GzipPayload_RoundTrips()
    {
        var sharedCard = new PrayerCard { Id = 10, Title = "Shared with me", IsSystem = true };
        _cardService.GetOrCreateSharedCardAsync().Returns(Task.FromResult(sharedCard));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        var json = System.Text.Json.JsonSerializer.Serialize(new { title = "Mom\u2019s surgery", notes = "Please pray" });
        var uri = BuildCompressedUrl("r", json);

        await _service.HandleAsync(uri);

        // Smart quote preserved from JSON (NormalizeQuotes applies on import)
        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p =>
            p.Title == "Mom's surgery" &&
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

        var json = System.Text.Json.JsonSerializer.Serialize(new { title = "Test", notes = "" });
        var uri = BuildCompressedUrl("r", json);

        await _service.HandleAsync(uri);

        await _nav.Received(1).GoToAsync(Routes.PrayerCardsTab + "?imported=true");
    }

    [Fact]
    public async Task HandleAsync_Request_InvalidatesCaches()
    {
        var sharedCard = new PrayerCard { Id = 10, Title = "Shared with me", IsSystem = true };
        _cardService.GetOrCreateSharedCardAsync().Returns(Task.FromResult(sharedCard));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        var json = System.Text.Json.JsonSerializer.Serialize(new { title = "Test", notes = "" });
        var uri = BuildCompressedUrl("r", json);

        await _service.HandleAsync(uri);

        _cardService.Received(1).InvalidateCache();
        _prayerService.Received(1).InvalidateCache();
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
    public async Task HandleAsync_Request_UncompressedPayload_StillWorks()
    {
        // Old-format URL without z. prefix (backward compat)
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

    // ── HandleAsync — Card ──────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Card_GzipPayload_RoundTrips()
    {
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            title = "Family Prayers",
            requests = new[]
            {
                new { title = "Mom\u2019s health", notes = "Ongoing treatment" },
                new { title = "Dad\u2019s job", notes = "" }
            }
        });
        var uri = BuildCompressedUrl("c", json);

        await _service.HandleAsync(uri);

        await _db.Received(1).InsertAsync(Arg.Is<PrayerCard>(c =>
            c.Title == "Family Prayers" &&
            c.IsImported == true));
        // Verify individual prayer content is correctly decoded (smart quotes normalized to ASCII)
        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p =>
            p.Title == "Mom's health" && p.Details == "Ongoing treatment" && p.IsImported == true));
        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p =>
            p.Title == "Dad's job" && p.IsImported == true));
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

    [Fact]
    public async Task HandleAsync_Card_LegacyFormat_CreatesCardWithPrayers()
    {
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

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

    // ── HandleAsync — Invalid URIs ──────────────────────────────────────────

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
    public async Task HandleAsync_Card_InvalidBase64_ShowsAlert()
    {
        await _service.HandleAsync("https://practicingprayerapp.com/share/c?title=Test&requests=!!!invalid!!!");

        await _nav.Received(1).DisplayAlertAsync("Unable to Import", Arg.Any<string>(), "OK");
        await _db.DidNotReceive().InsertAsync(Arg.Any<PrayerCard>());
    }

    [Fact]
    public async Task HandleAsync_Card_InvalidJson_ShowsAlert()
    {
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("not valid json"))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        await _service.HandleAsync($"https://practicingprayerapp.com/share/c?title=Test&requests={base64}");

        await _nav.Received(1).DisplayAlertAsync("Unable to Import", Arg.Any<string>(), "OK");
        await _db.DidNotReceive().InsertAsync(Arg.Any<PrayerCard>());
    }

    // ── GZip-specific ───────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Request_GzipPayload_CorruptData_ShowsAlert()
    {
        // z. prefix but garbage data after it
        var corruptBase64 = Convert.ToBase64String(new byte[] { 0xFF, 0xFE, 0xFD, 0xFC })
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        await _service.HandleAsync($"https://practicingprayerapp.com/share/r?v=2&d=z.{corruptBase64}");

        await _nav.Received(1).DisplayAlertAsync("Unable to Import", Arg.Any<string>(), "OK");
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

    // ── HandleFileAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task HandleFileAsync_ValidCardStream_CreatesCardWithPrayers()
    {
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            title = "Shared Card",
            requests = new[]
            {
                new { title = "Prayer 1", notes = "Details 1" },
                new { title = "Prayer 2", notes = "" }
            }
        });
        using var stream = CompressToStream(json);

        await _service.HandleFileAsync(stream);

        await _db.Received(1).InsertAsync(Arg.Is<PrayerCard>(c =>
            c.Title == "Shared Card" && c.IsImported == true));
        await _db.Received(2).InsertAsync(Arg.Any<Prayer>());
    }

    [Fact]
    public async Task HandleFileAsync_ValidRequestStream_CreatesPrayer()
    {
        var sharedCard = new PrayerCard { Id = 10, Title = "Shared with me", IsSystem = true };
        _cardService.GetOrCreateSharedCardAsync().Returns(Task.FromResult(sharedCard));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        var json = System.Text.Json.JsonSerializer.Serialize(new { title = "Test Prayer", notes = "Details" });
        using var stream = CompressToStream(json);

        await _service.HandleFileAsync(stream);

        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p =>
            p.Title == "Test Prayer" &&
            p.Details == "Details" &&
            p.IsImported == true &&
            p.CanNotify == false));
    }

    [Fact]
    public async Task HandleFileAsync_CorruptStream_ShowsAlert()
    {
        using var stream = new MemoryStream(new byte[] { 0xFF, 0xFE, 0xFD, 0xFC });

        await _service.HandleFileAsync(stream);

        await _nav.Received(1).DisplayAlertAsync("Unable to Import", Arg.Any<string>(), "OK");
        await _db.DidNotReceive().InsertAsync(Arg.Any<Prayer>());
        await _db.DidNotReceive().InsertAsync(Arg.Any<PrayerCard>());
    }

    [Fact]
    public async Task HandleFileAsync_EmptyStream_ShowsAlert()
    {
        using var stream = new MemoryStream();

        await _service.HandleFileAsync(stream);

        await _nav.Received(1).DisplayAlertAsync("Unable to Import", Arg.Any<string>(), "OK");
        await _db.DidNotReceive().InsertAsync(Arg.Any<Prayer>());
    }

    // ── Helper ──────────────────────────────────────────────────────────────

    private static MemoryStream CompressToStream(string json)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            gzip.Write(bytes);
        }
        return new MemoryStream(output.ToArray());
    }
}

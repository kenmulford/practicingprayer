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

    // ── Real-world URL import ─────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_RealWorldCardUrl_ImportsAllPrayers()
    {
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        // Actual URL generated by sharing a 20-prayer test card
        var url = "https://practicingprayerapp.com/share/c?v=2&d=z.H4sIAAAAAAAAA-2Xu04DMRBFf2XkeonY8E6HqJAoIkGHKIx3SAZ5H_F4IqEo_85sItHgH1h7i218PZo9Os31wUSKHs3KvCFHeLKhgQt46QO28DywtKYyAXeiIZvV--Hv-jrYHwxQa971ETU05ykap6DpfR-AKYJtMVbg-o7RRYwSwDY0EDvqNoCe4gIePe3EtuOlvfWeGPZjACgQiIVBP6czEb5QNmR1ZL21jN5rJj4GcsjQ2IE-9aAT720FqKudk5ZtB9_CsT__gy7SYAGvwgN2DTEj7ERXnjZqdhqHvQ0kvIAMmcyx-qdxOWucGlNK49WscWpMKY3Xs8apMaU03swap8aU0nibt8YU8l15yPflIT-Uh1xfFsic-RstyZz5gybJnHn7TzJnXpWTzJn3yiRzgSWsLrCF1QXWsLrAHrbMvod9HH8B4fHoj1sXAAA";

        await _service.HandleAsync(url);

        await _db.Received(1).InsertAsync(Arg.Is<PrayerCard>(c =>
            c.Title == "Test Card - Lorem Ipsum" &&
            c.IsImported == true));
        await _db.Received(20).InsertAsync(Arg.Any<Prayer>());
        // Spot-check first and last prayer
        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p =>
            p.Title == "Prayer 1" && p.Details!.StartsWith("Lorem ipsum") && p.IsImported == true));
        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p =>
            p.Title == "Prayer 20" && p.IsImported == true));
    }

    // ── URL cleanup: trailing fallback text from messaging apps ──────────────

    [Fact]
    public async Task HandleAsync_EmailEncodedUrlFromBugReport_ImportsSuccessfully()
    {
        // Actual failing URL from user bug report: email client URL-encoded
        // the entire share text, jamming the fallback into the d= parameter.
        // SMS worked; email/Slack/Discord/Teams need this cleanup.
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        var url = "https://practicingprayerapp.com/share/c?v=2&d=z.H4sIAAAAAAAAE22TQU_cMBCF_8qQc7pCHNqKWxWpSyvRonKgUtuDSSbJaB07HY_ZRoj_3hkHlgVx2o09fvPN8_N9JSQeq_Pq08zk4SOcnZ69r-qK8W_GJKk6_3V_qLlw7HHR3RAFdav6HBnaOM0eBWFE5ykM0HOcwHmvCwwTdtQ6D-2oKxgGTJvqoX6WbJqrBlzooNmmI-Erdgv0qh4zg0fXIacaZHRhB9vYHbaabV1-2zFzO9ZFaT86gSl7odlrb6EYYE_K42PcgacdgkvwjdpdKf8al9JB1ZEYBo55Bgr2Cb1Cv-JlpGE8Ir3eIwpIhIRY0GaOd9SZEaY-Z07ZPm6MwFYuHS_KSB6txQId050SadtQGNTQHISXDRxMaGMQChk7M3vKgWR5SXWd8QipOZRj32MrCWIPc8A8xUCuhp_vTFjiFJnj3thbNYDLfVm7kWZImQd8glDzVw4rc6FFO3Or0Le-_B2dTvB4osxoUdjAC8SbOGH4nU9Pzz4k-IHC6OSI-dIFdcVqoHXMi1mGU7TL0_TszXQp0kdBWg0yy3jVU4HsOyPrc6jNtqDjq9QaDNZA68fjOXoq16TrwEU7TjqmALp2NBgXXg3RlJjB976nVs0SdhRU8a3gGlZc61KJ02NtXXY1JGJoGk036EYSSDMxSbZpHfeO8VVvzelRnwt7W_rORo2yGxj1svUixGm4y5OjgCdwgX42I_ckoy7rfS_mxD9CWVZLOpzVlaQcG_giT9fzHOGkN6K-ecJ-jYa27eytxFhspFRCf1I9_Hn4D1UzDUJMBAAA%0A%0AApril%208%202026%0A+%207%20requests%0A%0A(Shared%20via%20Practicing%20Prayer)";

        await _service.HandleAsync(url);

        // Should import successfully despite the trailing garbage
        await _db.Received(1).InsertAsync(Arg.Is<PrayerCard>(c =>
            c.Title == "April 8 2026" && c.IsImported == true));
        await _db.Received(7).InsertAsync(Arg.Any<Prayer>());
        // Verify no error alert was shown
        await _nav.DidNotReceive().DisplayAlertAsync(
            "Unable to Import", Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAsync_UrlWithTrailingNewlineInPayload_StripsAndImports()
    {
        var sharedCard = new PrayerCard { Id = 10, Title = "Shared with me", IsSystem = true };
        _cardService.GetOrCreateSharedCardAsync().Returns(Task.FromResult(sharedCard));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        // Build a valid payload, then manually append encoded newline + text
        var json = System.Text.Json.JsonSerializer.Serialize(new { title = "Test", notes = "" });
        var uri = BuildCompressedUrl("r", json);
        // Append garbage after the payload (URL-encoded newline + prose)
        uri += "%0A%0ASome%20trailing%20text";

        await _service.HandleAsync(uri);

        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p => p.Title == "Test"));
    }

    [Fact]
    public async Task HandleAsync_UrlWithTrailingParentheses_StripsAndImports()
    {
        var sharedCard = new PrayerCard { Id = 10, Title = "Shared with me", IsSystem = true };
        _cardService.GetOrCreateSharedCardAsync().Returns(Task.FromResult(sharedCard));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        var json = System.Text.Json.JsonSerializer.Serialize(new { title = "Test", notes = "" });
        var uri = BuildCompressedUrl("r", json);
        // Some apps embed the whole "(Shared via Practicing Prayer)" footer
        uri += "%28Shared%20via%20Practicing%20Prayer%29";

        await _service.HandleAsync(uri);

        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p => p.Title == "Test"));
    }

    [Fact]
    public async Task HandleAsync_UrlWithTrailingEmoji_StripsAndImports()
    {
        var sharedCard = new PrayerCard { Id = 10, Title = "Shared with me", IsSystem = true };
        _cardService.GetOrCreateSharedCardAsync().Returns(Task.FromResult(sharedCard));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        var json = System.Text.Json.JsonSerializer.Serialize(new { title = "Test", notes = "" });
        var uri = BuildCompressedUrl("r", json);
        // User reaction emoji appended (Slack/Discord behavior)
        uri += "%F0%9F%99%8F"; // 🙏

        await _service.HandleAsync(uri);

        await _db.Received(1).InsertAsync(Arg.Is<Prayer>(p => p.Title == "Test"));
    }

    // ── Duplicate detection ──────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_CardDuplicate_ShowsAlreadyImportedWarning()
    {
        // Existing imported card with same title and same prayer titles
        var existingCard = new PrayerCard { Id = 42, Title = "Family", IsImported = true, IsSystem = false };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { existingCard }.AsReadOnly());
        _prayerService.GetPrayersByCardAsync(42).Returns(new List<Prayer>
        {
            new() { Id = 1, PrayerCardId = 42, Title = "Mom's health" },
            new() { Id = 2, PrayerCardId = 42, Title = "Dad's job" }
        }.AsReadOnly());
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        // Incoming card with identical data
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            title = "Family",
            requests = new[]
            {
                new { title = "Mom's health", notes = "" },
                new { title = "Dad's job", notes = "" }
            }
        });
        var uri = BuildCompressedUrl("c", json);

        await _service.HandleAsync(uri);

        // Confirm dialog should mention "already imported"
        await _nav.Received(1).DisplayConfirmAsync(
            Arg.Any<string>(),
            Arg.Is<string>(msg => msg.Contains("already imported", StringComparison.OrdinalIgnoreCase)),
            Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAsync_CardDuplicate_UserConfirms_StillImports()
    {
        var existingCard = new PrayerCard { Id = 42, Title = "Family", IsImported = true, IsSystem = false };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { existingCard }.AsReadOnly());
        _prayerService.GetPrayersByCardAsync(42).Returns(new List<Prayer>
        {
            new() { Id = 1, PrayerCardId = 42, Title = "Mom's health" }
        }.AsReadOnly());
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            title = "Family",
            requests = new[] { new { title = "Mom's health", notes = "" } }
        });
        var uri = BuildCompressedUrl("c", json);

        await _service.HandleAsync(uri);

        // Duplicate detected, user still confirmed → import proceeds
        await _db.Received(1).InsertAsync(Arg.Any<PrayerCard>());
    }

    [Fact]
    public async Task HandleAsync_CardSameTitleDifferentPrayers_NoDuplicateWarning()
    {
        // Existing card with same title but different prayer set
        var existingCard = new PrayerCard { Id = 42, Title = "Family", IsImported = true, IsSystem = false };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { existingCard }.AsReadOnly());
        _prayerService.GetPrayersByCardAsync(42).Returns(new List<Prayer>
        {
            new() { Id = 1, PrayerCardId = 42, Title = "Grandma's healing" }
        }.AsReadOnly());
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        // Incoming: same title, different prayers
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            title = "Family",
            requests = new[]
            {
                new { title = "Mom's health", notes = "" },
                new { title = "Dad's job", notes = "" }
            }
        });
        var uri = BuildCompressedUrl("c", json);

        await _service.HandleAsync(uri);

        // No duplicate warning — different prayer set
        await _nav.DidNotReceive().DisplayConfirmAsync(
            Arg.Any<string>(),
            Arg.Is<string>(msg => msg.Contains("already imported", StringComparison.OrdinalIgnoreCase)),
            Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAsync_CardIgnoresSystemCardsForDuplicateCheck()
    {
        // System card "Shared with me" should not trigger false positive
        var systemCard = new PrayerCard { Id = 1, Title = "Shared with me", IsImported = false, IsSystem = true };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { systemCard }.AsReadOnly());
        _db.InsertAsync(Arg.Any<PrayerCard>()).Returns(Task.FromResult(1));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            title = "Shared with me",
            requests = new[] { new { title = "Test", notes = "" } }
        });
        var uri = BuildCompressedUrl("c", json);

        await _service.HandleAsync(uri);

        await _nav.DidNotReceive().DisplayConfirmAsync(
            Arg.Any<string>(),
            Arg.Is<string>(msg => msg.Contains("already imported", StringComparison.OrdinalIgnoreCase)),
            Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAsync_RequestDuplicate_ShowsAlreadySavedWarning()
    {
        var sharedCard = new PrayerCard { Id = 10, Title = "Shared with me", IsSystem = true };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { sharedCard }.AsReadOnly());
        _cardService.GetOrCreateSharedCardAsync().Returns(Task.FromResult(sharedCard));
        _prayerService.GetPrayersByCardAsync(10).Returns(new List<Prayer>
        {
            new() { Id = 1, PrayerCardId = 10, Title = "Mom's surgery", Details = "Please pray" }
        }.AsReadOnly());
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            title = "Mom's surgery",
            notes = "Please pray"
        });
        var uri = BuildCompressedUrl("r", json);

        await _service.HandleAsync(uri);

        await _nav.Received(1).DisplayConfirmAsync(
            Arg.Any<string>(),
            Arg.Is<string>(msg => msg.Contains("already saved", StringComparison.OrdinalIgnoreCase)),
            Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAsync_RequestSameTitleDifferentNotes_NoDuplicateWarning()
    {
        var sharedCard = new PrayerCard { Id = 10, Title = "Shared with me", IsSystem = true };
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { sharedCard }.AsReadOnly());
        _cardService.GetOrCreateSharedCardAsync().Returns(Task.FromResult(sharedCard));
        _prayerService.GetPrayersByCardAsync(10).Returns(new List<Prayer>
        {
            new() { Id = 1, PrayerCardId = 10, Title = "Mom's surgery", Details = "Recovery ongoing" }
        }.AsReadOnly());
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            title = "Mom's surgery",
            notes = "Upcoming surgery next week"
        });
        var uri = BuildCompressedUrl("r", json);

        await _service.HandleAsync(uri);

        await _nav.DidNotReceive().DisplayConfirmAsync(
            Arg.Any<string>(),
            Arg.Is<string>(msg => msg.Contains("already saved", StringComparison.OrdinalIgnoreCase)),
            Arg.Any<string>(), Arg.Any<string>());
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

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
    public void BuildRequestShareText_EncodesSpecialCharacters()
    {
        var prayer = new Prayer { Title = "John & Jane's prayer", Details = "Details with spaces" };

        var result = _service.BuildRequestShareText(prayer);

        // URI portion should be encoded (no raw & or spaces in the URL line)
        var lines = result.Split('\n');
        var uriLine = lines.First(l => l.StartsWith("https://"));
        Assert.DoesNotContain(" ", uriLine);
        // Fallback text should contain the readable title
        Assert.Contains("John & Jane's prayer", result);
    }

    [Fact]
    public void BuildRequestShareText_NullDetails_OmitsNotesParam()
    {
        var prayer = new Prayer { Title = "Simple prayer", Details = null };

        var result = _service.BuildRequestShareText(prayer);

        var lines = result.Split('\n');
        var uriLine = lines.First(l => l.StartsWith("https://"));
        Assert.DoesNotContain("notes=", uriLine);
    }

    // ── BuildCardShareText ───────────────────────────────────────────────────

    [Fact]
    public void BuildCardShareText_IncludesAllActivePrayers()
    {
        var card = new PrayerCard { Title = "Family Prayers" };
        var prayers = new List<Prayer>
        {
            new() { Title = "Mom's health" },
            new() { Title = "Dad's job" }
        };

        var result = _service.BuildCardShareText(card, prayers);

        Assert.Contains("https://practicingprayerapp.com/share/c?", result);
        Assert.Contains("Family Prayers", result);
        Assert.Contains("Mom's health", result);
        Assert.Contains("Dad's job", result);
        Assert.Contains("Shared via Practicing Prayer", result);
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

        var uri = "https://practicingprayerapp.com/share/r?title=Test%20Prayer&notes=Please%20pray";

        await _service.HandleAsync(uri);

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

        await _service.HandleAsync("https://practicingprayerapp.com/share/r?title=Test");

        await _nav.Received(1).GoToAsync(Routes.PrayerCardsTab + "?imported=true");
    }

    [Fact]
    public async Task HandleAsync_Request_InvalidatesCaches()
    {
        var sharedCard = new PrayerCard { Id = 10, Title = "Shared with me", IsSystem = true };
        _cardService.GetOrCreateSharedCardAsync().Returns(Task.FromResult(sharedCard));
        _db.InsertAsync(Arg.Any<Prayer>()).Returns(Task.FromResult(1));

        await _service.HandleAsync("https://practicingprayerapp.com/share/r?title=Test");

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
}

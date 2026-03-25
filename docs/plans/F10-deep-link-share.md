# Plan: F-10 Deep-Link Share

## Status

Deferred — do not implement until app is live in the App Store / Play Store.

**Last updated:** Session 24 (2026-03-24) — Rewritten to use `maui-deep-linking` skill patterns, MAUI lifecycle events, and lessons from notification work.

---

## Context

Users want to share individual prayer requests and prayer cards with others. Recipients who have the app installed get the content auto-saved; recipients without the app see a plain-text fallback in the message.

**Approach: Dual scheme strategy**
- **Custom URI scheme (`prayercards://`)** — works offline, no domain needed, good for initial launch
- **Universal Links / App Links (`https://practicingprayer.app/share/...`)** — future upgrade when we have a domain, better UX (clickable in messages, verified ownership)

Start with custom scheme. Upgrade to https later by adding domain verification without changing the internal routing.

**Decisions from brainstorming:**
- Both cards and individual requests are shareable
- Cards → auto-save silently as a new card with all active requests
- Requests → auto-save into a "Shared with me" system card (same pattern as Quick Add card)
- System card: cannot be deleted, can be hidden via Settings, sorts first
- Share message includes both deep link URI and plain-text fallback
- Move-to-card already implemented (F-17) — no work needed

---

## Architecture

```
Share button tap
  → DeepLinkService.BuildShareText()
  → Share.RequestAsync() (OS share sheet)

Recipient taps link
  → OS launches app with URI
  → MauiProgram ConfigureLifecycleEvents catches URI
  → DeepLinkService.HandleAsync(uri)
  → Creates card/prayer, navigates to it
```

---

## Task 1: DB + Model — Add `IsSystem` to `PrayerCard`

**`PrayerApp/Models/PrayerCard.cs`**
```csharp
[Column("IsSystem")]
public bool IsSystem { get; set; } = false;
```

**`PrayerApp/Services/DBService.cs`** — `UpdateSchema()`
```csharp
try { await _db!.ExecuteAsync("ALTER TABLE PrayerCard ADD COLUMN IsSystem INTEGER DEFAULT 0"); }
catch { /* column already exists */ }
```

**Note:** Quick Add card already uses a `Title`-based lookup. Consider migrating it to use `IsSystem` with a `SystemCardType` enum to distinguish Quick Add vs Shared With Me.

---

## Task 2: `ICardService` + `CardService` — System card support

Follow the existing `GetOrCreateQuickAddCardAsync()` pattern:

```csharp
// ICardService.cs
Task<PrayerCard> GetOrCreateSharedCardAsync();

// CardService.cs
public async Task<PrayerCard> GetOrCreateSharedCardAsync()
{
    var all = await PrayerCard.LoadAllAsync();
    var existing = all.FirstOrDefault(c => c.IsSystem && c.Title == "Shared with me");
    if (existing is not null) return existing;

    var card = new PrayerCard { Title = "Shared with me", IsSystem = true };
    await card.SaveAsync();
    _cache = null;
    return card;
}
```

---

## Task 3: `IDeepLinkService` + `DeepLinkService`

**`PrayerApp/Services/IDeepLinkService.cs`** (new):
```csharp
public interface IDeepLinkService
{
    string BuildRequestShareText(Prayer prayer);
    string BuildCardShareText(PrayerCard card, IEnumerable<Prayer> prayers);
    Task HandleAsync(string uri);
}
```

**`PrayerApp/Services/DeepLinkService.cs`** (new):

**Outbound (sharing):**
- `BuildRequestShareText`: returns `"{deep link}\n\n{prayer.Title}\n{details}\n\n(Shared via Practicing Prayer)"`
- `BuildCardShareText`: returns deep link + plain-text bullet list of prayer titles
- URI format: `prayercards://request?title=<encoded>&notes=<encoded>`
- Card URI: `prayercards://card?title=<encoded>&requests=<base64url-json>`

**Inbound (receiving):**
- `HandleAsync(string uri)`: parse URI, create card/prayer, navigate to it
- `request` host → decode title/notes → `GetOrCreateSharedCardAsync()` → create Prayer → navigate to prayer detail
- `card` host → decode title + request list → create PrayerCard → create Prayers → navigate to card

**Registration in `MauiProgram.cs`:**
```csharp
builder.Services.AddSingleton<IDeepLinkService, DeepLinkService>();
```

---

## Task 4: Platform URI scheme registration

### Android — `MainActivity.cs`

Use `IntentFilter` attribute (per `maui-deep-linking` skill):
```csharp
[IntentFilter(
    new[] { Android.Content.Intent.ActionView },
    Categories = new[] {
        Android.Content.Intent.CategoryDefault,
        Android.Content.Intent.CategoryBrowsable
    },
    DataScheme = "prayercards")]
public class MainActivity : MauiAppCompatActivity { }
```

### iOS — `Info.plist`

```xml
<key>CFBundleURLTypes</key>
<array>
    <dict>
        <key>CFBundleURLName</key>
        <string>com.multithreadedllc.prayercards</string>
        <key>CFBundleURLSchemes</key>
        <array>
            <string>prayercards</string>
        </array>
    </dict>
</array>
```

---

## Task 5: Platform deep-link handlers (MAUI lifecycle events)

**`PrayerApp/MauiProgram.cs`** — use `ConfigureLifecycleEvents` (NOT the old Xamarin `OnNewIntent`/`OpenUrl` approach):

```csharp
builder.ConfigureLifecycleEvents(events =>
{
#if ANDROID
    events.AddAndroid(android =>
    {
        android.OnCreate((activity, _) => HandleAndroidIntent(activity.Intent));
        android.OnNewIntent((activity, intent) => HandleAndroidIntent(intent));
    });
#endif
#if IOS
    events.AddiOS(ios =>
    {
        ios.OpenUrl((app, url, options) =>
        {
            HandleDeepLink(url.ToString());
            return true;
        });
    });
#endif
});

static void HandleAndroidIntent(Android.Content.Intent? intent)
{
    if (intent?.Action != Android.Content.Intent.ActionView || intent.Data is null) return;
    HandleDeepLink(intent.Data.ToString()!);
}

static void HandleDeepLink(string? url)
{
    if (string.IsNullOrEmpty(url) || !url.StartsWith("prayercards://")) return;
    MainThread.BeginInvokeOnMainThread(async () =>
    {
        await App.InitTask; // Ensure DB is ready
        var svc = IPlatformApplication.Current!.Services.GetRequiredService<IDeepLinkService>();
        await svc.HandleAsync(url);
    });
}
```

**Key differences from original plan:**
- Handles both cold launch (`OnCreate`) and warm launch (`OnNewIntent`) on Android
- Uses `ConfigureLifecycleEvents` instead of overriding platform methods directly
- Awaits `App.InitTask` before processing (same pattern as notification tap handler)
- All processing on `MainThread` for navigation safety

---

## Task 6: Share buttons on UI

### Prayer request sharing (update existing)

**`PrayerApp/ViewModels/PrayerRequestDetailViewModel.cs`**
- Inject `IDeepLinkService` via service locator
- Update `ShareAsync()` to use `_deepLinkService.BuildRequestShareText(_prayer)`

### Card sharing (new)

**`PrayerApp/ViewModels/PrayerCardViewModel.cs`**
- Add `ShareCommand` → loads active prayers, calls `BuildCardShareText`, opens share sheet
- Add `IsSystem` / `IsNotSystem` properties

**`PrayerApp/Views/PrayerCard/PrayerCardPage.xaml`**
- Add Share button to action grid (3-column: Delete | Share | Save)
- Hide Delete when `IsSystem`

**`PrayerApp/Views/PrayerCard/PrayerCardsPage.xaml`**
- Add Share SwipeItem to left-swipe group

---

## Task 7: System card display rules

**`PrayerApp/ViewModels/PrayerCardsViewModel.cs`** — `ApplySorting()`
- System cards sort first: `.OrderByDescending(c => c.IsSystem).ThenByDescending(c => c.IsFavorite).ThenBy(c => c.Title)`
- Filter out when `Settings.HideSharedWithMe` is true

---

## Task 8: Settings — Hide "Shared with me"

**`PrayerApp/Services/Settings.cs`**
```csharp
public static bool HideSharedWithMe
{
    get => Preferences.Get(nameof(HideSharedWithMe), false);
    set => Preferences.Set(nameof(HideSharedWithMe), value);
}
```

**`PrayerApp/Views/Settings.xaml`** — Add toggle row matching existing notification pattern.

---

## Task 9: Tests

New test files:
- `PrayerApp.Tests/Services/DeepLinkServiceTests.cs`
  - `BuildRequestShareText_IncludesUriAndFallback`
  - `BuildRequestShareText_EncodesSpecialCharacters`
  - `BuildCardShareText_IncludesAllActivePrayers`
  - `HandleAsync_Request_CreatesInSharedCard`
  - `HandleAsync_Card_CreatesCardWithPrayers`
  - `HandleAsync_InvalidUri_NoOp`

Update `PrayerApp.Tests.csproj` with `<Compile Include>` for `DeepLinkService.cs` and `IDeepLinkService.cs`.

---

## Future: Upgrade to Universal Links / App Links

When we have a domain (e.g., `practicingprayer.app`):

1. **Android:** Add `IntentFilter` with `DataScheme = "https"`, `DataHost = "practicingprayer.app"`, `AutoVerify = true`. Host `/.well-known/assetlinks.json` with app SHA-256.
2. **iOS:** Add `applinks:practicingprayer.app` to `Entitlements.plist`. Host `/.well-known/apple-app-site-association` with team ID + bundle ID.
3. **Both:** Keep custom `prayercards://` scheme as fallback. `HandleDeepLink` routes both schemes through the same `DeepLinkService.HandleAsync`.
4. **iOS limitation:** Universal Links must be tested on a physical device (not simulator). AASA changes can take 24h to propagate via Apple's CDN.

This is additive — no breaking changes to the custom scheme path.

---

## Removed from Plan

- **Move-to-card (was Task 9):** Already implemented as F-17 via card picker on prayer edit form.

---

## Verification

```bash
# Android
adb shell am start -W -a android.intent.action.VIEW \
  -d "prayercards://request?title=Test&notes=Hello" com.multithreadedllc.prayercards

# iOS (custom scheme test via Safari)
# Type prayercards://request?title=Test in Safari address bar
```

**Manual checks:**
1. Share a prayer → share sheet shows deep link + plain text
2. Share a card → share sheet includes all active prayers
3. Open a shared request link → "Shared with me" card created, prayer inside
4. Open a shared card link → new card created with all prayers
5. Cold launch via link → app starts, DB inits, content saved + navigated
6. Settings toggle hides/shows "Shared with me" card
7. System card cannot be deleted

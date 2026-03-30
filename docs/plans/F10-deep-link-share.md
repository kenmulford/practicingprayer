# Plan: F-10 Deep-Link Share

## Status

**Ready to implement — Phase 1 (iOS).** App is live in both stores (1.0.6). Website infrastructure deployed.

**Last updated:** 2026-03-30 — Phased approach: iOS Universal Links first, Android App Links later. All shared code (schema, DeepLinkService, UI, outbound sharing) is cross-platform. Implementation plan at `.claude/plans/wild-gathering-meadow.md`.

### Phased Approach

| Phase | Scope | Status |
|-------|-------|--------|
| Phase 1 | Schema, DeepLinkService, UI, iOS Universal Links, Settings, tests | **Ready** |
| Phase 2 | Android `IntentFilter` + `AutoVerify` + lifecycle handler (~15 min) | Deferred |

On Android during Phase 1: outbound sharing works, inbound links fall through to website fallback page.

---

## Context

Users want to share individual prayer requests and prayer cards with others. Recipients who have the app installed get the content auto-saved; recipients without the app see a plain-text fallback in the message.

**Approach: Universal Links / App Links directly**
- **`https://practicingprayerapp.com/share/...`** — domain owned, AASA + assetlinks.json deployed, website fallback page live
- No custom URI scheme needed — go straight to verified https links

**Decisions from brainstorming + Ken's review (2026-03-29):**
- Both cards and individual requests are shareable
- **System cards (Quick Add, Shared with me) cannot be shared** — share buttons hidden/disabled
- Cards → auto-save as a new card with `IsImported=true` and all active requests (also `IsImported=true`)
- Requests → auto-save into a "Shared with me" system card with `IsImported=true`
- Imported cards/prayers show a cloud-download icon (FontAwesome Free `cloud-arrow-down` SVG)
- **Card import landing:** navigate to cards list with the newly imported card expanded
- System card: cannot be deleted, can be hidden via Settings, sorts first
- Share message includes both deep link URI and plain-text fallback
- Tags are NOT shared (personal organization)
- Notifications are NOT shared (`CanNotify=false`, defaults on import)
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

## Task 1a: DB + Model — `IsSystem` on `PrayerCard` — ALREADY DONE

`PrayerCard.IsSystem` already exists. Quick Add card uses it. The "Shared with me" card will be a second system card distinguished by title (matches existing Quick Add pattern).

## Task 1b: DB + Model — Add `IsImported` to `PrayerCard` and `Prayer`

**`PrayerApp/Models/PrayerCard.cs`**
```csharp
[Column("IsImported")]
public bool IsImported { get; set; } = false;
```

**`PrayerApp/Models/Prayer.cs`**
```csharp
[Column("IsImported")]
public bool IsImported { get; set; } = false;
```

**`PrayerApp/Services/DBService.cs`** — `UpdateSchema()`
```csharp
try { await _db!.ExecuteAsync("ALTER TABLE PrayerCard ADD COLUMN IsImported INTEGER DEFAULT 0"); }
catch { /* column already exists */ }
try { await _db!.ExecuteAsync("ALTER TABLE PrayerRequest ADD COLUMN IsImported INTEGER DEFAULT 0"); }
catch { /* column already exists */ }
```

## Task 1c: Imported icon asset

**Icon:** FontAwesome Free `cloud-arrow-down` SVG (provided by Ken). Save as `Resources/Images/imported.svg`.

**Display locations:**
- **PrayerCardsPage accordion header** — to the left of the active prayer count badge, only when `IsImported=true`
- **PrayerListPage prayer row** — inline with prayer title when the prayer's `IsImported=true`
- **PrayerDetailPage view mode** — near the title when viewing an imported prayer

**Styling:** Use `AppThemeBinding` with existing color tokens for light/dark theming. Suggested: `Primary` in light mode, `PrimaryDark` in dark mode (matches the warm journal aesthetic).

**Spacing:** Ensure the icon doesn't collide with card title text or count badge. Use `Margin` or `Spacing` in the header Grid.

---

## Task 2: `ICardService` + `CardService` — Shared card support

Follow the existing `GetOrCreateQuickAddCardAsync()` pattern:

```csharp
// ICardService.cs
Task<PrayerCard> GetOrCreateSharedCardAsync();

// CardService.cs
public async Task<PrayerCard> GetOrCreateSharedCardAsync()
{
    var cards = await GetCardsAsync();
    var existing = cards.FirstOrDefault(c => c.IsSystem && c.Title == "Shared with me");
    if (existing is not null) return existing;

    var card = new PrayerCard { Title = "Shared with me", IsSystem = true };
    await card.SaveAsync();
    _cache = null;
    return card;
}
```

**Note:** Uses `GetCardsAsync()` (cached) instead of `PrayerCard.LoadAllAsync()` (uncached) — matches current codebase pattern.

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
- `HandleAsync(string uri)`: parse URI, create card/prayer with `IsImported=true`, navigate
- `request` host → decode title/notes → `GetOrCreateSharedCardAsync()` → create Prayer (`IsImported=true`, `CanNotify=false`) → navigate to prayer detail
- `card` host → decode title + request list → create PrayerCard (`IsImported=true`) → create Prayers (`IsImported=true`, `CanNotify=false`) → navigate to cards list with new card expanded

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

### Sharing restrictions
- **System cards cannot be shared** (Quick Add, Shared with me) — hide share buttons when `IsSystem=true`
- Prayers inside system cards CAN be shared individually (just not the card itself)

### Prayer request sharing (update existing)

**`PrayerApp/ViewModels/PrayerRequestDetailViewModel.cs`**
- Inject `IDeepLinkService` via service locator
- Update `ShareAsync()` to use `_deepLinkService.BuildRequestShareText(_prayer)`

### Card sharing (new)

**`PrayerApp/ViewModels/PrayerCardViewModel.cs`**
- Add `ShareCommand` → loads active prayers, calls `BuildCardShareText`, opens share sheet
- `ShareCommand` disabled/hidden when `IsSystem=true`

**`PrayerApp/Views/PrayerCard/PrayerCardPage.xaml`**
- Add Share button to action grid — hidden when `IsSystem`
- Delete also hidden when `IsSystem` (already the case)

**`PrayerApp/Views/PrayerCard/PrayerCardsPage.xaml`**
- Add Share SwipeItem to left-swipe group — hidden when `IsSystem`

---

## Task 7: System card display rules — ALREADY DONE (partially)

**`PrayerCardsViewModel.ApplySorting()`** already sorts system cards first:
```csharp
.OrderByDescending(pc => pc.IsSystem)
.ThenByDescending(pc => pc.IsFavorite)
.ThenBy(pc => pc.Title)
```

**Remaining work:** Add filter to hide "Shared with me" when `Settings.HideSharedWithMe` is true. Add this to `ApplyFilter()`:
```csharp
if (_settings.HideSharedWithMe)
    result = result.Where(c => c.Title != "Shared with me" || !c.IsSystem);
```

**Note:** `PrayerCardsViewModel` currently doesn't inject `ISettings`. Would need to add it (same pattern as `ITagService` was added for F-19).

---

## Task 8: Settings — Hide "Shared with me"

**`PrayerApp/Services/Settings.cs`** + **`ISettings.cs`**
```csharp
public bool HideSharedWithMe
{
    get => Preferences.Get(nameof(HideSharedWithMe), false);
    set => Preferences.Set(nameof(HideSharedWithMe), value);
}
```

**`PrayerApp/Views/Settings/AppSettingsPage.xaml`** — Add toggle row in the App Settings sub-page (not the old monolithic Settings page, which was replaced by the Settings hub in UX-14).

**Note:** Only show this toggle if the "Shared with me" card exists (no point showing it before any shares are received). Use a `HasSharedCard` property on the SettingsViewModel.

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

## Website Configuration — DONE (2026-03-29)

The following files have been created in the `prayerapp-site` repo (`practicingprayerapp.com`):

| File | Purpose | Status |
|------|---------|--------|
| `public/.well-known/apple-app-site-association` | iOS Universal Links verification | Created |
| `public/.well-known/assetlinks.json` | Android App Links verification | Created |
| `public/_headers` | Ensures `.well-known` files served as `application/json` | Created |
| `public/_redirects` | Routes `/share/*` sub-paths to fallback page | Created |
| `src/app/share/page.tsx` | Fallback page for users without the app | Created |

**Share fallback page:** Shows app icon, "A prayer was shared with you" message, App Store + Google Play download badges, and "Learn more" link. Uses existing site theme variables — verified working in both light and dark mode.

### Pre-implementation deployment checklist

Before implementing F-10 app-side code, verify these files are live in production:

- [ ] Deploy site: `cd prayerapp-site && wrangler deploy`
- [ ] Verify AASA: `curl -I https://practicingprayerapp.com/.well-known/apple-app-site-association` — should return `200` with `Content-Type: application/json`
- [ ] Verify Asset Links: `curl -I https://practicingprayerapp.com/.well-known/assetlinks.json` — should return `200` with `Content-Type: application/json`
- [ ] Verify share fallback: visit `https://practicingprayerapp.com/share` — should show download page
- [ ] Verify sub-path redirect: visit `https://practicingprayerapp.com/share/r/test` — should show same download page
- [ ] Apple AASA validation: https://search.developer.apple.com/appsearch-validation-tool/ (can take up to 24h to propagate)
- [ ] Google Asset Links validation: https://developers.google.com/digital-asset-links/tools/generator

---

## Implementation Note: Go Straight to Universal Links

Since the domain and `.well-known` files are already deployed, **skip the custom `prayercards://` scheme entirely.** Use `https://practicingprayerapp.com/share/...` links from day one.

**App-side changes for https links (replaces Tasks 4 + 5 custom scheme approach):**
1. **Android:** `IntentFilter` with `DataScheme = "https"`, `DataHost = "practicingprayerapp.com"`, `DataPathPrefix = "/share"`, `AutoVerify = true`
2. **iOS:** Add `applinks:practicingprayerapp.com` to `Entitlements.plist`
3. **DeepLinkService:** Parse `https://practicingprayerapp.com/share/r/...` and `https://practicingprayerapp.com/share/c/...` URIs
4. **Lifecycle handlers:** Match on `practicingprayerapp.com` host instead of `prayercards://` scheme

**iOS note:** Universal Links must be tested on a physical device (not simulator). AASA changes can take up to 24h to propagate via Apple's CDN.

---

## Design Notes (added 2026-03-29)

### Tags and shared content
- **Shared prayers do NOT carry tags.** Tags are personal organization — the sender's tags are meaningless to the recipient. Shared prayers arrive untagged.
- The recipient can tag shared prayers after receiving them (same as any other prayer).
- The F-19 tag chip filter on PrayerCardsPage will naturally work with the "Shared with me" card — if the recipient tags a shared prayer, the card appears when that tag is selected.

### Notifications and shared content
- **Shared prayers arrive with `CanNotify=false`.** The recipient can enable notifications manually.
- Notification reconciliation on app launch (`ReconcileNotificationsAsync`) ensures shared prayers don't create orphan notifications.

### Navigation after receiving a share
- Use `Routes.PrayerCardsTab` and `Routes.PrayersTab` constants (added in this session) instead of hardcoded strings.
- After creating the shared prayer/card, navigate to it using existing Shell navigation patterns.

### Card creation via `CreateCardViewModel`
- `PrayerCardsViewModel` now uses `CreateCardViewModel(PrayerCard)` factory (added for F-19 testability). The shared card should be added via this factory to maintain consistency.

### Home page metrics
- F-20 home page metrics (Active Cards, Unanswered Prayers) will automatically include the "Shared with me" card if it has active prayers. No special handling needed.

---

## Removed from Plan

- **Move-to-card (was Task 9):** Already implemented as F-17 via card picker on prayer edit form.
- **Tags in shared data:** Tags are personal organization and should not be included in shared links.
- **Notification settings in shared data:** Shared prayers arrive with CanNotify=false — recipient controls their own reminders.

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
3. Share button NOT visible on Quick Add or Shared With Me cards
4. Open a shared request link → "Shared with me" card created, prayer inside, `IsImported=true`
5. Open a shared card link → new card created with `IsImported=true`, lands on cards list with card expanded
6. Imported icon (cloud-arrow-down) visible on imported cards and prayers
7. Imported prayers have `CanNotify=false` (no notification scheduled)
8. Cold launch via link → app starts, DB inits, content saved + navigated
9. Settings toggle hides/shows "Shared with me" card
10. System card cannot be deleted

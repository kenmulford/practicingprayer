# Prayer App — Backlog

> **How to use this file**: Read it at the start of each session to understand what's pending.
> Update it as work is completed. Do NOT implement Research Required items without a
> dedicated planning conversation with the user first.

---

## Status Key

| Symbol | Meaning |
|--------|---------|
| 🔴 | Immediate — blocking or high-friction bug/debt |
| 🟡 | Short-term — next feature or cleanup sprint |
| 🟢 | Medium-term — planned, not urgent |
| 🔵 | Research Required — discuss with user before any code |
| ✅ | Done (kept for reference) |

---

## 🔴 Immediate / Tech Debt

### TD-1 Fix broken tag binding on `PrayerDetailPage`
`PrayerDetailPage.xaml` has a "Tags" section whose `CollectionView` is bound to
`TagSelectionViewModel.AllTags`, but the page's `BindingContext` is
`PrayerRequestDetailViewModel` — which has no `TagSelectionViewModel`. This is a
silent binding failure at runtime.

**Decision**: Tags on individual Prayer requests are not yet the designed behavior
(tags belong to Prayer Cards). Remove the tags section from `PrayerDetailPage.xaml`
until the feature is properly scoped.

**File**: `Views/Prayer/PrayerDetailPage.xaml`

---

### TD-2 `PrayerRequestTag` → `PrayerCardTag` rename

The junction table and model are named `PrayerRequestTag` / `PrayerRequestId`, but tags
belong to **Prayer Cards**, not individual requests. The naming misleads future readers.

**Changes required:**

| Current | Rename to |
|---------|-----------|
| `Models/PrayerRequestTag.cs` | `Models/PrayerCardTag.cs` |
| `class PrayerRequestTag` | `class PrayerCardTag` |
| `[Table("PrayerRequestTag")]` | `[Table("PrayerCardTag")]` |
| Column `PrayerRequestId` | `PrayerCardId` |
| `ITagService.GetTagsByRequestIdAsync` | `GetTagsByCardIdAsync` |
| `ITagService.AddTagToRequestAsync` | `AddTagToCardAsync` |
| `ITagService.RemoveTagFromRequestAsync` | `RemoveTagFromCardAsync` |
| `TagService` method implementations | match interface rename |
| `IDBService.GetByRequestIdAsync` | `GetByCardIdAsync` |
| `IDBService.DeleteByRequestIdAsync` | `DeleteByCardIdAsync` |
| `PrayerTagSelectionViewModel.InitializeForCardAsync(prayerRequestId)` | param → `prayerCardId` |

**DB migration** (add to `DBService.UpdateSchema()`):
```sql
ALTER TABLE PrayerRequestTag RENAME TO PrayerCardTag;
-- If column rename is needed on older SQLite: recreate table + migrate data
```

---

### TD-3 `PrayerCardDetailViewModel` is orphaned

`PrayerCardDetailViewModel` is registered as `Transient` in `MauiProgram.cs` but no
page's `BindingContext` is set to it. `PrayerCardPage.xaml` uses `PrayerCardViewModel`.

**Options (discuss before implementing):**
- Consolidate `CanNotify`, `PrayerFrequency`, `IsAnswered`, `FrequencyOptions` from
  `PrayerCardDetailViewModel` into `PrayerCardViewModel`, then delete the orphan.
- Or: wire `PrayerCardDetailViewModel` to the edit card page if those fields are needed.

**Files**: `ViewModels/PrayerCardDetailViewModel.cs`, `MauiProgram.cs`

---

### TD-4 `PrayerInteraction` not in DB schema

`PrayerInteraction.cs` and `PrayerInteractionService.cs` exist but the table is not
created in `DBService`. This means interaction records (used by Prayer Time to log
sessions) are never persisted.

**Changes** (`Services/DBService.cs`):
- Add `await _db.CreateTableAsync<PrayerInteraction>()` in the schema creation block
- Add `await _db.CreateTableAsync<PrayerInteraction>()` in `UpdateSchema()` (guarded)
- Wire `PrayerInteractionService` save/query methods to `DBService`

**Files**: `Services/DBService.cs`, `Services/IDBService.cs`, `Services/PrayerInteractionService.cs`

---

### TD-5 Prayer Time — hardcoded 30-second auto interval

Auto mode is currently hardcoded to 30 seconds (`const int AutoIntervalSeconds = 30`).
The plan specified 30s / 1min / 2min selectable via toolbar.

**Changes** (`ViewModels/PrayerTimeViewModel.cs`, `Views/PrayerTime/PrayerTimePage.xaml`):
- Add `SelectIntervalCommand` with options [30, 60, 120]
- Show selected interval in toolbar / Auto button text
- Persist last-used interval to `Settings`

---

### TD-6 Legacy `Prayer.cs` model file

`Models/Prayer.cs` still exists alongside `PrayerCard.cs`. Verify whether `Prayer.cs`
is still the "prayer request" model (individual items within a card) or is truly dead.
If alive and intentional, rename to `PrayerRequest.cs` for clarity. If dead, delete it.

> ⚠️ **Confirm before deleting** — the `PrayerListPage` and `PrayerListViewModel` may
> still reference `Prayer` directly.

---

## 🟡 Short-term Features

### F-1 Tag creation and management UI

No UI exists to create, edit, or delete `PrayerTag` records. `ITagService.SaveTagAsync`
and `DeleteTagAsync` are implemented but unreachable from the app.

**Needs:**
- A "Tags" settings/management screen (list, add, edit, delete tags with optional color)
- Wire tag creation into `PrayerCardPage` (assign tags to a card while editing it)
- Replace the broken tag binding in `PrayerDetailPage` (TD-1) once scoped correctly

**Files**: New `Views/Tags/TagsPage.xaml`, `ViewModels/TagsViewModel.cs`; update `AppShell.xaml`

---

### F-2 Tag filtering on Prayer Cards / Prayer Time

Once tag management UI is in place:
- Add filter bar to `PrayerCardsPage` to show only cards with selected tags
- `PrayerTimeScopePage` already has the UI; verify `PrayerTimeViewModel` correctly
  filters by tag IDs when launched with `scope=tags`

---

### F-3 `PrayerDetailPage` — view/edit mode overhaul

Current page is a basic form. Target design:
- **View mode**: large title, details body, answered badge (if answered), frequency badge,
  action buttons: "Set Reminder" | "Edit" | "Mark Answered"
- **Edit mode**: title entry, details editor, frequency picker, CanNotify switch,
  IsAnswered toggle (auto-sets `AnsweredAt`), Save / Delete buttons
- `MarkAnsweredCommand`: single-tap sets `IsAnswered = true`, `AnsweredAt = DateTime.Now`, saves

**Files**: `Views/Prayer/PrayerDetailPage.xaml`, `ViewModels/PrayerRequestDetailViewModel.cs`

---

### F-4 `PrayerCardPage` — expose full card fields

Currently shows only title. After TD-3 is resolved (consolidate `PrayerCardDetailViewModel`),
expose: title, frequency picker, CanNotify switch, IsAnswered toggle, list of this
card's prayer requests inline.

**Files**: `Views/PrayerCard/PrayerCardPage.xaml`, `ViewModels/PrayerCardViewModel.cs`

---

### F-5 Notification scheduling

`PrayerFrequency` is the reminder cadence; `CanNotify` is the per-request toggle;
`Settings.AllowNotifications` is the global toggle. The infrastructure is in place
but no scheduling is wired up.

**Needs** (`Services/NotificationService.cs`):
- `ScheduleForPrayer(Prayer prayer)` — creates a local notification using `PrayerFrequency`
  as the repeat cadence
- Tapping the notification deep-links to the specific prayer request's card
  (requires `OnNewIntent` override on Android; `UNUserNotificationCenterDelegate` on iOS)

---

### F-6 Unit test project

No automated test project exists. The app targets mobile only (`net10.0-android`,
`net10.0-ios`), so a separate class library is required for testable logic.

**Plan (discuss before implementing):**
- Add a `PrayerApp.Tests` project (xUnit, `net10.0` or `net9.0`; no MAUI target needed)
- Extract pure-logic ViewModels / Services into a `PrayerApp.Core` class library that
  both the app and tests reference — OR — just mock platform dependencies in the test project
- Start with the most complex / risky ViewModels: `PrayerTimeViewModel` (timer logic),
  `PrayerCardViewModel` (save/delete flow), `PrayerListViewModel` (sorting/filtering)
- CI: GitHub Actions workflow running `dotnet test` on push

**Key architectural decision**: Core library extraction vs. direct mocking.
Both are valid; core library is cleaner long-term but requires refactoring the project structure.

---

### F-7 Home page personalization

User would like the home page to show the device owner's name (e.g. "Good morning, Ken").

**Research needed:**
- **Android**: `AccountManager` can query Google accounts but requires permissions and
  may be restricted on modern Android (API 26+ deprecations). `Settings.Secure.getString`
  for `BLUETOOTH_NAME` or `device_name` are unreliable. Best practical option: a
  one-time "What's your name?" prompt on first launch stored in `Settings`.
- **iOS**: No API to get the device owner's name without privacy permission prompts.
  Same practical recommendation: ask on first launch.
- **Decision**: Prompt for name on first run → store in `Settings.UserName` → display
  on Home page. This is the only cross-platform option that doesn't require intrusive permissions.

**Files**: `Views/MainPage.xaml`, `Services/Settings.cs`, possibly a new `FirstRunPage`

---

## 🟢 Medium-term / Planned

### M-1 Prayer Time — last-prayed notifications

Driven by `PrayerInteraction` records. After TD-4 (DB schema) is done:
- Calculate "days since last prayed" per request from `PrayerInteraction` table
- Schedule a push notification: "You haven't prayed for [name] in X days"
- Configurable threshold (default: 7 days) in Settings

**Do NOT implement until TD-4 is complete and notification scheduling (F-5) is working.**

---

### M-2 Prayer Time — swipe-past-last-card completion

When the user swipes past the last card in Prayer Time, show a completion screen:
"You've prayed through all your requests!" with a "Finish" button. Currently the
session just stops at the last card.

**File**: `Views/PrayerTime/PrayerTimePage.xaml`, `ViewModels/PrayerTimeViewModel.cs`
(`HasCompleted` property already exists — needs UI wired to it)

---

### M-3 Seed initial tags in DBService

`DBService` has a commented-out stub for seeding tags. Once the tag taxonomy is
decided (what default tags make sense for a prayer app), uncomment and populate.

---

### M-4 Prayer statistics / analytics

- Count of prayers prayed (from `PrayerInteraction`)
- Streak tracking (consecutive days of Prayer Time)
- Answered prayer count / percentage
- Display on Home page or a dedicated Stats tab

---

## 🔵 Research Required — Do NOT implement without planning session

### BL-1 Bible Verse Integration

**Goal**: Associate a Bible verse or passage with a prayer request. Display inline.

**Research summary (completed):**

| Option | Pros | Cons |
|--------|------|------|
| **ESV API** (api.esv.org) | High quality, clean JSON | Requires Crossway **written permission** for public app distribution; free tier is personal use only |
| **API.Bible** (scripture.api.bible) | 2,000+ versions, ABS-backed, free tier | API key required; rate limits on free tier; some versions restricted |
| **GetBible** (getbible.net) | No API key, fully open, self-hostable JSON | Public domain translations only (KJV, WEB, ASV, NET) |
| **Bible Gateway deep link** | Zero integration cost, always up to date | Launches browser or WebView; not inline; no verse text stored locally |

**Recommended path** (discuss before choosing):
1. **GetBible** for zero-friction offline-cacheable approach with public domain texts
2. **API.Bible** if translation breadth matters (NIV, ESV, etc.)
3. **Bible Gateway link** if you want "look it up" rather than in-app display

**Key decisions needed:**
- Which API / approach?
- Store verse text locally on `Prayer` record after first fetch (recommended, works offline after) or fetch on-demand?
- UI: single verse reference field? Passage range? Just a hyperlink?
- What happens when offline at time of verse lookup?

**Model change if storing locally**: Add `VerseReference` (string) + `VerseText` (string) to `Prayer.cs`.

---

### BL-2 Offline Architecture

**Goal**: Prevent crashes if/when network features (Bible verse API) are added.

**Current state**: App is 100% offline — SQLite + local notifications. No network calls.
No crash risk from connectivity at this time.

**Trigger point**: Becomes relevant the moment BL-1 or any cloud feature ships.

**Options to discuss:**
- Add `IConnectivity` guard (MAUI built-in via `Microsoft.Maui.Networking`) before any
  HTTP call — lightweight, show user-friendly message instead of crashing
- `HttpClient` + `Polly` retry policy for resilient network calls
- Offline-first: always cache fetched content locally (verse text → `Prayer` record)

**Recommendation**: `IConnectivity` guard + local caching is sufficient for a personal
devotional app. Full retry logic (Polly) is overkill unless the API is unreliable.

**`Microsoft.Extensions.Http`** is already in the csproj — HTTP client infrastructure ready.

---

### BL-3 App Store Publishing

**Goal**: Publish to Google Play Store and Apple App Store.

**Research summary (completed):**

#### Google Play Store
- **Cost**: One-time $25 developer account registration
- **Build format**: Android App Bundle (`.aab`) — NOT `.apk` for Play Store
- **Signing**: Generate a release keystore (`.jks`) — **back it up — losing it means
  you can never update the app**. Google Play App Signing recommended (upload key ≠ final key)
- **Target API**: Must target Android 14 (API 34) or higher for new apps
- **Content rating**: IARC questionnaire — prayer app likely "Everyone"
- **Privacy policy**: Required if any user data is collected; local-only data = minimal requirements but still recommended
- **Store listing**: Screenshots (phone + 7" tablet), feature graphic (1024×500),
  short description (80 chars), full description (4000 chars), category (Lifestyle or Reference)
- **Review time**: Typically 1–3 days for new apps

#### Apple App Store
- **Cost**: $99/year Apple Developer Program membership
- **Requirement**: A Mac is required for the final iOS build and submission
- **`PrivacyInfo.xcprivacy`**: Required manifest declaring APIs used — **mandatory or Apple will reject the app**
- **Provisioning**: Distribution certificate + provisioning profile via Apple Developer portal
- **`Info.plist`**: `NSUserNotificationsUsageDescription` required (app uses local notifications)
- **Screenshots**: Required for iPhone 6.7", 6.5", 5.5" (iPad optional if not supported)
- **TestFlight**: Strongly recommended for UAT before App Store release
- **Review time**: Typically 24–48 hours; stricter than Play Store

#### What needs to be built before publishing:
- [ ] Release build configuration (currently debug-only)
- [ ] Android: release keystore + signing config in `.csproj`
- [ ] iOS: Apple Developer enrollment + provisioning
- [ ] `PrivacyInfo.xcprivacy` (iOS — blocks App Store submission without it)
- [ ] App icon: all required sizes (MAUI generates from single SVG if configured ✓ — verify adaptive icon for Android)
- [ ] Privacy policy page/URL (required by both stores)
- [ ] App ID / bundle ID finalized (`com.multithreadedllc.prayerapp` — already set ✓)
- [ ] Version + build number management strategy
- [ ] TestFlight / internal testing track

**Key decisions needed:**
1. Target both stores simultaneously or Android first?
2. Privacy policy — who hosts it? Simple GitHub Pages URL is acceptable.
3. Where to store the Android release keystore safely?
4. Do you have an Apple Developer account already?

---

## ✅ Completed (Reference)

| Item | Notes |
|------|-------|
| Phase 2: 3×5 card visual (`PrayerCardsPage`) | SwipeView + header/rule/requests layout |
| Phase 2: Answered prayer rendering | Strikethrough + muted color + answered date |
| Phase 3: Prayer Time page + orientation lock | Landscape forced, swipe/arrow navigation |
| Phase 3: Auto-mode countdown timer | 30s timer, pause on background, resume on foreground |
| Phase 3: Tag-scoped Prayer Time | `PrayerTimeScopePage` + `PrayerTimeViewModel` filter |
| `BoolToMutedColorConverter` — resource-aware colors | Resolves from `Application.Current.Resources`; no hardcoded hex |
| `PrayerCardBorder` named style | Secondary bg, Primary 1.5px stroke, 8px radius — applied to all card borders |
| `Microsoft.Maui.Controls` → `10.0.41` | Required by `CommunityToolkit.Maui 14.0.1` |
| `Shell.Current.Navigation.PushModalAsync` fix | `PushModalAsync` removed in MAUI 10.0.41 |
| `ICardService` write methods + cache invalidation | `SaveCardAsync`, `DeleteCardAsync`, `InvalidateCache` |
| `IPrayerService` / `PrayerService` | Singleton; caches per-card + all-prayers queries |
| `ITagService` write methods | `SaveTagAsync`, `DeleteTagAsync` |
| `Prayer.AnsweredAt` | `DateTime?` column; set on `IsAnswered = true` |
| `PrayerListViewModel` sort | Fixed Z→A → A→Z |
| Platforms cleanup | Removed `Windows/`, `MacCatalyst/`, `Tizen/` folders; csproj clean |
| Dead package target cleanup | Removed net9 + Windows conditional PropertyGroups |
| `PrayerInteractionService` | Created; DB schema hookup still pending (TD-4) |
| Quick Add modal | `BtnQuickAdd` wired → `QuickAddPage` modal |
| `QuickAddPage` | Card picker + prayer title form |

---

*Last updated: 2026-03-09*

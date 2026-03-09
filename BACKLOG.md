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

### TD-2 `PrayerRequestTag` → `PrayerCardTag` rename

Tags belong to **Prayer Cards**, not individual prayer requests. The junction table,
model, and service method names all still say `Request`, which misleads future readers.

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
```

---

### TD-5 Prayer Time — interval selection UI

Auto mode is hardcoded to 30 seconds (`const int AutoIntervalSeconds = 30`).
The original plan specified user-selectable intervals: 30s / 1 min / 2 min.

**Changes:**
- `ViewModels/PrayerTimeViewModel.cs`: Add `SelectIntervalCommand` with options [30, 60, 120];
  replace `const` with a settable property; persist last-used interval to `Settings`
- `Views/PrayerTime/PrayerTimePage.xaml`: Add interval selector to toolbar or auto-mode button area

---

## 🟡 Short-term Features

### F-1 Tag creation and management UI

No UI exists to create, edit, or delete `PrayerTag` records. `ITagService.SaveTagAsync`
and `DeleteTagAsync` are implemented but unreachable from the app. No tag assignment UI
on `PrayerCardPage` either.

**Needs:**
- A "Tags" management screen (list, add, edit, delete tags with optional color picker)
- Wire tag assignment into `PrayerCardPage` (assign/remove tags from a card while editing)
- Navigate to it from Settings or the Cards tab toolbar

**Files**: New `Views/Tags/TagsPage.xaml`, `ViewModels/TagsViewModel.cs`; update `AppShell.xaml`

---

### F-2 Tag filtering on Prayer Cards page

`PrayerTimeViewModel` already filters by tag IDs when launched with `scope=tags`.
What's missing is the equivalent filter UI on `PrayerCardsPage`.

**Needs:**
- Filter bar / chip row at the top of `PrayerCardsPage` to show only cards with selected tags
- Bind to a filter state in `PrayerCardsViewModel`

---

### F-5 Notification scheduling

`PrayerFrequency` is the reminder cadence; `CanNotify` is the per-prayer toggle;
`Settings.AllowNotifications` is the global toggle. Infrastructure exists but no
scheduling is implemented — `NotificationService` only has `RequestPermissionAsync`,
`AreNotificationsEnabledAsync`, and `ClearAllAsync`.

**Needs** (`Services/NotificationService.cs`):
- `ScheduleForPrayer(Prayer prayer)` — creates a local notification using `PrayerFrequency`
  as the repeat cadence
- Deep-link on tap: `OnNewIntent` override (Android `MainActivity.cs`),
  `UNUserNotificationCenterDelegate` (iOS `AppDelegate.cs`)

---

### F-6 Unit test project

No automated test project exists. Mobile-only targets (`net10.0-android`, `net10.0-ios`)
mean tests must live in a separate project.

**Plan (discuss approach before implementing):**
- Add `PrayerApp.Tests` (xUnit, `net10.0` target — no MAUI needed)
- **Option A**: Extract `PrayerApp.Core` class library for pure-logic ViewModels/Services;
  both the app and test project reference Core. Cleaner long-term, requires refactoring.
- **Option B**: Keep single app project; mock platform dependencies via interfaces.
  Less refactoring, slightly messier test setup.
- Start with highest-value test targets: `PrayerTimeViewModel` (timer state machine),
  `PrayerCardViewModel` (save/delete/cache), `PrayerListViewModel` (sort/filter)
- CI: GitHub Actions `dotnet test` on push

---

### F-7 Home page personalization

`MainPage.xaml` already has a greeting label showing the literal text `"Hello, {User}!"`
(the `{User}` is hardcoded, not a binding). `Settings` has no `UserName` property.

**Plan**: One-time first-launch prompt ("What's your name?") → store in `Settings.UserName`
→ bind greeting on `MainPage`. This is the only cross-platform approach that avoids
privacy permission friction (neither Android nor iOS exposes the device owner's name
via a clean public API in 2026).

**Files**: `Views/MainPage.xaml`, `Services/Settings.cs`, possibly a new `FirstRunPage`

---

## 🟢 Medium-term / Planned

### M-1 Prayer Time — last-prayed notifications

`PrayerInteraction` records are now logged. Next step: use them to drive
"you haven't prayed for [name] in X days" push notifications.

**Needs** (after F-5 notification scheduling is done):
- `PrayerInteractionService` method: calculate days since last interaction per prayer
- Schedule a notification per prayer when threshold is exceeded (default: 7 days)
- Configurable threshold in Settings

---

### M-4 Prayer statistics

No stats surface exists. `PrayerInteraction` data is now being written, enabling:
- Count of prayers prayed (total interactions)
- Streak tracking (consecutive days of Prayer Time)
- Answered prayer count / percentage
- Display on Home page or dedicated Stats tab

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
- Store verse text locally on `Prayer` record after first fetch (recommended — works offline after)?
- UI: single verse reference field? Passage range? Just a hyperlink?
- What happens when offline at time of verse lookup?

**Model change if storing locally**: Add `VerseReference` (string) + `VerseText` (string) to `Prayer.cs`.

**`Microsoft.Extensions.Http`** is already in the csproj — HTTP infrastructure ready.

---

### BL-2 Offline Architecture

**Goal**: Prevent crashes if/when network features (Bible verse API) are added.

**Current state**: App is 100% offline — SQLite + local notifications. No network calls.
No crash risk from connectivity at this time.

**Trigger point**: Becomes relevant the moment BL-1 or any cloud feature ships.

**Options to discuss:**
- `IConnectivity` guard (MAUI built-in via `Microsoft.Maui.Networking`) before any
  HTTP call — lightweight, user-friendly message instead of crashing
- `HttpClient` + `Polly` retry policy for resilient network calls
- Offline-first: always cache fetched content locally (verse text → `Prayer` record)

**Recommendation**: `IConnectivity` guard + local caching is sufficient for a personal
devotional app. `Polly` is overkill unless the API is unreliable.

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
- **Privacy policy**: Required; local-only data = minimal requirements but still needed
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
- [ ] App icon: verify adaptive icon for Android (MAUI generates from SVG ✓ — confirm adaptive layers)
- [ ] Privacy policy page/URL (required by both stores)
- [ ] App ID / bundle ID finalized (`com.multithreadedllc.prayerapp` — already set ✓)
- [ ] Version + build number management strategy
- [ ] TestFlight / internal testing track setup

**Key decisions needed:**
1. Target both stores simultaneously or Android first?
2. Privacy policy — who hosts it? (Simple GitHub Pages URL is acceptable)
3. Where to store the Android release keystore safely?
4. Do you have an Apple Developer account already?

---

## ✅ Completed (Reference)

| Item | Notes |
|------|-------|
| TD-1 Broken tag binding on PrayerDetailPage | Tags section removed from PrayerDetailPage — issue is gone |
| TD-3 PrayerCardDetailViewModel orphaned | ViewModel was never created; no orphan exists |
| TD-4 PrayerInteraction not in DB schema | `CreateTableAsync<PrayerInteraction>()` is in DBService constructor and UpdateSchema |
| TD-6 Legacy Prayer.cs cleanup | Prayer.cs is the active prayer-request model — not dead code; no action needed |
| F-3 PrayerDetailPage view/edit overhaul | Separate VIEW/EDIT mode sections; MarkAnsweredCommand; AnsweredAtDisplay; Set Reminder placeholder |
| F-4 PrayerCardPage full card fields | Frequency picker, CanNotify switch, IsAnswered toggle all present |
| M-2 Prayer Time completion screen | "You've prayed through all your requests!" overlay wired to HasCompleted |
| M-3 Seed initial tags | Urgent / Family / Work tags seeded in DBService.SeedDataAsync |
| Phase 2: 3×5 card visual (PrayerCardsPage) | SwipeView + header/rule/requests layout |
| Phase 2: Answered prayer rendering | Strikethrough + muted color + answered date |
| Phase 3: Prayer Time page + orientation lock | Landscape forced, swipe/arrow navigation |
| Phase 3: Auto-mode countdown timer | 30s timer, pause on background, resume on foreground |
| Phase 3: Tag-scoped Prayer Time | PrayerTimeScopePage + PrayerTimeViewModel filter by tagIds |
| BoolToMutedColorConverter resource-aware | Resolves from Application.Current.Resources; fallback hex |
| PrayerCardBorder named style | Secondary bg, Primary 1.5px stroke, 8px radius — applied to all card borders |
| Microsoft.Maui.Controls → 10.0.41 | Required by CommunityToolkit.Maui 14.0.1 |
| Shell.Current.Navigation.PushModalAsync fix | PushModalAsync removed in MAUI 10.0.41 |
| ICardService write methods + cache invalidation | SaveCardAsync, DeleteCardAsync, InvalidateCache |
| IPrayerService / PrayerService | Singleton; caches per-card + all-prayers queries |
| ITagService write methods | SaveTagAsync, DeleteTagAsync |
| Prayer.AnsweredAt | DateTime? column; set on IsAnswered = true |
| PrayerListViewModel sort | Fixed Z→A → A→Z |
| Platforms cleanup | Removed Windows/, MacCatalyst/, Tizen/ folders; csproj clean |
| PrayerInteractionService | LogInteractionAsync implemented |
| Quick Add modal | BtnQuickAdd wired → QuickAddPage modal with card picker |

---

*Last updated: 2026-03-09*

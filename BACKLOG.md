# Prayer App — Backlog

> **Session start checklist:**
> 1. Read `CONTEXT.md` (architecture, conventions, gotchas)
> 2. Read the **Currently In Progress** section below
> 3. Pick up the next `[ ]` item in the Priority Queue
>
> Update this file when starting, finishing, or adding work.

---

## Currently In Progress

> ✏️ _Update this section at the start and end of every session._

**Status**: 🔨 Starting F-10 — Share feature (`feature/f10-share`)
**Last completed**: F-9 (comprehensive UI review — 10 named styles, accessibility, bug fixes). Merged via PR #9.
**Next up after this**: TD-5 — Prayer Time interval selection UI.

---

## Priority Queue

Items are listed in work order. Start at the top, work down.

| # | ID | Item | Notes |
|---|-----|------|-------|
| 1 | F-9 | Comprehensive UI review | Fonts, controls, animations consistency |
| 2 | F-10 | Share feature | Share prayer request in-app or via SMS/email |
| 3 | TD-5 | Prayer Time — interval selection UI | 30s / 1min / 2min picker |
| 4 | F-1 | Tag management UI | Create / edit / delete tags; assign to cards |
| 5 | F-2 | Tag filtering on Prayer Cards page | Filter chips on PrayerCardsPage |
| 6 | F-5 | Notification scheduling | `ScheduleForPrayer()` + deep-link on tap |
| 7 | M-1 | Last-prayed notifications | Days-since calculation + push notification |
| 8 | M-4 | Prayer statistics | Streak, totals, answered %, on Home or Stats tab |
| 9 | BL-1 | Bible verse integration | Research done — needs planning conversation |
| 10 | BL-2 | Offline architecture | Needs planning conversation |
| 11 | BL-3 | App Store publishing | Needs planning conversation |
| 12 | F-8 | Onboarding — splash, welcome, tutorial | Design separately; no blocking dependencies |

---

## Detailed Descriptions

### F-7 Home page personalization
`Views/MainPage.xaml` shows literal text `"Hello, {User}!"` — not a binding.
`Settings` has no `UserName` property.

**Plan**: One-time first-launch prompt → store in `Settings.UserName` → bind greeting
on `MainPage`. Neither Android nor iOS exposes the device owner's name via a clean
public API, so asking on first run is the only cross-platform option.

**Files**: `Views/MainPage.xaml`, `Services/Settings.cs`, possibly a new `FirstRunPage`

---

### F-8 Onboarding — splash screen, welcome screen, brief tutorial
Industry-standard first-run experience. Covers:
- Animated splash screen (beyond the static MAUI splash)
- Welcome / value-prop screen shown once after first launch
- Brief in-app tutorial (swipeable cards or tooltip overlay) explaining
  Prayer Cards, Prayer Time, and Quick Add
- "Get Started" CTA that marks `Settings.OnboardingComplete = true`

**Research needed before implementing:**
- MAUI splash screen customisation options (animated Lottie vs. static SVG)
- Tutorial pattern: overlay tooltips vs. swipeable onboarding pages
- Skip / replay tutorial from Settings

**Files**: New `Views/Onboarding/` pages; `Services/Settings.cs` (`OnboardingComplete` flag);
possibly `Resources/` for animation assets

---

### F-9 Comprehensive UI review
Audit and standardise the full UI:
- **Typography**: Consistent font sizes and weights across all pages (currently mixed)
- **Control styles**: Standardise `Button`, `Entry`, `Picker`, `Switch` appearances;
  add named styles to `Styles.xaml` for reuse
- **Spacing & layout**: Review padding/margin consistency across pages
- **Transitions**: Add navigation transitions and micro-animations (tap feedback, list
  load, card expand/collapse) — MAUI `Shell` transitions + `Animation` API
- **Accessibility**: `SemanticProperties.Description` on interactive elements;
  adequate tap target sizes

**Approach**: Page-by-page audit as a dedicated sprint; each page gets a GitHub Issue
with before/after screenshots.

---

### F-10 Share feature
Allow users to share a prayer request two ways:

**Option A — App deep-link share**
Share a URL that opens the specific prayer request inside the app if installed,
or routes to the relevant App Store listing if not.
- Requires a custom URI scheme (e.g. `prayerapp://prayer/{id}`)
- Android: `Intent` with custom scheme; iOS: Universal Links or custom URL scheme
- Requires App Store presence for the fallback — coordinate with BL-3

**Option B — Text share**
Share the title + details of a prayer request as plain text via the OS share sheet
(SMS, email, clipboard, etc.).
- MAUI: `Share.RequestAsync(new ShareTextRequest { Title = ..., Text = ... })`
- Cross-platform, no App Store dependency, can ship independently of Option A

**Recommended order**: Ship Option B first (trivial, works today), then Option A after
App Store publishing (BL-3) is underway.

**Files**: `Views/Prayer/PrayerDetailPage.xaml` (share button), `ViewModels/PrayerRequestDetailViewModel.cs` (`ShareCommand`)

---

### TD-5 Prayer Time — interval selection UI
Auto mode is hardcoded to 30 seconds (`const int AutoIntervalSeconds = 30`).

**Changes:**
- `ViewModels/PrayerTimeViewModel.cs`: Add `SelectedIntervalSeconds` (int property),
  `IntervalOptions` (list: 30, 60, 120), `SelectIntervalCommand`; persist last-used
  value to `Settings.AutoModeInterval`
- `Views/PrayerTime/PrayerTimePage.xaml`: Interval picker in toolbar or as tap target
  on the countdown display

---

### F-1 Tag management UI
`ITagService.SaveTagAsync` and `DeleteTagAsync` are implemented but unreachable.
No UI to create, edit, delete, or assign tags.

**Needs:**
- `Views/Tags/TagsPage.xaml` + `ViewModels/TagsViewModel.cs` — list with add/edit/delete
- Tag color picker (use a small palette, not a full color wheel)
- Wire tag assignment into `PrayerCardPage` (chip row with `PrayerTagSelectionViewModel`)
- Register route and add entry point (Settings menu or Cards toolbar)

---

### F-2 Tag filtering on Prayer Cards page
`PrayerTimeViewModel` already filters by tag IDs when `scope=tags`. Missing: filter UI
on `PrayerCardsPage`.

**Needs:**
- Horizontal chip row at top of `PrayerCardsPage` listing all tags
- Tapping a chip toggles it; `PrayerCardsViewModel` filters `AllPrayerCards` to show
  only cards associated with selected tags (via `ITagService.GetPrayerIdsByTagIdsAsync`)

---

### F-5 Notification scheduling
`NotificationService` has `RequestPermissionAsync`, `AreNotificationsEnabledAsync`,
and `ClearAllAsync` — but no scheduling.

**Needs:**
- `ScheduleForPrayer(Prayer prayer)` method using `PrayerFrequency` as repeat cadence
- Deep-link on tap: `OnNewIntent` override in `Platforms/Android/MainActivity.cs`;
  `UNUserNotificationCenterDelegate` in `Platforms/iOS/AppDelegate.cs`

---

### M-1 Last-prayed notifications
Depends on F-5 (notification scheduling) being done first.

**Needs:**
- `PrayerInteractionService` method: days since last `PrayerInteraction` per prayer
- Schedule a notification when threshold exceeded (default 7 days, configurable in Settings)

---

### M-4 Prayer statistics
No stats surface exists. `PrayerInteraction` data is being written, enabling:
- Total prayers prayed, streak tracking, answered %, per-card counts
- Display on Home page or a new Stats tab

---

### BL-1 Bible Verse Integration ⚠️ Plan before implementing

**Research complete.** Options ranked:

| Option | Cost | Requires |
|--------|------|---------|
| GetBible (getbible.net) | Free, no key | Public domain only (KJV, WEB, ASV) |
| API.Bible (scripture.api.bible) | Free tier | API key, rate limits |
| ESV API (api.esv.org) | Free tier | Crossway **written permission** for public app distribution |
| Bible Gateway deep link | Zero | Opens browser/WebView; no inline text |

**Recommended**: GetBible for zero-friction; API.Bible if non-public-domain translations needed.

**Decisions needed (discuss before any code):**
- Which API?
- Store verse text on `Prayer` record after first fetch (offline-friendly)?
- UI: reference field, passage range, or just a link?

**Model change if storing**: Add `VerseReference (string)` + `VerseText (string)` to `Prayer.cs`

---

### BL-2 Offline Architecture ⚠️ Plan before implementing

Currently 100% offline. No risk until BL-1 or other network feature ships.

**Options to discuss:**
- `IConnectivity` guard (MAUI built-in) before HTTP calls — show friendly message
- Local cache (store verse text on `Prayer` record) — works offline after first fetch
- `Polly` retry — overkill for a personal devotional app

`Microsoft.Extensions.Http` already in csproj. Infrastructure ready.

---

### BL-3 App Store Publishing ⚠️ Plan before implementing

**Research complete.**

#### Google Play
- $25 one-time fee · AAB format (not APK) · Keystore backup is critical
- Target API 34+ · IARC content rating · Privacy policy required
- Review: 1–3 days

#### Apple App Store
- $99/year · **Mac required** for final build · `PrivacyInfo.xcprivacy` mandatory
- `NSUserNotificationsUsageDescription` in `Info.plist` (app uses local notifications)
- TestFlight recommended before release · Review: 24–48 hrs

#### Pre-publishing checklist
- [ ] Release build config + signing
- [ ] Android: release keystore (`.jks`) — **never lose this file**
- [ ] iOS: Apple Developer enrollment + provisioning profile
- [ ] `PrivacyInfo.xcprivacy` for iOS
- [ ] Adaptive icon verified (Android)
- [ ] Privacy policy hosted (GitHub Pages is fine)
- [ ] Version + build number strategy decided

**Decisions needed:** Both stores simultaneously or Android first? Apple Developer account?

---

## ✅ Completed

| ID | Item | PR | Notes |
|----|------|----|-------|
| TD-1 | Broken tag binding on PrayerDetailPage | #4/5 | Tags section removed — issue gone |
| TD-2 | `PrayerRequestTag` → `PrayerCardTag` rename | #7 | All layers updated; DB migration in UpdateSchema() |
| TD-3 | PrayerCardDetailViewModel orphaned | — | Never existed; no action needed |
| TD-4 | PrayerInteraction not in DB schema | — | CreateTableAsync in constructor + UpdateSchema |
| TD-6 | Legacy Prayer.cs cleanup | — | Prayer.cs is active (prayer request model); no action |
| F-3 | PrayerDetailPage view/edit overhaul | #4 | VIEW/EDIT modes, MarkAnsweredCommand, AnsweredAtDisplay |
| F-4 | PrayerCardPage full card fields | #4 | Frequency, CanNotify, IsAnswered all present |
| F-6 | Unit test project | #7 | 40 tests, GitHub Actions CI on push/PR |
| M-2 | Prayer Time completion screen | #4 | HasCompleted overlay wired |
| M-3 | Seed initial tags | — | Urgent/Family/Work seeded in DBService |
| Phase 3 | Prayer Time page + auto-mode | #4/5/6 | Landscape, swipe/arrow, 30s auto-timer, background pause |
| Phase 2 | Card 3×5 visual + answered prayer rendering | #4/5 | SwipeView, strikethrough, muted color, answered date |
| — | BoolToMutedColorConverter resource-aware | #4/5/6 | Resolves from Application.Current.Resources |
| — | PrayerCardBorder named style | #4/5/6 | Applied to all 5 card border sites |
| — | Shell.Current.Navigation.PushModalAsync fix | #5/6 | PushModalAsync removed in MAUI 10.0.41 |
| — | Microsoft.Maui.Controls → 10.0.41 | #5/6 | Required by CommunityToolkit.Maui 14.0.1 |
| — | Platforms cleanup (Windows/Mac/Tizen removed) | — | csproj clean, Android + iOS only |
| F-7 | Home page personalization | #8 | One-time name prompt, time-of-day greeting |
| F-9 | Comprehensive UI review | #9 | 10 named styles (ButtonBase, LabelBase + 8 variants), inline duplication eliminated, SemanticProperties on all form inputs, Settings bugs fixed |

---

*Last updated: 2026-03-09*

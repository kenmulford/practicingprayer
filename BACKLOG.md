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

**Status**: Idle
**Last completed**: Session 9 — BUG-23 (Gray700 XamlParseException on Prayers tab); build bumped to 11
**Next up**: F-13 (iOS native field styling)

---

## Priority Queue

Items are listed in work order. Start at the top, work down.

| # | ID | Item | Notes |
|---|-----|------|-------|
| 1 | F-13 | iOS native field styling | Full plan at `docs/plans/F13-ios-field-styling.md`. OnPlatform backgrounds + Focused VSM state using app palette. Logged from Ken (Round 2) |
| 3 | F-10 | Deep-link share — create card/request via tapped link | Deferred until app is in the store — full plan at `docs/plans/F10-deep-link-share.md` |
| 5 | TD-8 | Refactor ViewModels to use constructor injection instead of `IPlatformApplication.Current!.Services` | All ViewModels resolve services at runtime via the MAUI DI host, making them impossible to unit test. Switching to constructor injection unlocks ViewModel tests |
| 8 | TD-10 | Fix XC0022/XC0023 compiled binding warnings in PrayerDetailPage.xaml and QuickAddPage.xaml | Add `x:DataType` to untyped binding scopes (lines 133, 247, 248, 250 in PrayerDetailPage.xaml; line 28 in QuickAddPage.xaml). Low risk, performance improvement. |
| 7 | INV-4 | In-app update notification — Android Play Core flexible update flow | **Approach decided: Android-only, Play Core flexible update (no server/JSON).** Full implementation written on branch `feature/inv4-android-update`. Research notes at `docs/research/INV-4-android-update-research.md`. **Blocked:** `Xamarin.Google.Android.Play.App.Update 2.1.0.18` conflicts with MAUI 10.0.41 AndroidX pin (`Lifecycle.LiveData 2.9.x`); Play Core binding requires `>= 2.10.x`. Resume when MAUI bumps its AndroidX floor or a compatible binding ships. |

---

## Detailed Descriptions

### TD-7 Extract ILocalNotificationCenter

`NotificationService.ScheduleAsync` and `CancelAsync` call `LocalNotificationCenter.Current` directly — a static singleton from `Plugin.LocalNotification`. This is not mockable, so `NotificationService` has no unit tests.

**Fix:** Introduce a thin `ILocalNotificationCenter` interface:
```csharp
public interface ILocalNotificationCenter
{
    Task Show(NotificationRequest request);
    void Cancel(params int[] notificationIds);
    void CancelAll();
}
```
Register a production wrapper in `MauiProgram.cs` that delegates to `LocalNotificationCenter.Current`. Inject it into `NotificationService` via constructor.

**Files likely involved:**
`Services/ILocalNotificationCenter.cs` (new), `Services/LocalNotificationCenterWrapper.cs` (new),
`Services/NotificationService.cs`, `MauiProgram.cs`,
`PrayerApp.Tests/Services/NotificationServiceTests.cs` (new)

---

### TD-8 Refactor ViewModels to constructor injection

All ViewModels resolve services via `IPlatformApplication.Current!.Services.GetRequiredService<>()` inside the default constructor. This couples them to the MAUI runtime — they cannot be instantiated in a test context.

**Fix:** Add constructor overloads (or replace the default constructor) that accept injected services. Register ViewModels with DI in `MauiProgram.cs` and update XAML pages to resolve them from the container rather than using `<viewModels:XViewModel />` in XAML.

**Affects all ViewModels:** `PrayerRequestDetailViewModel`, `PrayerCardViewModel`, `HomeViewModel`, `PrayerTimeViewModel`, `TagDetailViewModel`, `SettingsViewModel`

**Files likely involved:**
`MauiProgram.cs`, all ViewModel `.cs` files, all page `.xaml` and `.xaml.cs` files

**Unlocks:** Full ViewModel unit test coverage including notification scheduling hooks, mark-answered flow, onboarding state transitions

---

### F-10 Deep-link share

> **Full implementation plan:** [`docs/plans/F10-deep-link-share.md`](docs/plans/F10-deep-link-share.md)
> **Deferred until app is live in the App Store / Play Store** — the `prayercards://` URI scheme must be registered and live before links work for recipients.

Both cards and individual requests are shareable. Share sheet sends a `prayercards://` deep link alongside a plain-text fallback (for recipients without the app).

**Recipient behavior:**
- Shared card → auto-saved silently as a new card with all active requests
- Shared request → auto-saved into a "Shared with me" system card (top of list, cannot be deleted, hideable in Settings)

**Sub-features (all designed, details in plan doc):**
1. `IDeepLinkService` / `DeepLinkService` — build URIs and handle incoming links
2. `prayercards://` URI scheme registration (Android intent filter + iOS CFBundleURLTypes)
3. Platform handlers: `MainActivity.OnNewIntent` + `AppDelegate.OpenUrl`
4. "Shared with me" system card (`PrayerCard.IsSystem` column + `GetOrCreateSystemCardAsync`)
5. Share button on card edit page + swipe action on card list
6. Move-to-card action on prayer request edit page
7. Settings toggle to hide "Shared with me" card

---

### F-11 iCloud / Google Drive backup

User-initiated export of the SQLite database (or a JSON snapshot) to cloud storage, with matching import/restore flow.

**Primary use case:** Transfer data to a new device without going through a store update.
**Secondary use case:** Manual backup / peace of mind.

**Platform options:**
- Android: Google Drive API (`com.google.android.gms:play-services-drive`) or `StorageAccessFramework` file picker → user picks Drive location
- iOS: `NSFileManager` + iCloud `ubiquityContainerIdentifier` or Files app integration
- Cross-platform fallback: export to a `.json` file → OS share sheet → user saves wherever they want

**Files likely involved:**
New `Services/BackupService.cs` (`IBackupService`), `Views/Settings/SettingsPage.xaml` (export/import buttons), `Platforms/Android/` and `Platforms/iOS/` for platform-specific cloud auth

**Decisions needed before building:**
- Automatic scheduled backup vs. manual only?
- Full SQLite file copy or structured JSON export?
- Android: Drive API (requires Google sign-in) vs. SAF file picker (no sign-in)?
- iOS: iCloud Drive vs. Files app integration?

---

## ✅ Completed

| ID | Item | PR | Notes |
|----|------|----|-------|
| TD-1 | Broken tag binding on PrayerDetailPage | #4/5 | Tags section removed |
| TD-2 | `PrayerRequestTag` → `PrayerCardTag` rename | #7 | All layers updated |
| TD-3 | PrayerCardDetailViewModel orphaned | — | Never existed |
| TD-4 | PrayerInteraction not in DB schema | — | CreateTableAsync added |
| TD-6 | Legacy Prayer.cs cleanup | — | No action needed |
| F-3 | PrayerDetailPage view/edit overhaul | #4 | VIEW/EDIT modes, MarkAnsweredCommand |
| F-4 | PrayerCardPage full card fields | #4 | Frequency, CanNotify, IsAnswered |
| F-6 | Unit test project | #7 | 40 tests, GitHub Actions CI |
| F-7 | Home page personalization | #8 | One-time name prompt, time-of-day greeting |
| F-8 | Onboarding — splash, welcome, tutorial | — | In-app tutorial banners, welcome/complete popups |
| F-9 | Comprehensive UI review | #9 | 10 named styles, SemanticProperties, Settings fixes |
| M-2 | Prayer Time completion screen | #4 | HasCompleted overlay |
| M-3 | Seed initial tags | — | Urgent/Family/Work seeded |
| Phase 3 | Prayer Time page + auto-mode | #4/5/6 | Landscape, swipe, 30s timer |
| Phase 2 | Card 3×5 visual + answered rendering | #4/5 | SwipeView, strikethrough, muted color |
| BUG-1 | Post-save view not refreshing | #10 | ApplyQueryAttributes reload |
| BUG-2 | Prayer Time — blank card content | #10 | CurrentEntry PropertyChanged fix |
| BUG-3 | Prayer card accordion title stale after save | — | Navigation depth fix: `../..` for existing prayer edit |
| BUG-4 | Final Prayer Time card unreachable | — | Removed `IsEnabled="{Binding HasNext}"` from → button |
| BUG-5 | Tutorial text says "tap checkmark"; UI says "I'm Done" | — | HeadlineText copy corrected |
| BUG-6 | ObservableCollection crash on Add Card (API 36) | — | Reentrancy guard `_isSorting` flag in ApplySorting |
| BUG-7 | Tag color picker clips last swatch (closed test — Tony) | 5f36cd5 | HorizontalScrollView added around swatch row |
| BUG-8 | Backup fails immediately on tap (closed test — Todd) | 5f36cd5 | `ExecuteAsync` → `ExecuteScalarAsync<int>` for WAL checkpoint pragma |
| BUG-9 | Notification permission prompt only fires on first Settings visit | d125486 | `EnsureNotificationPermissionRequested()` called on CanNotify toggle in both ViewModels |
| BUG-10 | Settings "Go" button text clipped on some devices | d125486 | Layout reworked to `Auto` label column |
| BUG-11 | Keyboard covers prayer request entry buttons | d125486 | `android:windowSoftInputMode="adjustResize"` in AndroidManifest |
| BUG-12 | iOS orientation lock broken — Prayer Time doesn't go landscape | — | Replaced broken `UIDevice.SetValueForKey` with `UIWindowScene.RequestGeometryUpdate` + `AppDelegate.GetSupportedInterfaceOrientations` |
| BUG-13 | Prayer Time by tag shows wrong prayer count | — | Filter compared card IDs against prayer IDs; fixed to use `p.PrayerCardId` |
| BUG-14 | "All done" re-enters Prayer Time / requires second tap | — | Reset `HasCompleted = false` at start of `LoadEntriesAsync`; BUG-13 fix also eliminates empty-set trigger |
| BUG-15 | Card delete leaves prayers orphaned; no delete in view mode | — | Cascade delete in `PrayerCardViewModel.DeleteAsync`; added Delete button to prayer view mode |
| BUG-16 | Switch thumb insufficient contrast in dark mode | — | Off-state thumb `Dark` value raised from `Gray500` to `Gray200` |
| F-11 | iCloud / Google Drive backup | — | BackupService with Share.RequestAsync export + FilePicker import; already on dev |
| INV-1 | Audit: notifications cancelled on mark-answered? | — | Scheduling was unimplemented; added `ScheduleAsync`/`CancelAsync` to `INotificationService`; hooked into SaveAsync and MarkAnsweredAsync |
| INV-2 | Audit: add/edit prayer card shows reminder UI? | — | Confirmed leftover; removed Reminders toggle + Frequency picker from PrayerCardPage and PrayerCardViewModel |
| INV-3 | Portrait lock everywhere except Prayer Time | — | Added `LockPortrait()` to `IOrientationService`; PrayerTimePage restores portrait on exit; Android `[Activity]` + iOS startup lock |
| UX-3 | Card list — dividers between rows | #10 | BoxView DividerLine in BindableLayout |
| — | Dark mode contrast audit | — | 7 files fixed; version bumped to 1.0.1 |
| — | App renamed to "Prayer Cards" | — | ApplicationTitle + ApplicationId updated |
| — | App renamed to "Practicing Prayer" | — | ApplicationTitle, BackupService filename, NotificationService title, AppShell, Info.plist, website, onboarding popup all updated |
| — | iOS TestFlight — build 1.0.4 (build 7) | — | Provisioning profile "Practicing Prayer" (App Store Distribution), Entitlements.plist added, xcode-select fixed, submitted via Transporter |
| BUG-17 | Prayer request title pre-populated on new form | — | `Prayer.cs` default changed from "Prayer Request" to `string.Empty`; auto-focus added to TitleEntry on new request |
| BUG-19 | iOS Info.plist missing NSLocation* purpose strings | — | Keys already present from prior session; no code change needed |
| BUG-20 | Home screen containers low contrast in dark mode | — | Two `{StaticResource Tertiary}` labels in MainPage.xaml switched to `AppThemeBinding` (Tertiary light / White dark) |
| BUG-18 | Prayer Time timer visible on "all done" end state | — | Added `IsVisible` InverseBool binding to Row 0 header Grid in PrayerTimePage.xaml |
| BUG-21 | Tag data model — tags stored at card level instead of request level | — | Added `PrayerRequestId` column to `PrayerCardTag`; new request-level service methods; data migration on startup; `PrayerRequestDetailViewModel` and `PrayerTimeViewModel` updated |
| F-12 | Prayer list page UX overhaul | — | Live search (title + card name + tag name), 3-way status toggle (Active/Answered/All), tag chip filter; `PrayerListViewModel` full rewrite; 55 tests passing |
| BUG-22 | iOS AOT crash on launch (build 8) — SQLite-net module out of date | — | Root cause: iOS linker trimming SQLite-net internals, making AOT module stale after Mac workload update. Fix: `Platforms/iOS/LinkerConfig.xml` with `preserve="all"` for SQLite-net + SQLitePCLRaw; wired via `TrimmerRootDescriptor` in csproj. Clean rebuild (rm -rf bin/obj) required. Shipped as build 10. |
| BUG-23 | Prayers tab crash — `XamlParseException: StaticResource not found for key Gray700` | — | PrayerListPage.xaml (F-12 work) referenced `Gray700` in 3 toggle-button `AppThemeBinding` Dark values. `Gray700` was never defined in Colors.xaml (palette goes Gray600 → Gray900). Fixed: all 3 replaced with `Gray600` (`#404040`). Build bumped to 11. |
| TD-9 | Dark mode color audit | — | Scanned all XAML files. 2 confirmed contrast failures (Primary text on dark bg ≈ 2.85–3.18:1), 1 borderline (SuccessGreen 4.08:1). Most hits intentionally fixed colors. Fixes tracked as TD-11. Full notes at `docs/research/TD-9-dark-mode-color-audit.md`. |
| TD-7 | Extract `ILocalNotificationCenter` | — | Created `ILocalNotificationCenter` + `NotifyRepeat` enum; `LocalNotificationCenterWrapper` delegates to Plugin static. `NotificationService` now injects both — zero MAUI/Plugin dependencies in the class itself. `Settings.AllowNotifications` lambda supplied in `MauiProgram.cs`. 11 new tests; 66/66 passing. |
| TD-11 | Dark-mode contrast fixes (from TD-9 audit) | — | `PrayerCardsPage.xaml:151` and `Settings.xaml:80`: `Primary` → `AppThemeBinding Light=Primary, Dark=PrimaryDark`. `PrayerDetailPage.xaml:39`: `SuccessGreen` → `AppThemeBinding Light=SuccessGreen, Dark=SuccessGreenDark`. Added `SuccessGreenDark #66BB6A` to Colors.xaml. |

---

*Last updated: 2026-03-17 (session 10 — TD-9/TD-11/TD-7 done; 66/66 tests; pausing before TD-8)*

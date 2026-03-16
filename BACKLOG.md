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
**Last completed**: INV-1/2/3 — notification scheduling, card reminder UI cleanup, portrait lock
**Next up**: F-10 deep-link share

---

## Priority Queue

Items are listed in work order. Start at the top, work down.

| # | ID | Item | Notes |
|---|-----|------|-------|
| 1 | F-10 | Deep-link share — create card/request via tapped link | Custom URI scheme; recipient opens app (or store if not installed) and lands on pre-filled create flow |
| 2 | TD-7 | Extract `ILocalNotificationCenter` to make `NotificationService` unit-testable | `NotificationService` calls `LocalNotificationCenter.Current` (static). Wrap it behind an injectable interface so tests can mock scheduling without a device |
| 3 | TD-8 | Refactor ViewModels to use constructor injection instead of `IPlatformApplication.Current!.Services` | All ViewModels resolve services at runtime via the MAUI DI host, making them impossible to unit test. Switching to constructor injection unlocks ViewModel tests |

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

Allow users to share a prayer card or request as a deep link. Recipient taps the link and:
- If the app is installed → opens directly into a pre-filled "Create Card" or "Create Request" flow with the shared title/details pre-populated
- If the app is not installed → routes to the Play Store / App Store listing

**Platform mechanics:**
- Android: custom URI scheme (`prayercards://`) via `Intent` filter in `AndroidManifest.xml`; handled in `MainActivity.OnNewIntent`
- iOS: custom URL scheme in `Info.plist`; handled in `AppDelegate.OpenUrl`
- MAUI bridge: `App.OnAppLinkRequestReceived` or a registered `IAppLinkEntry`

**Share payload**: URL-encoded query params, e.g.
`prayercards://create?type=request&title=...&details=...`

**Files likely involved:**
`Platforms/Android/MainActivity.cs`, `Platforms/iOS/AppDelegate.cs`,
`App.xaml.cs` (deep-link routing), `Views/Prayer/PrayerDetailPage.xaml` (share button),
`ViewModels/PrayerRequestDetailViewModel.cs` (`ShareDeepLinkCommand`),
possibly a new `DeepLinkService`

**Decisions needed before building:**
- URI scheme name (e.g. `prayercards://`)
- Should the link pre-fill and prompt user to save, or auto-save silently?
- Card share vs. request share vs. both?

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
| F-11 | iCloud / Google Drive backup | — | BackupService with Share.RequestAsync export + FilePicker import; already on dev |
| INV-1 | Audit: notifications cancelled on mark-answered? | — | Scheduling was unimplemented; added `ScheduleAsync`/`CancelAsync` to `INotificationService`; hooked into SaveAsync and MarkAnsweredAsync |
| INV-2 | Audit: add/edit prayer card shows reminder UI? | — | Confirmed leftover; removed Reminders toggle + Frequency picker from PrayerCardPage and PrayerCardViewModel |
| INV-3 | Portrait lock everywhere except Prayer Time | — | Added `LockPortrait()` to `IOrientationService`; PrayerTimePage restores portrait on exit; Android `[Activity]` + iOS startup lock |
| UX-3 | Card list — dividers between rows | #10 | BoxView DividerLine in BindableLayout |
| — | Dark mode contrast audit | — | 7 files fixed; version bumped to 1.0.1 |
| — | App renamed to "Prayer Cards" | — | ApplicationTitle + ApplicationId updated |

---

*Last updated: 2026-03-16 (session 2)*

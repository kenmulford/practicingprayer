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
**Last completed**: Session 13 — BUG-29, BUG-30, UX-7, UX-10 done; 66/66 tests; 0 warnings
**Next up**: F-13 Phase 1 (iOS form styling) or F-14 (tag color picker)

---

## Priority Queue

Items are listed in work order. Start at the top, work down.

| # | ID | Item | Source | Notes |
|---|-----|------|--------|-------|
| 1 | F-13 | iOS native form design pass — Phase 1 (field + container styling) | Ken | Remove nested border effect on iOS: transparent InputBorder + no-stroke PrayerCardBorder on iOS. Fix placeholder color (Gray200→Gray400). Android unchanged. Phase 2 (button layout) deferred. |
| 2 | F-14 | Tag color palette: user-defined colors + native picker | Tony/Ken | New `UserColor` table. FlexLayout wrap grid. iOS: native `UIColorPickerViewController`. Android: hex-entry popup. 8 defaults seeded. |
| 3 | F-15 | Notification tap opens prayer request + ad hoc "I prayed" button | Ken | Currently notification opens to home page with no context. Should deep-link to the prayer request view page. Add standalone "I prayed for this" button so users can record interactions outside Prayer Time. |
| 4 | TD-12 | Full ViewModel ObservableCollection audit | — | Review all ObservableCollections across all ViewModels for blocking calls, inconsistencies, and data flow issues. Standardize initialization patterns using modern best practices. |
| 5 | TD-8 | Refactor ViewModels to constructor injection | — | All ViewModels resolve services at runtime via MAUI DI host, making them impossible to unit test. |
| 6 | F-10 | Deep-link share — create card/request via tapped link | — | Deferred until app is in the store — full plan at `docs/plans/F10-deep-link-share.md` |
| 7 | INV-4 | In-app update notification — Android Play Core | — | **Blocked:** `Xamarin.Google.Android.Play.App.Update 2.1.0.18` conflicts with MAUI 10.0.41 AndroidX pin. Also tried `Xamarin.Google.Android.Play.Core 1.10.3` — only targets `net6.0-android31.0`. Resume when MAUI bumps AndroidX floor or a compatible binding ships. |

---

## Detailed Descriptions

### F-15 Notification deep-link + ad hoc "I prayed" button

**Reporter:** Ken
**Details:** Two related changes:
1. When user taps a notification, navigate to the specific prayer request's view page instead of the home page.
2. Add an "I prayed for this" button on the prayer request view/detail page so users can log interactions without entering a full Prayer Time session.
**Requires:** Deep-link routing from notification payload → Shell navigation to PrayerDetailPage with prayer ID.

### TD-8 Refactor ViewModels to constructor injection

All ViewModels resolve services via `IPlatformApplication.Current!.Services.GetRequiredService<>()` inside the default constructor. This couples them to the MAUI runtime — they cannot be instantiated in a test context.

**Fix:** Add constructor overloads (or replace the default constructor) that accept injected services. Register ViewModels with DI in `MauiProgram.cs` and update XAML pages to resolve them from the container rather than using `<viewModels:XViewModel />` in XAML.

**Affects all ViewModels:** `PrayerRequestDetailViewModel`, `PrayerCardViewModel`, `HomeViewModel`, `PrayerTimeViewModel`, `TagDetailViewModel`, `SettingsViewModel`

**Unlocks:** Full ViewModel unit test coverage including notification scheduling hooks, mark-answered flow, onboarding state transitions

---

### F-10 Deep-link share

> **Full implementation plan:** [`docs/plans/F10-deep-link-share.md`](docs/plans/F10-deep-link-share.md)
> **Deferred until app is live in the App Store / Play Store** — the `prayercards://` URI scheme must be registered and live before links work for recipients.

Both cards and individual requests are shareable. Share sheet sends a `prayercards://` deep link alongside a plain-text fallback (for recipients without the app).

**Recipient behavior:**
- Shared card → auto-saved silently as a new card with all active requests
- Shared request → auto-saved into a "Shared with me" system card (top of list, cannot be deleted, hideable in Settings)

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
| BUG-22 | iOS AOT crash on launch (build 8) — SQLite-net module out of date | — | `Platforms/iOS/LinkerConfig.xml` with `preserve="all"` for SQLite-net + SQLitePCLRaw; wired via `TrimmerRootDescriptor` in csproj. |
| BUG-23 | Prayers tab crash — `XamlParseException: StaticResource not found for key Gray700` | — | 3 references to undefined `Gray700` replaced with `Gray600` in PrayerListPage.xaml toggle buttons. |
| TD-9 | Dark mode color audit | — | Full audit. 2 confirmed failures, 1 borderline. Fixes tracked as TD-11. Notes at `docs/research/TD-9-dark-mode-color-audit.md`. |
| TD-7 | Extract `ILocalNotificationCenter` | — | `ILocalNotificationCenter` + `NotifyRepeat` enum; `LocalNotificationCenterWrapper`; `NotificationService` fully injectable. 11 new tests; 66/66 passing. |
| TD-11 | Dark-mode contrast fixes (from TD-9 audit) | — | Primary → AppThemeBinding on PrayerCardsPage + Settings; SuccessGreen → AppThemeBinding on PrayerDetailPage. Added `SuccessGreenDark` to Colors.xaml. |
| UX-4 | Delete confirmation dialogs | — | Confirmation on card delete (with prayer count), prayer delete (with title), tag delete (already had it). All entry points covered. |
| BUG-25 | Prayer edit shows wrong notification toggle + frequency | — | `RefreshProperties()` never notified `PrayerFrequency` (only `PrayerFrequencyDisplay`). Picker stayed at model default (`Weekly`). Added `OnPropertyChanged(nameof(PrayerFrequency))` to setter and `RefreshProperties()`. |
| BUG-24 | Tag filter doesn't refresh after saving prayer via card | — | `_requestTagIds` lookup in `PrayerListViewModel` was only built in constructor, never rebuilt on save. Extracted `HandleSavedAsync` that rebuilds tag lookup + refreshes tag chips. |
| UX-9 | InputBorder — bordered container for all input fields | — | Added `InputBorder` named style (White/Gray900 bg, Gray300/Gray600 stroke, 8px corners). Wrapped 8 inputs across 4 edit pages. Editor implicit bg changed to Transparent. TagDetailPage label standardized to FormLabel. |
| UX-6 | Uniform action buttons across all data entry forms | — | PrayerCardPage Delete now uses `DangerButton` (was unstyled `StyleClass=""`). QuickAddPage Cancel uses `GhostButton`. All button grids standardized to `ColumnSpacing="8"` + `HorizontalOptions="Fill"`. |
| UX-5 | Tag entry UX — section header + spacing | — | Tags section already had FormLabel header. Added DividerLine separator with top margin between tag area and action buttons on PrayerDetailPage. |
| BUG-26 | Typed tag without hitting Return doesn't save | — | `SaveAsync` now auto-submits any pending `TagSearchText` before saving via `SubmitTagEntryAsync()`. |
| UX-8 | Remove underline from prayer requests on home + prayers pages | — | Removed per-request DividerLine BoxView from MainPage.xaml and PrayerListPage.xaml item templates. |
| TD-10 | Fix XC0022/XC0023 compiled binding warnings | — | Replaced `x:DataType="{x:Null}"` with `x:DataType="models:PrayerTag"` in PrayerDetailPage.xaml SuggestedTags DataTemplate. QuickAddPage already fully typed. |
| BUG-27 | Splash screen shows broken image instead of app icon | — | Replaced placeholder `splash.svg` (yellow line on card) with app icon foreground SVG. `MauiSplashScreen Color` provides the green background. |
| BUG-28 | iOS 26 crash on launch during tab transition | — | Removed blocking `.Result` calls from PrayerCardsViewModel and PrayerListViewModel constructors (deadlock risk on iOS 26 scheduler). Added async `LoadAsync()` called from `OnAppearing`. Added global exception handlers to iOS AppDelegate for diagnostics. Guarded onboarding popup with try/catch. |
| BUG-29 | iPad crash when skipping onboarding tour | — | `PopupBlockedException` — popup must be closed before mutating onboarding state so modal stack is clear when completion popup fires. Reordered `CloseAsync` before `Skip()`/`Advance()`. |
| BUG-30 | Switch "On" state invisible in dark mode | — | `OnColor` dark value was `Gray200` (near-white) with white thumb. Changed to `Primary` (green) so white thumb is clearly visible in dark mode. |
| UX-7 | Home page unified overdue card + Last Prayed stat | — | Merged two disconnected boxes into one `PrayerCardBorder` card: bold count header (tappable → Prayers filtered to Overdue) + inline needs-attention list. Added "Last prayed: X" stat label. Added `FilterStatus.Overdue` to PrayerListViewModel + `?filter=overdue` navigation. |
| UX-10 | Prayer card form styling — match request form | — | Applied `PrayerCardBorder` style, removed redundant "Prayer Card" title label, restructured to Grid layout with pinned onboarding banner, added DividerLine above buttons. |

---

*Last updated: 2026-03-19 (session 13 — BUG-29/30, UX-7/10 completed; 66/66 tests; 0 warnings)*

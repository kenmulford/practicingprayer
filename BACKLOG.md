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
**Last completed**: BUG-7 color picker + BUG-8 backup crash (commit 5f36cd5); feature/f11-backup pending merge
**Next up**: F-10 deep-link share (or merge feature/f11-backup → dev first)

---

## Priority Queue

Items are listed in work order. Start at the top, work down.

| # | ID | Item | Notes |
|---|-----|------|-------|
| 1 | F-10 | Deep-link share — create card/request via tapped link | Custom URI scheme; recipient opens app (or store if not installed) and lands on pre-filled create flow |
| 2 | F-11 | iCloud / Google Drive backup — export/import DB for cross-device transfer | Implementation complete on feature/f11-backup; pending merge to dev |
| 3 | INV-1 | Audit: are all notifications cancelled when a prayer is marked answered? | Verify `INotificationService` cancels scheduled reminders on answered; check if stale notifications can fire after mark-answered |
| 4 | INV-2 | Audit: why does add/edit prayer card still show reminder/notification UI? | Was this intentionally re-added or is it leftover UI that should have been removed? Confirm intended behaviour before touching |
| 5 | BUG-9 | Notification permission prompt only fires on first Settings visit — misses tutorial flow | Users can toggle notifications ON during tutorial (prayer request creation) before ever hitting Settings; OS permission is never requested. Fix: trigger permission check at the moment the user first enables notifications anywhere in the app, not at Settings load |
| 6 | BUG-10 | Settings "Go" button text clipped on some devices | `ColumnDefinitions="300, *"` gives fixed 300dp to label, starving button column on smaller screens. Rethink layout — likely `Auto` label column or a stacked row |
| 7 | BUG-11 | Keyboard covers prayer request entry buttons — cannot save without manually dismissing keyboard | Soft keyboard overlaps bottom of form; save/accept buttons unreachable while keyboard is open. Known MAUI issue — fix via `android:windowSoftInputMode="adjustResize"` in AndroidManifest and/or CommunityToolkit `KeyboardAutoManagerScroll` |
| 8 | INV-3 | Lock app to portrait everywhere except Prayer Time — passive UX signal that Prayer Time should landscape | Portrait lock on all pages via `IOrientationService`; Prayer Time already uses `IOrientationService` to force landscape — confirm that removing the lock on page leave restores correctly and that no other page breaks |

---

## Detailed Descriptions

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
| UX-3 | Card list — dividers between rows | #10 | BoxView DividerLine in BindableLayout |
| — | Dark mode contrast audit | — | 7 files fixed; version bumped to 1.0.1 |
| — | App renamed to "Prayer Cards" | — | ApplicationTitle + ApplicationId updated |

---

*Last updated: 2026-03-16*

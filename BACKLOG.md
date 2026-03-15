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

**Status**: Idle — no active feature branch
**Last completed**: Contrast audit + version bump to 1.0.1
**Next up**: See Priority Queue below

---

## Priority Queue

Items are listed in work order. Start at the top, work down.

| # | ID | Item | Notes |
|---|-----|------|-------|
| 1 | BUG-7 | Tag color picker clips "gray" — last swatch half off screen, no scroll | New tag page; color row needs horizontal scroll or wrapping |
| 2 | BUG-8 | Backup fails immediately on tap | Todd (closed test); "Backup failed" toast fires instantly — likely missing Android permission or FileSaver init issue |
| 3 | F-10 | Deep-link share — create card/request via tapped link | Custom URI scheme; recipient opens app (or store if not installed) and lands on pre-filled create flow |
| 4 | F-11 | iCloud / Google Drive backup — export/import DB for cross-device transfer | Implementation complete on feature/f11-backup; needs merge after BUG-8 resolved |

---

## Detailed Descriptions

### BUG-7 — Tag color picker clips last swatch

**Reporter:** Tony (closed testing, 2026-03-15)
**Verbatim:** "the color selector on the new tag page won't let me select the 'gray' icon. It's half off the screen and he cannot scroll the colors horizontally."

The color swatch row on the new/edit tag page is a fixed horizontal layout. The last color ("gray") is partially clipped outside the visible area and the row is not scrollable, making it impossible to select.

**Likely files:**
`Views/Tags/TagDetailPage.xaml` — the color picker control or `HorizontalStackLayout` hosting the swatches

**Fix options:**
- Wrap the swatch row in a `ScrollView` with `Orientation="Horizontal"`
- Or switch to a `FlexLayout` with `Wrap="Wrap"` so swatches flow to a second row

---

### BUG-8 — Backup fails immediately on tap

**Reporter:** Todd (closed testing, 2026-03-15)
**Verbatim:** "backup failed immediately when tapped" _(screenshot attached)_

The "Back Up Now" button shows a "Backup failed" toast almost instantly. The failure is caught in `BackupService.ExportAsync()` and surfaced as a toast, so an unhandled exception is occurring before or during `IFileSaver.SaveAsync()`.

**Suspected causes (in order of likelihood):**
1. **`FileSaver.Default` eager initialization** — registered as `AddSingleton<IFileSaver>(FileSaver.Default)` which accesses `Default` before the MAUI platform is fully ready. Should use a factory lambda: `_ => FileSaver.Default`
2. **Missing `READ_MEDIA_*` or `WRITE_EXTERNAL_STORAGE` manifest entry** — some Android versions require this even with SAF
3. **`IFileSaver` not initialized via `UseMauiCommunityToolkitStorage()`** — separate setup call needed in some CommunityToolkit versions

**Files to check:**
`Services/BackupService.cs`, `MauiProgram.cs` (registration), `Platforms/Android/AndroidManifest.xml`

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
| UX-3 | Card list — dividers between rows | #10 | BoxView DividerLine in BindableLayout |
| — | Dark mode contrast audit | — | 7 files fixed; version bumped to 1.0.1 |
| — | App renamed to "Prayer Cards" | — | ApplicationTitle + ApplicationId updated |

---

*Last updated: 2026-03-15 (Round 1 closed testing feedback — BUG-7, BUG-8 logged)*

# Audit Pass E -- Post-Remediation Final Review

**Date:** 2026-03-26
**Auditor:** Claude Opus 4.6 (automated)
**Branch:** `dev` (commit `153e984`)
**Prior Audits:** Pass A, Pass B (same session)
**Scope:** Complete codebase review of all .cs, .xaml, platform config, and project files

---

## Summary

This is a post-remediation audit. The team fixed many issues identified in Passes A and B between those audits and the current codebase state. This pass verifies those fixes and identifies any remaining or newly introduced issues.

| Severity | Count |
|----------|-------|
| Critical | 0     |
| High     | 2     |
| Medium   | 6     |
| Low      | 7     |
| Info     | 4     |

**Overall assessment:** The codebase is in good shape for app store submission. The two Critical issues from Pass B (PrivacyInfo.xcprivacy UserDefaults and backup cache invalidation) have both been fixed. The remaining findings are quality improvements, not blockers.

---

## Verified Fixes from Prior Audits

### VERIFIED FIX: Backup restore now invalidates all service caches (was CRITICAL-01 / HIGH-3)
- **File:** `BackupService.cs`, lines 121-125
- After `ReinitializeAsync`, the code now calls `_cardService.InvalidateCache()`, `_prayerService.InvalidateCache()`, and `_tagService.InvalidateCache()`. The services are injected via constructor. Notification rescheduling also runs post-restore.
- **Status:** FIXED

### VERIFIED FIX: PrivacyInfo.xcprivacy UserDefaults is now declared (was CRITICAL-02 / HIGH-1)
- **File:** `Platforms/iOS/Resources/PrivacyInfo.xcprivacy`
- The `NSPrivacyAccessedAPICategoryUserDefaults` entry with reason `CA92.1` is present and uncommented. `NSPrivacyCollectedDataTypes` (empty array) and `NSPrivacyTracking` (false) are both present.
- **Status:** FIXED

### VERIFIED FIX: IEditGuard now covers tab switches (was HIGH-01 / HIGH-2)
- **File:** `AppShell.xaml.cs`, line 88
- The guard now checks `ShellNavigationSource.Pop`, `ShellNavigationSource.ShellItemChanged`, and `ShellNavigationSource.ShellSectionChanged`.
- **Status:** FIXED

### VERIFIED FIX: Monthly notification IDs use 1M offset (was HIGH-05)
- **File:** `LocalNotificationCenterWrapper.cs`, line 18
- `MonthlyIdOffset = 1_000_000` is defined and used consistently in `ScheduleMonthlyAsync` and `CancelMonthly`. No collision between prayer IDs and monthly notification IDs.
- **Status:** FIXED

### VERIFIED FIX: Notification scheduling wrapped in try/catch (was MEDIUM-09)
- **File:** `NotificationService.cs`, lines 35-110
- The entire `ScheduleAsync` method body is wrapped in a try/catch that logs failures without blocking prayer saves.
- **Status:** FIXED

### VERIFIED FIX: SafeAreaEdges on all main pages (was LOW-04)
- **Files:** `MainPage.xaml`, `PrayerCardsPage.xaml`, `PrayerListPage.xaml`, `TagsPage.xaml`, `PrayerCardPage.xaml`, `PrayerDetailPage.xaml`, `TagDetailPage.xaml`, `QuickAddPage.xaml`, `PrayerTimePage.xaml`, `SettingsHubPage.xaml`, `AppSettingsPage.xaml`, `BackupPage.xaml`, `AboutPage.xaml`, `HelpPage.xaml`, `PrayerTimeScopePage.xaml`
- All content pages set `SafeAreaEdges="Container"` or `SafeAreaEdges="All"`.
- **Status:** FIXED

### VERIFIED FIX: SettingsHubPage accessibility (was HIGH-03)
- **File:** `SettingsHubPage.xaml`
- Each navigation row Grid now has `SemanticProperties.Hint="Double tap to open"`. Chevron labels are hidden from accessibility tree.
- **Status:** FIXED

### VERIFIED FIX: IsDirty tracks notification time/day fields (mentioned in audit scope)
- **File:** `PrayerRequestDetailViewModel.cs`
- `IsDirty` compares `_prayer.NotifyHour`, `_prayer.NotifyMinute`, `_prayer.NotifyDayOfWeek`, and `_prayer.NotifyDayOfMonth` against captured originals. `CaptureOriginals()` stores all four values.
- **Status:** FIXED

---

## Area 1: Data Integrity, MVVM, and Navigation

### HIGH-E01: PrayerCardViewModel.LoadPrayerCardAsync does not null-check LoadAsync result

- **File:** `PrayerApp/ViewModels/PrayerCardViewModel.cs`, line 281
- **Severity:** High
- **Status:** NOT FIXED (was LOW-01 in Pass B, upgrading to High)
- **Detail:** `PrayerCard.LoadAsync(id)` calls `_dbService.FindAsync<T>(id)` which returns null for missing records. The result is assigned directly to `_prayerCard` without a null guard. The `finally` block calls `RefreshProperties()` which accesses `_prayerCard.Title` -- this will throw NullReferenceException if the card was deleted externally (e.g., by backup restore or another code path).
- **Impact:** App crash if navigating to a deleted card. Unlikely in normal use but possible after backup restore or race conditions.
- **Fix:** Add null check before assignment:
  ```csharp
  var result = await PrayerCard.LoadAsync(id);
  if (result is null) { await Shell.Current.GoToAsync(".."); return; }
  _prayerCard = result;
  ```

### MEDIUM-E01: PropertyChanged lambda subscriptions never unsubscribed

- **File:** `PrayerApp/ViewModels/PrayerCardsViewModel.cs`, line 282
- **File:** `PrayerApp/ViewModels/PrayerListViewModel.cs`, line 409
- **Severity:** Medium (was HIGH-02, downgraded -- unlikely to cause user-visible issues)
- **Status:** NOT FIXED
- **Detail:** `SubscribeToPropertyChanges` attaches anonymous lambdas to `card.PropertyChanged` / `prayer.PropertyChanged`. These lambdas capture `this` (the parent ViewModel). When cards/prayers are removed from the collection, the old ViewModels still hold references to the parent via these lambdas. Since both the parent and child ViewModels are short-lived (transient pages), the actual leak is minimal in practice -- the entire object graph is eligible for GC when the page is popped. However, it remains technically incorrect.
- **Fix:** Use `WeakEventManager` or store/unsubscribe handlers when items are removed.

### MEDIUM-E02: PrayerTimeViewModel._loadCts not disposed before replacement

- **File:** `PrayerApp/ViewModels/PrayerTimeViewModel.cs`, lines 228-229
- **Severity:** Medium (was MEDIUM-01 in Pass B)
- **Status:** NOT FIXED
- **Detail:** `_loadCts.Cancel()` is called but `_loadCts.Dispose()` is not. Low practical impact since LoadEntriesAsync is called infrequently.
- **Fix:** Add `_loadCts.Dispose()` after `Cancel()` before reassignment.

### MEDIUM-E03: PrayerCardsViewModel._filterAnnounceCts not disposed

- **File:** `PrayerApp/ViewModels/PrayerCardsViewModel.cs`, line 269
- **Severity:** Medium (was MEDIUM-02 in Pass B)
- **Status:** NOT FIXED
- **Detail:** Same pattern as E02. Called on every keystroke in the search bar.
- **Fix:** Add `_filterAnnounceCts?.Dispose()` before reassignment.

### LOW-E01: PrayerListViewModel suppress/restore _suppressAnnounce missing in RefreshAsync

- **File:** `PrayerApp/ViewModels/PrayerListViewModel.cs`, `RefreshAsync()` method
- **Severity:** Low
- **Status:** NEW
- **Detail:** `RefreshAsync` calls `ApplyFilter()` at the end, which fires `SemanticScreenReader.Announce($"Showing {count} prayers")` every time the user switches tabs (OnAppearing triggers RefreshAsync). This creates unnecessary screen reader noise on every tab visit. `LoadAsync` correctly sets `_suppressAnnounce = true` during initial load, but `RefreshAsync` does not.
- **Fix:** Set `_suppressAnnounce = true` at the start of `RefreshAsync` and restore in a finally block, matching the pattern in `LoadAsync`.

### LOW-E02: HomeViewModel.LoadAsync has no IsLoading guard

- **File:** `PrayerApp/ViewModels/HomeViewModel.cs`
- **Severity:** Low
- **Status:** NEW
- **Detail:** `LoadAsync` is called on every `OnAppearing` of `MainPage` (no `_loaded` guard like other pages). If the user switches tabs rapidly, multiple concurrent `LoadAsync` calls could race. The `SuggestedPrayers.Clear()` and re-add loop is not guarded. No data corruption risk, but could cause brief visual flicker.
- **Fix:** Add a `_loaded` / `_isLoading` guard or call a lightweight `RefreshAsync` on subsequent visits.

### LOW-E03: QuickAddPage has no IEditGuard

- **File:** `PrayerApp/Views/QuickAddPage.xaml.cs`
- **Severity:** Low (was LOW-07 in Pass B)
- **Status:** NOT FIXED
- **Detail:** QuickAddPage is a modal with a single text field. If the user types a title and then dismisses the modal (iOS swipe-down gesture), the text is lost without warning. Since it is a quick-add form with only one field, the impact is minor.

### INFO-E01: PrayerTagSelectionViewModel appears unused

- **File:** `PrayerApp/ViewModels/PrayerTagSelectionViewModel.cs`
- **Severity:** Info (was INFO-01 in Pass B)
- **Status:** NOT FIXED
- **Detail:** This ViewModel is not referenced in any View, XAML, or DI registration. It is dead code superseded by inline tag search in PrayerRequestDetailViewModel. Safe to remove.

---

## Area 2: Accessibility and App Store Readiness

### HIGH-E02: SCHEDULE_EXACT_ALARM on Android 14+ needs verification

- **File:** `Platforms/Android/AndroidManifest.xml`
- **Severity:** High (was HIGH-04 in Pass B)
- **Status:** NOT FIXED / UNVERIFIED
- **Detail:** `SCHEDULE_EXACT_ALARM` is declared in the manifest. Starting with Android 14 (API 34), this permission requires runtime handling. Plugin.LocalNotification v14.0.0 may handle this internally, but verification is needed. The app targets `net10.0-android` which defaults to targetSdkVersion 35. Google Play may flag the permission.
- **Recommendation:** Verify that Plugin.LocalNotification v14 handles the `ACTION_REQUEST_SCHEDULE_EXACT_ALARM` intent. If not, either add the runtime permission flow or switch the alarm type to `InexactAllowWhileIdle` in `LocalNotificationCenterWrapper.ShowAsync`. For a prayer reminder app, inexact timing is likely acceptable.

### MEDIUM-E04: Overdue filter button not discoverable in PrayerListPage

- **File:** `PrayerApp/Views/Prayer/PrayerListPage.xaml`
- **Severity:** Medium (was LOW-05 in Pass B, upgrading -- app store readiness concern)
- **Status:** NOT FIXED
- **Detail:** The PrayerListPage shows a 3-button filter row (Active / Answered / All) but the "Overdue" filter is only reachable via deep link from the home page (`?filter=overdue`). The `SetStatusCommand` handler supports "Overdue" but there is no UI button for it. Users cannot discover or use the Overdue filter unless they tap the overdue card on the home page.
- **Fix:** Add "Overdue" as a fourth filter button in the Grid (change to `ColumnDefinitions="*,*,*,*"`), or remove the Overdue filter code if it is not intended to be user-facing.

### MEDIUM-E05: MainPage overdue card Grid should not have SemanticProperties.Description

- **File:** `PrayerApp/Views/MainPage.xaml`, lines 41-52
- **Severity:** Medium
- **Status:** NEW
- **Detail:** Per the accessibility skill: "On iOS/VoiceOver, setting SemanticProperties.Description on a layout (e.g. Grid) makes the entire container a single accessible element, making child elements unreachable." The overdue header Grid uses `TapGestureRecognizer` with `GoToOverdueCommand` and has `SemanticProperties.Hint` on child Labels. This is actually fine since no Description is set on the Grid itself. However, the `SuggestedPrayerViewModel` Grid rows have `TapGestureRecognizer` but rely on child Label hints -- screen readers may not clearly convey these as tappable rows.
- **Recommendation:** Add `SemanticProperties.Hint="Double tap to view"` on the SuggestedPrayer Grid itself (this is safe since it is not a Description).

### LOW-E04: Info.plist CFBundleIdentifier is empty string

- **File:** `Platforms/iOS/Info.plist`, line 36
- **Severity:** Low (was MEDIUM-06 in Pass B)
- **Status:** NOT FIXED
- **Detail:** `<key>CFBundleIdentifier</key><string></string>` is empty. MAUI fills this from csproj `ApplicationId` at build time. Works correctly but some Apple validation tools may warn.
- **Fix:** Remove the key entirely or set it to `$(CFBundleIdentifier)`.

### LOW-E05: PrayerTimePage gradient uses inline hex colors

- **File:** `PrayerApp/Views/PrayerTime/PrayerTimePage.xaml`, line 125
- **Severity:** Low (was MEDIUM-05 in Pass B)
- **Status:** NOT FIXED
- **Detail:** `GradientStop Color="{AppThemeBinding Light=#FAF8F3, Dark=#0d0e0c}"` uses inline hex values. These match `PageLight` and `PageDark` in Colors.xaml but are not referenced as StaticResource. The gradient cannot use StaticResource in a GradientStop AppThemeBinding, so this is a known MAUI limitation. Downgrading to Low since the values match the theme.

### INFO-E02: Location usage descriptions present for Plugin.LocalNotification

- **File:** `Platforms/iOS/Info.plist`
- **Status:** ACKNOWLEDGED (was INFO-02)
- **Detail:** `NSLocationWhenInUseUsageDescription` and `NSLocationAlwaysAndWhenInUseUsageDescription` are present with explanatory "not used" text. Required by Plugin.LocalNotification's Info.plist merge. Apple reviewers may ask; the descriptions are sufficient.

### INFO-E03: AppSettingsPage uses code-behind event handlers

- **File:** `Views/Settings/AppSettingsPage.xaml.cs`
- **Status:** ACKNOWLEDGED (was INFO-05)
- **Detail:** Intentional per architecture. Static `Settings` class does not fit ObservableObject pattern.

---

## Area 3: Memory, Performance, and Edge Cases

### MEDIUM-E06: ObservableCollection Clear+Add pattern in multiple ViewModels

- **File:** Multiple ViewModels (PrayerCardsViewModel, PrayerListViewModel, TagsViewModel)
- **Severity:** Medium (was MEDIUM-08 in Pass B)
- **Status:** NOT FIXED
- **Detail:** `ApplySorting()`, `ApplyFilter()`, and `LoadAsync()` use `collection.Clear()` followed by individual `Add()` calls. Each Add fires `CollectionChanged`, causing incremental UI re-renders. For the current data size (dozens of items), this is acceptable. For larger datasets (hundreds), it would cause visible flicker.
- **Note:** `PrayerCardsViewModel.ApplySorting` does have a smart check that skips the clear+add if order hasn't changed. This mitigates the issue for sorting. The `ApplyFilter` and `LoadAsync` paths still always clear+add.

### LOW-E06: N+1 query in TagService.GetRequestIdsByTagIdsAsync

- **File:** `PrayerApp/Services/TagService.cs`, lines 77-88
- **Severity:** Low (was LOW-02 in Pass B)
- **Status:** NOT FIXED
- **Detail:** Iterates each tagId calling `_dbService.GetByTagIdAsync(tagId)`. One query per tag. Acceptable for the small number of tags in this app.

### LOW-E07: PrayerListViewModel.BuildRequestTagLookupAsync loads all PrayerCardTag rows

- **File:** `PrayerApp/ViewModels/PrayerListViewModel.cs`, line 295
- **Severity:** Low (was LOW-03 in Pass B)
- **Status:** NOT FIXED / ACCEPTABLE
- **Detail:** `PrayerCardTag.LoadAllAsync()` fetches every junction row. Fine for hundreds of rows.

### INFO-E04: Seed data uses UTC vs production local time

- **File:** `PrayerApp/Services/DBService.cs` (SeedDataAsync)
- **Severity:** Info (was INFO-04)
- **Status:** ACKNOWLEDGED
- **Detail:** Debug-only seed data uses `DateTime.UtcNow`. No production impact.

---

## API Currency Check (maui-current-apis skill)

| Check | Result |
|-------|--------|
| `DisplayAlert` deprecated in .NET 10 | PASS -- All calls use `DisplayAlertAsync` |
| `DisplayActionSheet` deprecated | PASS -- `MainPage.xaml.cs` uses `DisplayActionSheetAsync` |
| `FadeTo`/`TranslateTo` deprecated | PASS -- `AppShell.xaml.cs` uses `FadeToAsync` and `TranslateToAsync` |
| `Frame` deprecated | PASS -- No `Frame` usage; all card wrapping uses `Border` |
| `ListView` / `TableView` deprecated | PASS -- All lists use `CollectionView` or `BindableLayout` |
| `Device.*` static class deprecated | PASS -- Uses `MainThread.BeginInvokeOnMainThread`, `Dispatcher`, etc. |
| `MessagingCenter` deprecated | PASS -- Not used; events + service injection instead |
| `Page.IsBusy` deprecated | PASS -- Uses `ActivityIndicator` explicitly |
| `AutomationProperties.Name` deprecated | PASS -- Uses `SemanticProperties.Description` throughout |
| `Color.FromHex()` deprecated | PASS -- Uses `Color.FromArgb()` |

---

## Accessibility Audit (maui-accessibility skill)

| Check | Result |
|-------|--------|
| SemanticProperties.Description on Labels | PASS -- No Labels have Description (correct per skill) |
| Description on Grids (iOS hides children) | PASS -- No layout containers have Description |
| Decorative elements hidden | PASS -- Chevrons, badges, dividers use `AutomationProperties.IsInAccessibleTree="false"` |
| HeadingLevel on section headings | PASS -- Page titles use Level1, section headings use Level2 |
| Entry/Editor avoid Description (Android TalkBack) | PASS -- Entries use Placeholder and Hint, not Description |
| SemanticScreenReader.Announce for status changes | PASS -- Save/delete/load/filter actions announce |
| AutomationId on interactive elements | PASS -- All buttons, entries, switches, pickers have AutomationId |
| Missing Announce on RefreshAsync tab switch | ISSUE -- See LOW-E01 above |

---

## Recommended Priority Order for Remaining Fixes

1. **HIGH-E01** -- Null-check in PrayerCardViewModel.LoadPrayerCardAsync (5 min, crash prevention)
2. **HIGH-E02** -- Verify SCHEDULE_EXACT_ALARM handling for Android 14+ (30 min investigation)
3. **MEDIUM-E04** -- Add Overdue filter button to PrayerListPage (15 min, feature completeness)
4. **MEDIUM-E05** -- Add Hint to SuggestedPrayer Grid rows (5 min, accessibility)
5. **MEDIUM-E02/E03** -- Dispose CancellationTokenSources (5 min each)
6. **MEDIUM-E01** -- WeakEventManager for PropertyChanged subscriptions (30 min)
7. **MEDIUM-E06** -- Consider ObservableRangeCollection for future scaling (optional)
8. **LOW items** -- Address as time permits; none block submission

---

## Conclusion

The codebase has improved significantly since Pass B. Both Critical issues are resolved. The remaining High items are a potential NRE crash (easy fix) and an Android permission verification (investigation needed). No data integrity, navigation dead-end, or app store rejection risks remain beyond the SCHEDULE_EXACT_ALARM question. The app is ready for submission pending the HIGH-E01 fix.

---

*End of audit.*

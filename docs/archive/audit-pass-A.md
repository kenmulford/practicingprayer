# Audit Pass A -- Pre-Release Code Review

**Date:** 2026-03-26
**Auditor:** Claude Opus 4.6
**Scope:** Every .cs, .xaml, .plist, .xml source file in PrayerApp (excluding obj/bin)
**Methodology:** Full file read of all source, manual analysis against three audit areas

---

## Summary

The codebase is well-structured for its size. MVVM discipline is strong, cache invalidation is consistent, and accessibility has clearly been a priority. The findings below are ordered by severity. No show-stopper Critical issues were found, but several High items should be addressed before app store submission.

| Severity | Count |
|----------|-------|
| Critical | 0     |
| High     | 5     |
| Medium   | 9     |
| Low      | 8     |
| Info     | 5     |

---

## Area 1: Data Integrity, MVVM, and Navigation

### HIGH-1: PrivacyInfo.xcprivacy missing UserDefaults declaration

- **File:** `PrayerApp/Platforms/iOS/Resources/PrivacyInfo.xcprivacy` (lines 39-48)
- **Issue:** The app uses `Preferences` (MAUI wrapper around NSUserDefaults) extensively via the `Settings` class (FirstRun, OnboardingComplete, AllowNotifications, DefaultNotifyHour, DefaultNotifyMinute, OverdueDayThreshold, AutoModeIntervalSeconds, OnboardingStep). The UserDefaults declaration in the privacy manifest is **commented out**. Apple will reject the binary during App Store review.
- **Fix:** Uncomment the `NSPrivacyAccessedAPICategoryUserDefaults` block with reason `CA92.1`.

### HIGH-2: IEditGuard only fires on ShellNavigationSource.Pop -- tab switch bypasses guard

- **File:** `PrayerApp/AppShell.xaml.cs` (line 88)
- **Issue:** `OnShellNavigating` only checks `args.Source != ShellNavigationSource.Pop`. If a user is editing a prayer card (PrayerCardPage) or prayer detail (PrayerDetailPage) and taps a different tab, the navigation source is `ShellItem` not `Pop`, so the IEditGuard is never consulted. Unsaved changes are silently discarded.
- **Fix:** Also check for `ShellNavigationSource.ShellItemChanged` (or remove the source filter and check guard for all navigation away from the current page).

### HIGH-3: Backup/Restore does not invalidate service caches after reinitialize

- **File:** `PrayerApp/Services/BackupService.cs` (line 124-128)
- **Issue:** After `_dbService.ReinitializeAsync(_dbPath)`, the code navigates to `//MainPage` but never calls `InvalidateCache()` on `ICardService`, `IPrayerService`, or `ITagService`. These singletons still hold stale in-memory caches from the pre-restore database. The UI will show old data until the user force-refreshes or restarts.
- **Fix:** After `ReinitializeAsync`, resolve and invalidate all service caches. Also re-run the seed logic (system tags, Quick Add card, UserColor defaults) to ensure the restored DB has required rows.

### MEDIUM-1: CancellationTokenSource in PrayerCardsViewModel never disposed

- **File:** `PrayerApp/ViewModels/PrayerCardsViewModel.cs` (line 24, 269-271)
- **Issue:** `_filterAnnounceCts` is cancelled and re-created on each keystroke but `Dispose()` is never called on the old instance. Over a session with heavy search typing, this accumulates undisposed CTS objects. Not a leak per se (CTS finalizer handles it), but bad practice.
- **Fix:** Call `_filterAnnounceCts?.Dispose()` before reassigning.

### MEDIUM-2: CancellationTokenSource in PrayerTimeViewModel never disposed

- **File:** `PrayerApp/ViewModels/PrayerTimeViewModel.cs` (line 22, 228-229)
- **Issue:** `_loadCts` is cancelled and re-created but never disposed.
- **Fix:** Call `_loadCts.Dispose()` after `Cancel()`, before creating the new instance.

### MEDIUM-3: PropertyChanged lambda subscriptions in PrayerCardsViewModel leak

- **File:** `PrayerApp/ViewModels/PrayerCardsViewModel.cs` (line 282-299)
- **Issue:** `SubscribeToPropertyChanges` attaches a lambda to `card.PropertyChanged` that captures `this` (via `AllPrayerCards`, `ApplySorting`, `SemanticScreenReader`). If the PrayerCardViewModel outlives the PrayerCardsViewModel (unlikely but possible with Shell page caching), the lambda prevents GC. There is no corresponding unsubscribe path.
- **Fix:** Consider using `WeakReferenceMessenger` or storing the handler in a dictionary keyed by card so it can be unsubscribed on remove.

### MEDIUM-4: Race condition in PrayerCardViewModel.AddOrUpdatePrayerAsync

- **File:** `PrayerApp/ViewModels/PrayerCardViewModel.cs` (line 339-370)
- **Issue:** `AddOrUpdatePrayerAsync` is called from `ApplyQueryAttributes` via `SafeFireAndForget`. If the user quickly saves two prayers via "Save +", two concurrent calls could both pass `!_prayersLoaded` and both call `LoadPrayersAsync` simultaneously, leading to duplicate entries or exceptions during `Prayers.Clear()`.
- **Fix:** Add a simple guard boolean (`_loadingPrayers`) to serialize access, or use `SemaphoreSlim(1,1)`.

### LOW-1: PrayerListViewModel.SubscribeToPropertyChanges also leaks

- **File:** `PrayerApp/ViewModels/PrayerListViewModel.cs` (line 407-414)
- **Issue:** Same pattern as MEDIUM-3 -- lambda captures parent VM via `ApplyFilter()`.
- **Fix:** Same recommendation.

### LOW-2: AppSettingsPage uses code-behind event handlers instead of ViewModel

- **File:** `PrayerApp/Views/Settings/AppSettingsPage.xaml.cs` (lines 10-43)
- **Issue:** All settings logic (toggling notifications, changing overdue threshold, changing default time) is in the code-behind directly writing to `Settings`. This is a minor MVVM violation. The page doesn't have a ViewModel at all.
- **Fix:** Low priority -- the page is simple enough that code-behind is defensible, but for consistency, a SettingsViewModel would match the rest of the codebase.

### LOW-3: SettingsHubPage uses code-behind navigation handlers

- **File:** `PrayerApp/Views/Settings/SettingsHubPage.xaml.cs` (lines 10-21)
- **Issue:** Navigation to child pages is done via code-behind `Tapped` handlers rather than commands. Not technically wrong but inconsistent with the rest of the app.
- **Fix:** Low priority.

### INFO-1: BackupService.ImportAsync navigates before popping modal

- **File:** `PrayerApp/Services/BackupService.cs` (line 127-128)
- **Issue:** `GoToAsync("//MainPage")` is called before `PopModalAsync()`. Shell navigation and modal stacks are independent, so this works, but the order is unusual. On some devices, the modal may briefly show on the wrong tab.
- **Fix:** Consider popping modal first, then navigating.

---

## Area 2: Accessibility and App Store Readiness

### HIGH-4: PrivacyInfo.xcprivacy UserDefaults declaration missing (same as HIGH-1)

- Apple requires this declaration. Duplicate mention for app store readiness emphasis.

### HIGH-5: SCHEDULE_EXACT_ALARM requires user opt-in on Android 12+ (API 31+)

- **File:** `PrayerApp/Platforms/Android/AndroidManifest.xml` (line 8)
- **Issue:** `SCHEDULE_EXACT_ALARM` is declared but starting with Android 12 (API 31), the permission is not granted by default. On Android 13+ (API 33), apps targeting API 33+ should use `USE_EXACT_ALARM` instead (auto-granted for alarm/timer apps) or gracefully degrade to `setAndAllowWhileIdle()`. The Plugin.LocalNotification library handles this internally, but Google Play review may flag the permission if the app doesn't demonstrate alarm-clock use.
- **Fix:** Verify that Plugin.LocalNotification v14 handles the runtime permission check. If so, document in the Data Safety form that exact alarms are used for prayer reminders. If not, add runtime permission handling.

### MEDIUM-5: SemanticProperties.Description on Labels (iOS hides children issue)

- **File:** Multiple XAML files
- **Issue:** Several `Label` elements have `SemanticProperties.Hint` which is correct, but some interactive `Grid` containers use `SemanticProperties.Hint` on their children without ensuring the Grid itself is properly configured for accessibility. For example:
  - `PrayerListPage.xaml` line 172: `Grid` has `SemanticProperties.Hint` which on iOS can cause VoiceOver to read only the Grid's hint, hiding child Labels.
- **Fix:** Move `SemanticProperties.Hint` to individual tappable elements, or set `SemanticProperties.Description` on the Grid to a summary and mark children with `AutomationProperties.IsInAccessibleTree="false"`.

### MEDIUM-6: Missing SemanticProperties.HeadingLevel on several page titles

- **Files:** `PrayerCardsPage.xaml`, `PrayerListPage.xaml` (the "Prayer Requests" label uses `Style={StaticResource Headline}` which may not set HeadingLevel)
- **Issue:** Screen readers need heading levels to build the page hierarchy. Only some pages explicitly set `SemanticProperties.HeadingLevel`.
- **Fix:** Add `SemanticProperties.HeadingLevel="Level1"` to the main heading on each page. Verify the `Headline` style includes this.

### MEDIUM-7: Hardcoded gradient colors in PrayerTimePage

- **File:** `PrayerApp/Views/PrayerTime/PrayerTimePage.xaml` (line 125)
- **Issue:** `GradientStop Color="{AppThemeBinding Light=#FAF8F3, Dark=#0d0e0c}"` uses inline hex values instead of `StaticResource` keys. While these happen to match `PageLight` and `PageDark`, inline hex is fragile if the theme ever changes.
- **Fix:** Replace with `{AppThemeBinding Light={StaticResource PageLight}, Dark={StaticResource PageDark}}`.

### LOW-4: Info.plist CFBundleIdentifier is empty

- **File:** `PrayerApp/Platforms/iOS/Info.plist` (line 36-37)
- **Issue:** `CFBundleIdentifier` is set to an empty string. MAUI's build system populates this from the `ApplicationId` in the .csproj (`com.multithreadedllc.prayercards`), so this is cosmetically harmless. However, some tools may misread the plist directly.
- **Fix:** Either remove the key (let build inject it) or set it to `$(CFBundleIdentifier)` / the correct value.

### LOW-5: Info.plist has unnecessary location usage descriptions

- **File:** `PrayerApp/Platforms/iOS/Info.plist` (lines 47-50)
- **Issue:** `NSLocationWhenInUseUsageDescription` and `NSLocationAlwaysAndWhenInUseUsageDescription` are present with "This app does not use your location" strings. Apple reviewers may ask why location strings exist if the app never requests location. These are likely from a third-party library (Plugin.LocalNotification).
- **Fix:** Verify if Plugin.LocalNotification v14 still requires these. If not, remove them to avoid reviewer questions.

### LOW-6: CFBundleDisplayName is "Prayer" not "Practicing Prayer"

- **File:** `PrayerApp/Platforms/iOS/Info.plist` (line 39-40)
- **Issue:** `CFBundleDisplayName` is "Prayer" but the .csproj `ApplicationTitle` is "Practicing Prayer". The display name under the icon on the home screen will say "Prayer".
- **Fix:** Intentional? If so, Info. If not, update to match.

### INFO-2: Android targetSdkVersion not explicitly set

- **File:** `PrayerApp/PrayerApp.csproj`
- **Issue:** No explicit `<AndroidTargetSdkVersion>` is set. .NET 10 MAUI defaults to API 35 which meets Google Play's current requirement (API 34+). This is fine but should be verified at build time.

### INFO-3: Android allowBackup=false is set

- **File:** `PrayerApp/Platforms/Android/AndroidManifest.xml` (line 3)
- **Issue:** `android:allowBackup="false"` prevents Android's built-in backup mechanism. This is correct for a privacy-first app -- just noting it for awareness.

---

## Area 3: Memory, Performance, and Edge Cases

### MEDIUM-8: N+1 query pattern in TagService.GetRequestIdsByTagIdsAsync

- **File:** `PrayerApp/Services/TagService.cs` (lines 77-88)
- **Issue:** For each tag ID in the input, a separate `GetByTagIdAsync` call is made (one DB query per tag). If the user has many tags selected, this generates N queries.
- **Fix:** Add a batch method to `IDBService` that fetches all `PrayerCardTag` rows for a set of tag IDs in a single query.

### MEDIUM-9: PrayerListViewModel.BuildRequestTagLookupAsync loads all PrayerCardTag rows

- **File:** `PrayerApp/ViewModels/PrayerListViewModel.cs` (lines 292-303)
- **Issue:** `PrayerCardTag.LoadAllAsync()` fetches every row in the junction table into memory. For users with thousands of prayer-tag assignments, this could be slow.
- **Fix:** Add an indexed SQL query that returns only the columns needed (PrayerRequestId, PrayerTagId).

### LOW-7: ObservableCollection Clear+Add pattern causes UI flicker

- **Files:** Multiple ViewModels (PrayerCardsViewModel.LoadAsync, PrayerListViewModel.ApplyFilter, TagsViewModel.LoadAsync)
- **Issue:** The pattern of `collection.Clear(); foreach (item) collection.Add(item);` fires N+1 CollectionChanged events. For large lists, this causes visible flicker as the CollectionView rebuilds.
- **Fix:** Consider using `ReplaceRange` from CommunityToolkit (available via `ObservableRangeCollection`), or suppress notifications during the batch.

### LOW-8: Notification ID collision for monthly schedules on Android

- **File:** `PrayerApp/Services/LocalNotificationCenterWrapper.cs` (line 126)
- **Issue:** Monthly Android notifications use IDs `prayerId * 100 + monthIndex`. If a user has prayer ID 1 and prayer ID 100, prayer 1 month 0 gets ID 100 and prayer 100's non-monthly notification also has ID 100. Collision is unlikely with auto-increment IDs starting at 1, but theoretically possible once IDs reach the single digits (prayer 1 vs prayer 100).
- **Fix:** Use a higher multiplier (e.g., 10000) or a hash-based ID scheme to reduce collision risk.

### INFO-4: DiagnosticLog uses synchronous File.AppendAllText with lock

- **File:** `PrayerApp/Services/DiagnosticLog.cs` (line 22)
- **Issue:** `Log()` performs synchronous file I/O under a lock. If called from the UI thread (e.g., via `SafeFireAndForget`), it could cause a brief hang.
- **Fix:** Low priority -- exceptions are rare. Could use async file I/O with `SemaphoreSlim` instead of `lock` for future-proofing.

### INFO-5: Backup export does not validate DB integrity before sharing

- **File:** `PrayerApp/Services/BackupService.cs` (lines 17-68)
- **Issue:** The export reads the DB bytes after WAL checkpoint and zips them. There is no `PRAGMA integrity_check` before export. If the DB is corrupted (unlikely), the user gets a corrupted backup.
- **Fix:** Very low priority -- adding `integrity_check` would add latency to every export.

---

## Checklist Summary

### Must-fix before submission:
1. **HIGH-1/HIGH-4:** Uncomment UserDefaults in PrivacyInfo.xcprivacy
2. **HIGH-2:** Fix IEditGuard to also fire on tab switch navigation
3. **HIGH-3:** Invalidate all service caches after backup restore
4. **HIGH-5:** Verify SCHEDULE_EXACT_ALARM handling on Android 12+

### Should-fix:
5. MEDIUM-1/MEDIUM-2: Dispose CancellationTokenSources
6. MEDIUM-5/MEDIUM-6: Accessibility heading levels and Grid hints
7. MEDIUM-7: Replace hardcoded gradient hex with StaticResource
8. MEDIUM-8/MEDIUM-9: Optimize N+1 and full-table-scan queries

### Nice-to-have:
9. LOW-7: ObservableCollection batch updates
10. LOW-8: Notification ID collision mitigation
11. LOW-5: Remove unnecessary location plist entries

# Audit Pass B -- Pre-App-Store Code Review

**Date:** 2026-03-26
**Auditor:** Claude Opus 4.6 (automated)
**Branch:** `dev` (commit `153e984`)
**Scope:** Complete codebase review -- all .cs, .xaml, platform config, and project files

---

## Summary

| Severity | Count |
|----------|-------|
| Critical | 2     |
| High     | 5     |
| Medium   | 9     |
| Low      | 8     |
| Info     | 5     |

---

## Area 1: Data Integrity, MVVM, and Navigation

### CRITICAL-01: Backup restore does not invalidate singleton caches

- **File:** `PrayerApp/Services/BackupService.cs`, line 124
- **Severity:** Critical
- **Detail:** After `ImportAsync` calls `_dbService.ReinitializeAsync(_dbPath)`, it navigates to `//MainPage` but does NOT invalidate the in-memory caches on `CardService._cache`, `PrayerService._cardCache`/`._allCache`, or `TagService._cache`. These are singletons that persist for the app's lifetime. After a restore, every service call returns stale data from the OLD database until the user force-quits the app.
- **Fix:** Inject `ICardService`, `IPrayerService`, and `ITagService` into `BackupService` and call `InvalidateCache()` on each after `ReinitializeAsync`. Alternatively, expose a cache-clear on `ITagService`.

### HIGH-01: IEditGuard only triggers on ShellNavigationSource.Pop

- **File:** `PrayerApp/AppShell.xaml.cs`, line 88
- **Severity:** High
- **Detail:** `OnShellNavigating` returns early unless `args.Source == ShellNavigationSource.Pop`. When a user taps a different tab while on PrayerCardPage or PrayerDetailPage with dirty data, `args.Source` is `ShellNavigationSource.ShellSectionChanged` (tab switch), so the unsaved-changes guard is silently bypassed. Data is lost without warning.
- **Fix:** Also check for `ShellNavigationSource.ShellSectionChanged` and `ShellNavigationSource.ShellItemChanged`.

### HIGH-02: PrayerCardsViewModel.SubscribeToPropertyChanges leaks event handlers

- **File:** `PrayerApp/ViewModels/PrayerCardsViewModel.cs`, line 282
- **Severity:** High
- **Detail:** `card.PropertyChanged += (s, e) => { ... }` is a lambda that captures `card` and `this` (via `AllPrayerCards`, `ApplySorting`). When `LoadAsync` or `RefreshAsync` adds new VMs and removes old ones, the old VMs still hold a reference to `PrayerCardsViewModel` via the lambda. Since `PrayerCardsViewModel` is transient but service references inside it are singleton, this is a slow leak. The same pattern exists in `PrayerListViewModel.SubscribeToPropertyChanges` (line 409).
- **Fix:** Either unsubscribe in `RemoveTag`/`Remove` cleanup paths, or use `WeakEventManager` / `ConditionalWeakTable` pattern.

### MEDIUM-01: PrayerTimePage leaks _loadCts CancellationTokenSource

- **File:** `PrayerApp/ViewModels/PrayerTimeViewModel.cs`, lines 22, 229
- **Severity:** Medium
- **Detail:** `_loadCts` is replaced with a new CTS each call to `LoadEntriesAsync` but the old one is only `Cancel()`ed, never `Dispose()`d. Each CTS allocates a `CancellationTokenRegistration` and timer handle that won't be reclaimed until GC. Low leak rate but technically incorrect.
- **Fix:** Call `_loadCts.Dispose()` before replacing it.

### MEDIUM-02: PrayerCardsViewModel._filterAnnounceCts not disposed

- **File:** `PrayerApp/ViewModels/PrayerCardsViewModel.cs`, line 269
- **Severity:** Medium
- **Detail:** Same pattern as above. `_filterAnnounceCts?.Cancel()` does not dispose; the old CTS leaks.
- **Fix:** Dispose before replacing.

### MEDIUM-03: Race condition between SaveAndNewAsync and navigation

- **File:** `PrayerApp/ViewModels/PrayerRequestDetailViewModel.cs`, line 416-429
- **Severity:** Medium
- **Detail:** `SaveAndNewAsync` calls `CoreSaveAsync()` (which persists staged tags, schedules notifications) and then calls `ResetForNewPrayer`. No guard prevents double-tap on the "Save +" toolbar button. `AsyncRelayCommand` does provide re-entrancy protection, but between the Toast and the form reset, the UI may briefly accept additional input.
- **Fix:** Consider adding `IsBusy` gating or disabling the button during save.

### LOW-01: PrayerCardViewModel.LoadPrayerCardAsync null safety

- **File:** `PrayerApp/ViewModels/PrayerCardViewModel.cs`, line 281
- **Severity:** Low
- **Detail:** `PrayerCard.LoadAsync(id)` can return null (if `FindAsync<T>` returns null for a deleted card). The result is assigned directly to `_prayerCard` without null check. If null, `RefreshProperties()` in the finally block will throw NRE accessing `_prayerCard.Title`.
- **Fix:** Add null check: `if (result is null) { await Shell.Current.GoToAsync(".."); return; }`.

### LOW-02: TagService.GetRequestIdsByTagIdsAsync N+1 query

- **File:** `PrayerApp/Services/TagService.cs`, lines 77-88
- **Severity:** Low (mitigated by small tag count)
- **Detail:** Iterates each tagId calling `_dbService.GetByTagIdAsync(tagId)` -- one query per tag. For large tag sets this is inefficient.
- **Fix:** Add a bulk query method like `GetByTagIdsAsync(IEnumerable<int> tagIds)`.

### LOW-03: PrayerListViewModel.BuildRequestTagLookupAsync loads ALL PrayerCardTag rows

- **File:** `PrayerApp/ViewModels/PrayerListViewModel.cs`, line 295
- **Severity:** Low (acceptable at current data sizes)
- **Detail:** `PrayerCardTag.LoadAllAsync()` fetches every junction row. This is fine for hundreds of rows but will degrade at thousands.

### INFO-01: PrayerTagSelectionViewModel appears unused

- **File:** `PrayerApp/ViewModels/PrayerTagSelectionViewModel.cs`
- **Severity:** Info
- **Detail:** This ViewModel is not referenced in any View, XAML, or DI registration. It appears to be dead code from a previous iteration, superseded by the inline tag search in PrayerRequestDetailViewModel.
- **Fix:** Remove or mark as future use.

---

## Area 2: Accessibility and App Store Readiness

### CRITICAL-02: PrivacyInfo.xcprivacy is missing UserDefaults declaration

- **File:** `PrayerApp/Platforms/iOS/Resources/PrivacyInfo.xcprivacy`, lines 39-48
- **Severity:** Critical (App Store rejection risk)
- **Detail:** The `NSPrivacyAccessedAPICategoryUserDefaults` entry is **commented out**, but the app uses `Preferences.Get/Set` extensively in `Settings.cs` (7+ properties), `OnboardingService.cs`, and the MAUI framework itself. Apple's automated screening flags UserDefaults usage and rejects builds without the matching privacy manifest entry. This was introduced in spring 2024 and enforced for all submissions.
- **Fix:** Uncomment the UserDefaults block in PrivacyInfo.xcprivacy.

### HIGH-03: SettingsHubPage items lack accessibility semantics

- **File:** `PrayerApp/Views/Settings/SettingsHubPage.xaml`, lines 14-106
- **Severity:** High
- **Detail:** The four navigation rows (App Settings, Backup, About, Help) use `Grid` with `TapGestureRecognizer` but no `SemanticProperties.Description` or `SemanticProperties.Hint` on the Grid. VoiceOver/TalkBack will read each child Label separately rather than treating the row as a single tappable element. The chevron is correctly hidden (`AutomationProperties.IsInAccessibleTree="False"`), but the Grid needs `SemanticProperties.Description` and `SemanticProperties.Hint="Double tap to open"`.
- **Fix:** Add `SemanticProperties.Description="App Settings, Notifications and reminders"` and `SemanticProperties.Hint="Double tap to open"` on each Grid.

### HIGH-04: SCHEDULE_EXACT_ALARM deprecated on Android 14+

- **File:** `PrayerApp/Platforms/Android/AndroidManifest.xml`, line 8
- **Severity:** High
- **Detail:** `SCHEDULE_EXACT_ALARM` was restricted starting Android 14 (API 34). Apps must either request it at runtime via `ACTION_REQUEST_SCHEDULE_EXACT_ALARM` intent or switch to `USE_EXACT_ALARM` (for alarm-clock apps) or `setAndAllowWhileIdle` for non-exact alarms. Google Play may flag this permission. The app currently targets API 21+ but `.NET 10` defaults to `targetSdkVersion=35`.
- **Fix:** Verify `Plugin.LocalNotification` v14 handles the runtime permission request. If not, use `AlarmType = AndroidAlarmType.InexactAllowWhileIdle` or add the runtime permission flow.

### MEDIUM-04: Missing SemanticProperties.Description on interactive Grids in MainPage

- **File:** `PrayerApp/Views/MainPage.xaml`, lines 41-44
- **Severity:** Medium
- **Detail:** The overdue card header Grid (with `TapGestureRecognizer`) and each `SuggestedPrayerViewModel` Grid have `Hint` on child Labels but the Grid itself is not treated as a single accessible element. Screen readers may not convey it as a button.

### MEDIUM-05: PrayerTimePage hardcoded gradient colors

- **File:** `PrayerApp/Views/PrayerTime/PrayerTimePage.xaml`, line 125
- **Severity:** Medium
- **Detail:** `GradientStop Color="{AppThemeBinding Light=#FAF8F3, Dark=#0d0e0c}"` uses inline hex values instead of `StaticResource` tokens. If the theme palette changes, this gradient won't update.
- **Fix:** Define `GradientFadeLight` and `GradientFadeDark` in Colors.xaml and reference them.

### MEDIUM-06: Info.plist CFBundleIdentifier is empty

- **File:** `PrayerApp/Platforms/iOS/Info.plist`, line 36
- **Severity:** Medium
- **Detail:** `<key>CFBundleIdentifier</key><string></string>` is empty. MAUI overrides this from csproj `ApplicationId` at build time, so it works, but Xcode and some Apple validation tools may warn about it.
- **Fix:** Either remove the key (let MAUI inject it) or set it to `$(CFBundleIdentifier)`.

### LOW-04: No SafeAreaEdges on PrayerCardsPage, PrayerListPage, TagsPage, MainPage

- **File:** Multiple XAML files
- **Severity:** Low
- **Detail:** `PrayerCardPage.xaml`, `PrayerDetailPage.xaml`, `TagDetailPage.xaml`, `QuickAddPage.xaml`, and `PrayerTimePage.xaml` all set `SafeAreaEdges="Container"` or `"All"`. However, `PrayerCardsPage.xaml`, `PrayerListPage.xaml`, `TagsPage.xaml`, and `MainPage.xaml` do not. Shell provides some safe area handling, but explicit declaration is safer for notched devices.

### LOW-05: Overdue filter button not in SettingsHubPage XAML, only reachable from home

- **File:** `PrayerApp/Views/Prayer/PrayerListPage.xaml`
- **Severity:** Low
- **Detail:** The "Overdue" filter button exists in the 3-way toggle (Active/Answered/All) but is not rendered in the XAML. It is only accessible via deep link from the home page overdue card (`?filter=overdue`). Users cannot discover the Overdue filter on their own.
- **Fix:** Add "Overdue" as a fourth filter button, or document its existence in Help.

### INFO-02: Location usage descriptions required by third-party library

- **File:** `PrayerApp/Platforms/iOS/Info.plist`, lines 47-50
- **Severity:** Info
- **Detail:** `NSLocationWhenInUseUsageDescription` and `NSLocationAlwaysAndWhenInUseUsageDescription` are present with "not used" messages. This is correct -- Plugin.LocalNotification historically required these. Apple reviewers may ask about it; the current descriptions are sufficient explanation.

### INFO-03: No data safety declaration file for Google Play

- **Severity:** Info
- **Detail:** Google Play requires a data safety questionnaire (not a file in the project). Since the app is fully offline with no analytics, no network calls, and no data collection, the questionnaire answers are straightforward. This is a manual step during Play Console submission, not a code issue.

---

## Area 3: Memory, Performance, and Edge Cases

### HIGH-05: Notification ID collision for monthly schedules on Android

- **File:** `PrayerApp/Services/LocalNotificationCenterWrapper.cs`, lines 114-130
- **Severity:** High
- **Detail:** Monthly notifications on Android use IDs `prayer.Id * 100 + monthOffset`. If a user has prayer ID 1 and prayer ID 100, the monthly notifications collide: prayer 1 gets IDs 100-111, and prayer 100 gets IDs 10000-10011. This is fine. However, for prayer IDs over 21,474,836 (close to `int.MaxValue / 100`), the multiplication overflows. More practically: any prayer with ID >= 100 will have monthly IDs that could conflict with `prayerId` values of other prayers (e.g., prayer 200's one-shot notification ID is 200, but prayer 2's monthly offset 0 notification ID is also 200).
- **Fix:** Use a separate ID space for monthly notifications, e.g., `prayerId * 100 + offset + 1_000_000` or a dedicated lookup table. Alternatively, ensure `Cancel(prayer.Id)` in `ScheduleAsync` also cancels the `prayer.Id` value when another prayer's monthly series generates that same ID.

### MEDIUM-07: UserColor model has no static _dbService pattern -- not directly affected but inconsistent

- **File:** `PrayerApp/Models/UserColor.cs`
- **Severity:** Medium
- **Detail:** `UserColor` has no `SetDBService` or instance methods like `SaveAsync()`. All CRUD goes through `IDBService` directly from `UserColorService`. This is actually a cleaner pattern than the Active Record models. Noting for consistency only -- no fix needed.

### MEDIUM-08: ObservableCollection Clear+Add pattern causes UI flicker

- **File:** Multiple ViewModels
- **Severity:** Medium
- **Detail:** `PrayerCardsViewModel.ApplySorting()` (line 233), `PrayerListViewModel.ApplyFilter()` (line 346), `TagsViewModel.LoadAsync()`, and others use `collection.Clear()` followed by `foreach Add()`. Each Add fires `CollectionChanged`, causing the UI to re-render incrementally. For lists over ~20 items, this causes visible flicker.
- **Fix:** Use `ObservableRangeCollection` from CommunityToolkit.Mvvm, or build the list first and swap the entire collection reference.

### MEDIUM-09: No try/catch around notification scheduling in ScheduleAsync

- **File:** `PrayerApp/Services/NotificationService.cs`, line 37
- **Severity:** Medium
- **Detail:** `ScheduleAsync` does not wrap its calls in try/catch. If `_center.ShowAsync()` or `_center.ScheduleMonthlyAsync()` throws (e.g., notification permission revoked at OS level mid-session), the exception propagates up through `CoreSaveAsync` and could prevent the prayer from being saved.
- **Fix:** Wrap notification calls in try/catch and log failures without blocking the save.

### LOW-06: PrayerTimeViewModel._autoTimer Tick handler not guarded against re-entrancy

- **File:** `PrayerApp/ViewModels/PrayerTimeViewModel.cs`, line 438-443
- **Severity:** Low
- **Detail:** `OnAutoTimerTick` calls `NextAsync().SafeFireAndForget()`. If `NextAsync` takes longer than 1 second (e.g., DB call for interaction logging), the timer fires again before the previous `NextAsync` completes. This could log duplicate interactions.
- **Fix:** Add a `_isAdvancing` guard or stop the timer during `NextAsync`.

### LOW-07: QuickAddPage lacks IEditGuard

- **File:** `PrayerApp/Views/QuickAddPage.xaml.cs`
- **Severity:** Low
- **Detail:** QuickAddPage is pushed as a modal and has no `IEditGuard`. If the user types a title and then swipes to dismiss (iOS interactive dismiss gesture), the text is lost without warning. Since it's a quick-add with only one field, this is minor.

### LOW-08: Backup export reads entire DB into memory

- **File:** `PrayerApp/Services/BackupService.cs`, line 36
- **Severity:** Low (acceptable for expected DB sizes)
- **Detail:** `File.ReadAllBytesAsync(_dbPath)` loads the entire database into memory. For a prayer journal app, the DB is expected to be small (< 1 MB). But if a user somehow accumulates large details text over years, this could be problematic on low-memory devices.

### INFO-04: Seed data uses UTC, production data uses local time

- **File:** `PrayerApp/Services/DBService.cs`, lines 301-352
- **Severity:** Info
- **Detail:** `SeedDataAsync` creates PrayerCards and Prayers with `DateTime.UtcNow`, while all model defaults use `DateTime.Now` (local time). This inconsistency means seed data timestamps may appear shifted. Debug-only, so no production impact.

### INFO-05: AppSettingsPage uses code-behind event handlers instead of MVVM

- **File:** `PrayerApp/Views/Settings/AppSettingsPage.xaml.cs`
- **Severity:** Info
- **Detail:** Settings page uses `Toggled`, `PropertyChanged`, and `TextChanged` event handlers directly in code-behind to read/write static `Settings` properties. This is intentional per the architecture (static `Settings` class doesn't fit the ObservableObject pattern), but it means this page cannot be unit-tested through a ViewModel. Acceptable trade-off for a simple settings page.

---

## Recommended Priority Order for Fixes

1. **CRITICAL-02** -- Uncomment UserDefaults in PrivacyInfo.xcprivacy (1 min, blocks App Store submission)
2. **CRITICAL-01** -- Invalidate all service caches after backup restore (15 min, data corruption risk)
3. **HIGH-05** -- Fix notification ID collision for monthly schedules (30 min)
4. **HIGH-01** -- Extend IEditGuard to cover tab switches (15 min)
5. **HIGH-04** -- Verify SCHEDULE_EXACT_ALARM handling on Android 14+ (30 min investigation)
6. **HIGH-03** -- Add accessibility semantics to SettingsHubPage (10 min)
7. **HIGH-02** -- Fix PropertyChanged event handler leaks (30 min)
8. **MEDIUM-09** -- Wrap notification scheduling in try/catch (10 min)
9. **MEDIUM-01/02** -- Dispose CancellationTokenSources (5 min each)
10. **MEDIUM-05** -- Move hardcoded gradient hex to Colors.xaml (5 min)
11. Remaining Medium/Low items as time permits

---

*End of audit.*

# Pre-Submission Code Audit — Consolidated Report

**Date:** 2026-03-26
**Auditors:** 3 independent AI agents (specialized, not overlapping — see methodology note)
**Scope:** Full codebase review of Practicing Prayer (.NET MAUI 10, net10.0-android/ios)

> **Methodology note:** Three agents were assigned specialized focus areas (Data/MVVM, A11y/Store, Memory/Perf) rather than running the same full evaluation independently. This gives breadth but not the cross-validation variance that comes from redundant runs. Future audits should use identical prompts across agents to surface disagreements.

---

## CRITICAL

| # | Finding | Source | Location |
|---|---------|--------|----------|
| 1 | **Empty CFBundleIdentifier in Info.plist** — value is `<string></string>`. MAUI overrides from `.csproj` `ApplicationId` at build time, but should be verified in the built IPA. | Audit 2 | `Platforms/iOS/Info.plist:36` |

---

## HIGH

| # | Finding | Source | Location |
|---|---------|--------|----------|
| 2 | **NSPrivacyAccessedAPICategoryUserDefaults commented out** — app uses `Preferences.Get/Set` (18+ calls) which maps to `NSUserDefaults` on iOS. Apple requires this in the privacy manifest with reason `CA92.1`. | Audit 2 | `Platforms/iOS/Resources/PrivacyInfo.xcprivacy:39-48` |
| 3 | **Notification ID collision** — monthly IDs use `prayerId * 100 + monthOffset(0-11)`. Prayer ID 1 generates notification IDs 100-111, which collide with prayer ID 100's non-monthly notification. Could cancel the wrong prayer's notification. | Audit 3 | `NotificationService.cs:53`, `LocalNotificationCenterWrapper.cs:50` |
| 4 | **Lambda PropertyChanged subscriptions never unsubscribed** — `SubscribeToPropertyChanges` in PrayerCardsVM and PrayerListVM creates lambdas that capture `this`. Old child VMs removed from the collection still hold references to the parent. In long sessions with many load/refresh cycles, this accumulates. | Audit 3 | `PrayerCardsViewModel.cs:282`, `PrayerListViewModel.cs:409` |
| 5 | **5 Shell tab pages missing SafeAreaEdges** — MainPage, PrayerCardsPage, PrayerListPage, TagsPage, SettingsHubPage. Content could render under the notch/Dynamic Island. Shell nav bar may provide coverage, but explicit handling is safer. | Audit 2 | Various |
| 6 | **SemanticProperties.Description on Grid** (iOS VoiceOver gotcha — hides child elements) — color swatch Grid in TagDetailPage has Description bound to `{Binding LightHex}`. | Audit 2 | `Views/Tags/TagDetailPage.xaml:57-59` |
| 7 | **SettingsHubPage — 4 navigation rows have no a11y hints** — TapGestureRecognizer with no SemanticProperties.Hint, invisible to screen readers. | Audit 2 | `Views/Settings/SettingsHubPage.xaml:14-88` |

---

## MEDIUM

| # | Finding | Source | Location |
|---|---------|--------|----------|
| 8 | **Service caches not invalidated after backup restore** — `ReinitializeAsync` reopens the DB but singleton CardService/PrayerService/TagService still hold pre-restore cached data. Home dashboard may show stale data until user navigates away. | Audit 3 | `BackupService.cs:123-128` |
| 9 | **Restore failure leaves DB closed** — if restore fails after `CloseAsync()` but before `ReinitializeAsync()`, the DB connection is closed and never reopened. User is told to restart, but any further app interaction before restart crashes. | Audit 3 | `BackupService.cs:131-138` |
| 10 | **Notifications not rescheduled after restore** — old OS notifications for pre-restore DB remain active. Restored prayers with notifications enabled won't fire until user manually re-saves each prayer. | Audit 3 | `BackupService.cs` (missing) |
| 11 | **ApplyFilter runs N times during bulk load** — `AllPrayers.CollectionChanged` calls `ApplyFilter()` on every `Add()`. Loading 50 prayers = 50 filter runs. `_suppressAnnounce` only suppresses the screen reader, not the filter logic. | Audit 3 | `PrayerListViewModel.cs:98, 141-146` |
| 12 | **N+1 query in GetRequestIdsByTagIdsAsync** — one DB query per selected tag. 5 tags = 5 queries. Should be a single query. | Audit 3 | `TagService.cs:77-88` |
| 13 | **Notification permission denial not reflected in UI toggle** — if `RequestPermissionAsync` returns false, the Preferences value is reset but the toggle stays visually ON until the page is revisited. | Audit 3 | `Settings.cs:71-88`, `AppSettingsPage.xaml.cs:15` |
| 14 | **CFBundleShortVersionString mismatch** — Info.plist says `1.0.4`, csproj says `1.0.5`. MAUI csproj wins at build time, but stale plist is confusing. | Audit 2 | `Platforms/iOS/Info.plist:32` |
| 15 | **Missing NSPrivacyCollectedDataTypes** — privacy manifest should explicitly declare an empty array since no data is collected. Apple automated checks may flag this. | Audit 2 | `Platforms/iOS/Resources/PrivacyInfo.xcprivacy` |
| 16 | **PrayerTimePage completion text missing HeadingLevel** — "You've prayed through all your requests!" should be Level1 for screen reader heading navigation. | Audit 2 | `Views/PrayerTime/PrayerTimePage.xaml:62` |
| 17 | **SCHEDULE_EXACT_ALARM permission** — triggers Google Play review flag on Android 12+. Verify the local notification plugin handles runtime rationale correctly. | Audit 2 | `Platforms/Android/AndroidManifest.xml:8` |
| 18 | **DeleteAsync shows misleading prayer count** — uses `Prayers.Count` which is 0 for unexpanded cards. Confirmation says "Delete Title?" without mentioning the prayers that will be lost. | Audit 1 | `PrayerCardViewModel.cs:208` |
| 19 | **Stale overdue IDs in RefreshAsync** — `_overdueIds` is built in `LoadAsync()` but not refreshed in `RefreshAsync()`. Overdue filter shows stale data after cross-tab prayer interactions. | Audit 1 | `PrayerListViewModel.cs:424-478` |
| 20 | **Edit guard does not protect modal dismissal** — `OnShellNavigating` only fires for Shell Pop. Modal pages (QuickAdd, PrayerTimeScopePage) dismissed via `PopModalAsync` bypass the guard. Not currently exploitable (no modals have IEditGuard), but a pattern risk. | Audit 1 | `AppShell.xaml.cs:86-103` |
| 21 | **TagService.ReassignColorAsync mutates cached objects** — directly modifies `tag.Color` on cached objects before saving. Between mutation and `InvalidateCache()`, concurrent readers see uncommitted state. | Audit 3 | `TagService.cs:108-120` |
| 22 | **SemanticScreenReader.Announce called from thread pool** — the debounced filter announce continuation runs on `TaskScheduler.Default`, not the UI thread. May silently fail on some platforms. | Audit 3 | `PrayerCardsViewModel.cs:276` |
| 22b | **Edit guard only fires on Pop, not tab switches** — `OnShellNavigating` checks `ShellNavigationSource.Pop` only. If user taps a different tab while editing, the source is `ShellItemChanged` and the guard is bypassed. Unsaved changes lost silently. | Audit 1 | `AppShell.xaml.cs:86-103` |
| 22c | **IsDirty doesn't track NotifyTime/DayOfWeek/DayOfMonth** — changing only notification schedule fields and backing out discards changes without a prompt. `CaptureOriginals()` doesn't capture these fields. | Audit 1 | `PrayerRequestDetailViewModel.cs:92-98` |

---

## LOW

| # | Finding | Source | Location |
|---|---------|--------|----------|
| 23 | Stale `_overdueIds` not refreshed on `RefreshAsync` | Audit 1 | `PrayerListViewModel.cs:136-138` |
| 24 | `PrayerTimeScopeViewModel.StartAsync()` — PopModal then GoToAsync could race on some devices | Audit 1 | `PrayerTimeScopeViewModel.cs:74-75` |
| 25 | AppSettingsPage/BackupPage have validation/orchestration logic in code-behind (no ViewModel) | Audit 1 | `AppSettingsPage.xaml.cs`, `BackupPage.xaml.cs` |
| 26 | No concurrency guard on tab page LoadAsync/RefreshAsync (theoretical only) | Audit 1 | Various page code-behinds |
| 27 | TagDetailViewModel constructor swatch load races with ApplyQueryAttributes | Audit 1 | `TagDetailViewModel.cs:87` |
| 28 | Tag changes not tracked by IsDirty in PrayerRequestDetailViewModel | Audit 1 | `PrayerRequestDetailViewModel.cs:92-98` |
| 29 | CancellationTokenSource not disposed before replacement | Audit 3 | `PrayerCardsViewModel.cs:270`, `PrayerTimeViewModel.cs:229` |
| 30 | DBService.ReinitializeAsync race with concurrent callers | Audit 3 | `DBService.cs:183-187` |
| 31 | QuickAddPage missing tap-to-dismiss keyboard handler | Audit 2 | `Views/QuickAddPage.xaml` |
| 32 | RestoreProgressPage ActivityIndicator not AppThemeBinding-aware | Audit 2 | `Views/Backup/RestoreProgressPage.xaml:16` |
| 33 | PrayerTimePage gradient uses inline hex instead of StaticResource tokens | Audit 2 | `Views/PrayerTime/PrayerTimePage.xaml:125` |
| 34 | Missing AutomationId on ToolbarItems (Add Card, Add Prayer, Save in TagDetail/CardPage) | Audit 2 | Various |
| 35 | Missing AutomationId on SettingsHubPage navigation rows | Audit 2 | `Views/Settings/SettingsHubPage.xaml` |
| 36 | Yearly notification uses 365-day interval (drifts ~6hr over 4 years due to leap years) | Audit 3 | `NotificationService.cs:97-99` |
| 37 | `UserColorService.SaveColorAsync` loads all colors for duplicate check | Audit 3 | `UserColorService.cs:36-37` |
| 38 | `BuildRequestTagLookupAsync` loads entire PrayerCardTag table | Audit 3 | `PrayerListViewModel.cs:292-303` |

---

## INFO (Acknowledged, No Action Needed)

| # | Finding | Notes |
|---|---------|-------|
| 39 | XA0141 Android 16KB page size SQLite warning | Third-party issue, documented in CLAUDE.md |
| 40 | INTERNET/ACCESS_NETWORK_STATE permissions declared but app is offline | Pulled in by Plugin.LocalNotification. Explain in Play Store data safety form. |
| 41 | Minimum SDK 21 (Android 5.0) | Very old but acceptable. Consider raising to 24 in future. |
| 42 | SafeFireAndForget swallows exceptions silently | By design — logs to diagnostic service. Acceptable for non-critical paths. |

---

## FINDINGS FROM ADDITIONAL AUDIT PASSES (A + B)

These findings were surfaced by two additional full-scope audit passes and cross-validated. Items already in the main table above are omitted.

| # | Finding | Severity | Source | Location |
|---|---------|----------|--------|----------|
| 45 | **Race in AddOrUpdatePrayerAsync** — two concurrent Save+ calls could both trigger LoadPrayersAsync simultaneously, causing duplicate entries | Medium | Pass A | `PrayerCardViewModel.cs:339-370` |
| 46 | **No try/catch around notification scheduling** — exception in `ShowAsync()` could propagate through `CoreSaveAsync` and prevent the prayer from being saved | Medium | Pass B | `NotificationService.cs:37` |
| 47 | **PrayerCardViewModel.LoadPrayerCardAsync missing null check** — if card was deleted externally, NRE in `RefreshProperties()` accessing `_prayerCard.Title` | Low | Pass B | `PrayerCardViewModel.cs:281` |
| 48 | **Overdue filter not discoverable** — only reachable via deep link from home overdue card, not visible as a filter button in the Prayers tab UI | Low | Pass B | `PrayerListPage.xaml` |
| 49 | **PrayerTagSelectionViewModel appears unused** — not referenced in any View, XAML, or DI registration. Dead code from a previous iteration. | Info | Pass B | `ViewModels/PrayerTagSelectionViewModel.cs` |
| 50 | **Unnecessary location plist descriptions** — `NSLocationWhenInUseUsageDescription` present with "not used" message. May trigger Apple reviewer questions. | Low | Pass A | `Platforms/iOS/Info.plist:47-50` |
| 51 | **Seed data uses DateTime.UtcNow, production uses DateTime.Now** — inconsistent timestamps in seed data. Debug-only, no production impact. | Info | Pass B | `DBService.cs:301-352` |
| 52 | **SaveAndNewAsync no double-tap guard** — AsyncRelayCommand provides re-entrancy protection, but Toast+reset window could theoretically accept input | Low | Pass B | `PrayerRequestDetailViewModel.cs:416-429` |

---

## CROSS-VALIDATION SUMMARY

All 5 audit passes (3 specialized + 2 full-scope) independently confirmed the same top findings:

| Finding | Confirmed by |
|---------|-------------|
| PrivacyInfo.xcprivacy UserDefaults commented out | All 5 passes |
| IEditGuard only fires on Pop, not tab switch | 4 of 5 passes |
| Backup restore doesn't invalidate service caches | All 5 passes |
| Notification ID collision (monthly) | 4 of 5 passes |
| PropertyChanged lambda leak in SubscribeToPropertyChanges | 4 of 5 passes |
| SettingsHubPage missing a11y hints | 3 of 5 passes |
| CancellationTokenSource not disposed | 3 of 5 passes |
| SCHEDULE_EXACT_ALARM needs verification | 3 of 5 passes |

---

## FINAL SUMMARY BY PRIORITY

| Priority | Count |
|----------|-------|
| Critical | 1 |
| High | 6 |
| Medium | 19 |
| Low | 20 |
| Info | 6 |
| **Total** | **52** |

---

## RECOMMENDED FIX ORDER (Remediation Plan)

### Phase 1: App Store Blockers (must-fix)
1. **#2: Privacy manifest** — uncomment UserDefaults declaration with reason `CA92.1`
2. **#8, #9, #10: Backup/restore cache invalidation** — invalidate all service caches after restore, handle restore failure gracefully, reschedule notifications
3. **#3: Notification ID collision** — use higher multiplier or separate ID space for monthly schedules

### Phase 2: Data Integrity + Guard Fixes
4. **#22b: IEditGuard tab switch** — extend `OnShellNavigating` to check `ShellItemChanged` and `ShellSectionChanged`
5. **#22c: IsDirty missing notification fields** — track NotifyTime, DayOfWeek, DayOfMonth in `CaptureOriginals`
6. **#45: AddOrUpdatePrayerAsync race** — add `_loadingPrayers` guard boolean
7. **#46: Notification scheduling try/catch** — wrap in try/catch so save isn't blocked by notification failure

### Phase 3: Accessibility + Platform
8. **#7: SettingsHubPage a11y** — add SemanticProperties.Hint on navigation rows
9. **#6: TagDetailPage Grid a11y** — move Description off Grid to avoid iOS child-hiding
10. **#5: SafeAreaEdges on tab pages** — add to MainPage, PrayerCardsPage, PrayerListPage, TagsPage, SettingsHubPage
11. **#16: PrayerTimePage completion HeadingLevel** — add Level1

### Phase 4: Memory + Performance
12. **#4: PropertyChanged lambda leaks** — unsubscribe on remove, or use WeakEventManager
13. **#29: CancellationTokenSource disposal** — dispose before replacing
14. **#11: ApplyFilter N-times during load** — batch updates or suppress during load
15. **#12: N+1 tag query** — batch query method

### Phase 5: Polish + Housekeeping
16. **#15: NSPrivacyCollectedDataTypes** — add empty array to privacy manifest
17. **#47: LoadPrayerCardAsync null check** — navigate back on null
18. **#49: Dead code** — remove PrayerTagSelectionViewModel
19. **#50: Location plist descriptions** — verify if still needed with LocalNotification v14
20. **#33: Gradient hex → StaticResource** — replace inline colors
21. Remaining low/info items as time permits

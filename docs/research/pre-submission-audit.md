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

## SUMMARY BY PRIORITY

| Priority | Count |
|----------|-------|
| Critical | 1 |
| High | 6 |
| Medium | 15 |
| Low | 16 |
| Info | 4 |
| **Total** | **42** |

---

## RECOMMENDED FIX ORDER

1. **Privacy manifest** (#2, #15) — Apple will reject without UserDefaults declaration
2. **Notification ID collision** (#3) — data corruption risk
3. **SettingsHubPage a11y hints** (#7) — new code we just shipped
4. **SafeAreaEdges on tab pages** (#5) — visual issue on notched devices
5. **Service cache invalidation after restore** (#8, #9, #10) — data integrity
6. **TagDetailPage Grid a11y** (#6) — iOS VoiceOver regression
7. **Info.plist version sync** (#14) — housekeeping
8. **ApplyFilter N-times during load** (#11) — performance
9. Everything else in priority order

# UITest remediation — post-C triage & plan (2026-04-17)

## Post-C full-suite result

`Failed: 29, Passed: 64, Skipped: 3, Total: 96, Duration: 48m 2s`

Vs. prior (pre-B/C): `Failed: 34, Passed: 62, Skipped: 0, Total: 96, Duration: 36m 39s`.

- Net improvement: **-5 fails, +2 passes, +3 skips**.
- Duration went UP by ~11 min — implicit-wait timeouts (5/10/15s × 29 fails) dominate the added time. `TD-15` (NavigateToTab short-circuit) will help once fails drop.

## Expected-vs-actual

| Expected shift | Actual |
|----------------|--------|
| ~10 soft-skip → Skipped | Only 3 skipped. SkippableFact migration landed, so unused — the 7 "should-skip" tests aren't short-circuiting because their precondition *throw* never fires (they fall through into a helper that hard-fails first). |
| ~9 cascade → Passing | Only ~5 cleared. Most "cascade" tests are actually **data-precondition failures** (see RC1/RC2/RC3 below), which ResetAppUIState was never designed to cover. |
| ~15 real regressions | Closer to the truth — after root-causing the three precondition failures, this is where we land. |

## Root-cause clusters

Stack-trace analysis collapses **~20 of 29 fails** into three shared helper failures.

### RC1 — `TapToolbarItem("Add")` on Prayers tab times out (10 tests)

Helper `NavigateToNewPrayer` (`AppExtensions.cs:877`) does a text-based tap on the Prayers tab's "Add" toolbar item. Call fails at `TapToolbarItem` (L567) with `WebDriverTimeoutException` → `NoSuchElementException`.

Toolbar is plain text (`PrayerListPage.xaml:10`: `<ToolbarItem Text="Add" />` — no `IconImageSource`, no `AutomationId`), so a text lookup *should* work. The Cards page uses iconized toolbars w/ AutomationIds after Option A — the Prayers page was untouched.

Affected tests:
- `UnsavedChanges_*` (4)
- `Reminders_*` (3) — via `NavigateToNewPrayerWithReminders`
- `PrayerListTests.Prayers_AddNewPrayer`
- `TagTests.Tags_AddTagToPrayer`
- `PrayerCardTests.Cards_AddPrayerToCard` (similar — uses `WaitAndTap`, fails at `AppExtensions.cs:69/101`)

**Leading hypothesis:** the Prayers tab is occasionally rendering in a state where the `ToolbarItem` is missing — either (a) the page hasn't finished loading when `TapToolbarItem` races in, (b) toolbar items got hidden when filter state persisted across tests, or (c) an MAUI 10 Shell toolbar regression. Evidence-from-code points to (a) or (b).

### RC2 — `Tap("Home_Btn_QuickAdd")` on Home tab times out (5 tests)

Helper `EnsureUITestPrayerExists` (`AppExtensions.cs:744`) fast-paths if `IsTextDisplayed("UI Test Prayer", 3)` returns true on the Prayers tab — otherwise navigates to Home and taps `Home_Btn_QuickAdd`. Fast-path *should* succeed because `TestDataSeed.cs:120` seeds "UI Test Prayer" into "UITest Card".

Affected tests — all `PrayerTimeTests` (5):
- `PrayerTime_NavigationButtons_Present`, `_AutoMode_CyclesInterval`, `_SessionStarts_ShowsCarousel`, `_FinishButton_ExitsPrayerTime`, `_TagScoped_ShowsScopePage`.

**Leading hypothesis:** a prior test leaves the Prayers tab in a filtered state (tag filter, search box, "show favorites only") that hides "UI Test Prayer" from the list, so the fast-path check fails, then the fallback Home→QuickAdd path hits its own issue (either QuickAdd button AutomationId regression or Home tab not actually rendering — the stack trace only tells us the find failed at `MainPage.xaml:21` AutomationId `Home_Btn_QuickAdd`, which still exists). `ResetAppUIState` does not reset Prayers-tab filter UI.

### RC3 — `TapToolbarItemById("Add Card")` on Cards tab times out (partial — 1+ tests)

`EnsureUITestCardExists` (`AppExtensions.cs:860`) uses the new AutomationId path. Affected: `FeatureGapTests.Cards_FavoriteToggle_ChangesState`. May also be the real driver behind `Cards_EmptySearch_ShowsEmptyState`, `EdgeCase_EmptyCardExpand_ShowsAddPrayer`, and `Cards_ArchivedSection_VisibleAndCollapsed`.

**Leading hypothesis:** Cards tab in a filtered/archived-only state where the "Add Card" toolbar item is visually hidden (e.g., select-mode persisted). Toolbar items DO have `IsVisible` mutation code in `PrayerCardsPage.xaml.cs:34–41` — this is a known dynamic-toolbar area.

### Non-cluster: real regressions & product bugs

**Product bug (HIGH):**
- `AndroidTests.HardwareBack_DirtyDetail_ShowsDiscardDialog` — IEditGuard not wired to Android hardware back. Carried over from prior handoff.

**Real regressions (assertion-level, needs investigation):**
- `Boxes_EditCollection_UpdatesName` — Assert.True False
- `PrayerListTests.Prayers_DeletePrayer` / `Prayers_EditPrayer` / `Prayers_CrossTabFreshness` — Assert.True False
- `Cards_ExpandedCard_ShowsActionButtons` — "should show Favorite button" (assertion)
- `Tags_CreateTag_AppearsInList` — "Should return to tag list after saving new tag" (known save-nav regression)

**Uncategorized singletons:**
- `SettingsTests.Settings_Backup_ShowsButtons` — 15s timeout
- `PrayerCardTests.Cards_EditPrayerFromCard` — `TapByText` timeout at L576/589

## Remediation plan

**Order matters — fix shared helpers first, re-run, then triage the residue.**

### Phase 1 — Harden the three precondition helpers (biggest ROI)

1. **`NavigateToNewPrayer` + `EnsureUITestPrayerExists` (RC1/RC2):**
   - Before tapping the toolbar item, `WaitForElement` the page's top-level AutomationId (e.g. the Prayers CollectionView) so we're not tapping before render.
   - Before the `IsTextDisplayed` fast-path in `EnsureUITestPrayerExists`, explicitly clear any active search/tag filter on the Prayers tab (pull down, tap clear, or re-enter the tab).
   - Add an optional `AutomationId="Prayers_ToolbarItem_Add"` to `PrayerListPage.xaml:10` and switch `NavigateToNewPrayer` to `TapToolbarItemById("Prayers_ToolbarItem_Add")` so we stop depending on text lookup for the Add toolbar.

2. **`EnsureUITestCardExists` (RC3):**
   - Before tapping "Add Card", confirm the Cards tab is in normal view (not select-mode, not filtered to archived-only). Call `ResetAppUIState` was state-only — extend with a "return Cards tab filter to default" step for this helper's preflight.

3. **Move test-data-preflight out of tests entirely:** `TestDataSeed` seeds all the fixtures these Ensure* helpers check for. If the seed is correctly applied, the helpers should *never* fall through to the create path. Consider deleting the fallback-create branches and asserting the seed is present — turns silent cascading bugs into loud fixture failures.

### Phase 2 — Product bug

4. **`HardwareBack_DirtyDetail_ShowsDiscardDialog`:** wire `IEditGuard` into Android hardware-back handling. This is a real UX bug that should be fixed independent of test hygiene — see `prayer-app-skills:prayer-app-navigation` for IEditGuard patterns.

### Phase 3 — Individual assertion failures

Run each in isolation after Phase 1 lands:

```powershell
dotnet test PrayerApp.UITests/PrayerApp.UITests.csproj --filter "FullyQualifiedName~Prayers_DeletePrayer"
```

Expected: once preconditions are reliable, some of these will either pass or surface a clear assertion-level bug to fix.

### Phase 4 — TD-15

Land the `NavigateToTab` "already on target" short-circuit (~45–60s/run back). Scheduled per backlog queue position 6.

## Suggested commit sequence (post-fix)

1. `test(ui): stabilize NavigateToNewPrayer + EnsureUITestPrayerExists preconditions`
2. `test(ui): add AutomationId to Prayers page Add toolbar + switch to TapToolbarItemById`
3. `test(ui): fail-fast if seed fixtures missing (delete fallback-create branches)`
4. `fix(nav): wire IEditGuard into Android hardware-back`
5. `perf(ui): NavigateToTab short-circuit when already on target (TD-15)`

## Artifacts

- Raw log: `C:\repos\PrayerApp\uitest-postC.log` (UTF-16)
- UTF-8 copy: `C:\repos\PrayerApp\uitest-postC.utf8.log`
- Prior triage doc: `docs/research/uitest-triage-2026-04-16.md`
- Handoff memory: `C:\Obsidian\Personal\Claude Memory\handoff_2026-04-17_uitest_remediation.md`

# UI Test Triage — 2026-04-16 full suite run

Live triage list. Items added as failures surface. Seeded with known fragilities from the project note.

## Observed failures (this run)

### F1 — `AccessibilityTests.Cards_PrayerRow_HasAccessibleSummary` — FAIL [1m 57s]

- **Error:** `NoSuchElementException: Text 'UITest Card' not found after 3 scrolls.`
- **Where:** [`AccessibilityTests.cs:134`](../../PrayerApp.UITests/Tests/AccessibilityTests.cs) → `ScrollDownToText` → [`AppExtensions.cs:213`](../../PrayerApp.UITests/Helpers/AppExtensions.cs)
- **Diagnosis:**
  - **Bug:** the test's intended skip path (`if (!cardFound) throw SkipException`) is unreachable — `ScrollDownToText` throws on exhaustion, never returns.
  - **Root cause:** seed data ("UITest Card") not present or not on Prayer Cards page when the test ran.
- **Fix plan:**
  - [ ] **A.** Add `TryScrollDownToText` returning `bool` to `AppExtensions`. Use it here.
  - [ ] **B.** Audit all `ScrollDownToText`/`ScrollDownTo` call sites for the same pattern.
  - [ ] **C.** Harden `EnsureUITestPrayerExists` — verify the card is visible on Prayer Cards before returning (expand box if collapsed; scroll into view).

### F2 — `AccessibilityTests.Cards_CardHeader_AnnouncesExpandCollapseState` — FAIL [15s]

- **Error:** `Assert.NotNull() Failure: Value is null` at [`AccessibilityTests.cs:101`](../../PrayerApp.UITests/Tests/AccessibilityTests.cs)
- **Diagnosis:** the loop at 93-100 probes for "UITest Card", "Test Card", "Delete Me Card" — none found. Same root cause as F1: **no test card is accessible on the Prayer Cards page**.
- **Fix plan:** covered by F1.C. Once `EnsureUITestPrayerExists` is firm, this test's probe succeeds on "UITest Card".
- **Cross-cut:** F1 + F2 both symptoms of the same seed/visibility issue.

### F3 — `AccessibilityTests.Cards_ToolbarItems_HaveHints` — FAIL [14s]

- **Error:** `"Collections toolbar item should be visible on Android"`
- **Suspicion (HIGH PRIORITY):** this may be a **real regression** from the recent toolbar mutate-in-place work (commit `56edb45`) or the Option D revert (`59b3df0`). The ToolbarItem `Cards_Btn_Collections` is present but possibly disabled/hidden when the test queries it.
- **Hypothesis:** app is stuck in multi-select mode from a prior test (`ApplyMultiSelectToolbarState` sets `collectionsItem.IsEnabled = false`). Or Collections was removed from `ToolbarItems.Clear()` by accident.
- **Fix plan:**
  - [ ] **Verify manually** that Collections toolbar item is visible on a fresh cold launch of 1.2.3b58.
  - [ ] If visible in manual testing → test issue: ensure we're not in multi-select mode before asserting.
  - [ ] If not visible → real regression in the toolbar state code. Audit `PrayerCardsPage.xaml` toolbar declarations + `ApplyMultiSelectToolbarState`.

### F4 — `AndroidTests.HardwareBack_NavigatesFromSubPages` — FAIL [18s]

- **Error:** `WebDriverTimeoutException : Timed out after 5 seconds` on `WaitForElement("AppSettings_Switch_Notifications")`
- **Where:** [`AndroidTests.cs:26`](../../PrayerApp.UITests/Tests/AndroidTests.cs) → [`AppExtensions.cs:69`](../../PrayerApp.UITests/Helpers/AppExtensions.cs) `WaitForElement`
- **Diagnosis:** tapped `Settings_Row_AppSettings` successfully, but `AppSettings_Switch_Notifications` never appeared.
- **Possible causes:**
  - AppSettings page changed — switch got renamed or removed.
  - Page didn't load (navigation failed silently).
  - App is in a weird state (stuck on a modal or a wrong page).
- **Fix plan:**
  - [ ] Verify `AppSettings_Switch_Notifications` still exists in `AppSettingsPage.xaml`.
  - [ ] Add a pre-condition assertion that we're actually on AppSettings before waiting for the switch.
  - [ ] Consider: is F4 cascade from F3's app-state drift?

### F5 — `AndroidTests.HardwareBack_DirtyDetail_ShowsDiscardDialog` — FAIL [23s]

- **Error:** `"Hardware back with unsaved changes should show discard dialog or stay on page"`
- **Where:** [`AndroidTests.cs:52`](../../PrayerApp.UITests/Tests/AndroidTests.cs)
- **Diagnosis:** after typing in `Detail_Entry_Title` and pressing back, neither the discard dialog fired nor did the page stay open → the app navigated away without prompting. **IEditGuard integration might not be firing for hardware back on Android.**
- **Possible causes:**
  - `IEditGuard.CanLeaveAsync` not wired to hardware back.
  - `IsDirty` not flipping fast enough before back is pressed (500ms delay may be insufficient).
  - Real regression in `AppShell.OnShellNavigating` handling of hardware back.
- **Fix plan:**
  - [ ] Manual repro: type in a new prayer title, press hardware back. Should show discard.
  - [ ] If manual repro matches test → real IEditGuard regression (high priority).
  - [ ] If manual repro shows dialog → timing issue, increase `Thread.Sleep` or poll for IsDirty.

---

## Pattern spotting (so far)

| Pattern | Failures | Severity |
|---------|----------|----------|
| Missing seed / visibility ("UITest Card" not there) | F1, F2 | Infra bug |
| **Possible regression**: Collections toolbar missing | F3 | **HIGH — product** |
| **Possible regression**: IEditGuard on hardware back | F5 | **HIGH — product** |
| Cascading nav / state drift after prior tests | F4 (maybe cascade from F3) | Infra / ordering |

**F3 and F5 are the ones to worry about.** They look like real app regressions, not test flakiness. F1, F2, F4 are more likely infra/ordering.

---

## Wave 2 (00:04:38 → 00:06:17)

All four of F6–F9 share the **same call stack**: `TapToolbarItem` at [`AppExtensions.cs:508`](../../PrayerApp.UITests/Helpers/AppExtensions.cs) — a 10s timeout on `FindElement(TextLocator(text))` for "Collections". On Android the helper literally searches the UI tree for the text "Collections".

### F6 — `BoxTests.Boxes_DeleteCollection_DeleteAllCards` [18s] @ 00:05:07
- Call stack: `TapToolbarItem` ← `EnsureUITestCollectionExists` (line 736) ← test (line 327)

### F7 — `BoxTests.Cards_Search_ExpandsMatchingSections` [22s] @ 00:05:41
- Call stack: `TapToolbarItem` ← `EnsureUITestCardExists` (line 801) ← test (line 436)

### F8 — `BoxTests.Boxes_CreateCollection_AppearsInList` [17s] @ 00:05:59
- Call stack: `TapToolbarItem` ← test (line 107) direct

### F9 — `BoxTests.Cards_CollectionsToolbar_NavigatesToBoxesPage` [18s] @ 00:06:17  ← **LAST LOGGED AT MARKER**
- Call stack: `TapToolbarItem` ← test (line 66) direct

### Rolled-up hypothesis

F3 + F6 + F7 + F8 + F9 are **one regression**: the "Collections" toolbar item's text label is not appearing in the Android UI tree. UiAutomator2 can't find it by text, so every `TapToolbarItem("Collections")` times out at 10s.

Prime suspect: commit `56edb45 feat(cards): reserved check slot in multi-select + toolbar mutate-in-place`.

**Ken's hypothesis:** the toolbar mutate-in-place refactor may have undone accessibility/testability attributes or property bindings that UiAutomator2 relied on. **Needs git archaeology before planning any fix.**

---

## Wave 3 (00:06:17 → 00:12:44)  — **all same regression as Wave 2**

Nine more failures, every single one with `TapToolbarItem` at [`AppExtensions.cs:508`](../../PrayerApp.UITests/Helpers/AppExtensions.cs) in the stack. All timed out at 10s searching for text "Collections".

| # | Test | Time | Entry |
|---|------|------|-------|
| F10 | `BoxTests.Boxes_SystemCollections_NoDeleteAction` | 00:07:10 | TapToolbarItem direct |
| F11 | `BoxTests.Cards_CreateCard_WithCollectionPicker` | 00:07:28 | TapToolbarItem direct |
| F12 | `BoxTests.Cards_MultiSelect_MoveToCollection` | 00:07:45 | via `EnsureUITestCollectionExists` |
| F13 | `BoxTests.Cards_EmptyCollection_ShowsHintText` | 00:08:19 | via `EnsureUITestCollectionExists` |
| F14 | `BoxTests.Boxes_DeleteCollection_UnassignCards` | 00:08:37 | via `EnsureUITestCollectionExists` |
| F15 | `BoxTests.Boxes_EditCollection_UpdatesName` | 00:08:54 | via `EnsureUITestCollectionExists` |
| F16 | `EdgeCaseTests.EdgeCase_EmptyCardExpand_ShowsAddPrayer` | 00:10:12 | TapToolbarItem direct |
| F17 | `FeatureGapTests.Cards_FavoriteToggle_ChangesState` | 00:12:06 | via `EnsureUITestCardExists` |
| F18 | `FeatureGapTests.PrayerTime_ByCollection_ShowsScopePage` | 00:12:44 | via `EnsureUITestCollectionExists` |

All roll up under the **Collections toolbar text regression** root cause below.

---

## Root cause analysis (subagent report — `56edb45` archaeology)

**What changed in `56edb45`:**
- Toolbar items in `PrayerCardsPage.xaml` kept `Text="Collections"`, `Text="Select"`, `Text="Add Card"` — not removed.
- **Added `IconImageSource` to each** (`folder_regular_full.png`, `list_check_solid_full.png`, `plus_solid_full.png`).
- Code-behind introduced `ApplyMultiSelectToolbarState` — runs only on `PropertyChanged`, not at initial load. XAML initial state is untouched.

**How MAUI Shell Android renders this:**
- `ToolbarItem` with **both** `Text` and `IconImageSource` renders as `ActionMenuItemView` with `showAsAction="ifRoom"`.
- When it fits on the action bar (3 items → fits), only the **icon** shows on screen; `Text` becomes the tooltip/accessibility label, **not a visible TextView**.
- Before `56edb45`: no icon → Shell rendered text-only action bar buttons → visible `TextView text="Collections"`.
- After `56edb45`: icon present → icon-only `ImageButton` → no `TextView.text="Collections"` in the tree.

**Test-side:** `TextLocator` builds an XPath `//*[@text='Collections' or @content-desc='Collections']`. Tests pass when either the `@text` or `@content-desc` node matches. After `56edb45`:
- `@text` no longer has "Collections" (icon-only, no TextView).
- `@content-desc` is set to the `SemanticProperties.Hint` value, which is **"Manage collections"** — not "Collections". Same for Add Card (`content-desc="Creates a new prayer card"` not "Add Card"). That's why the XPath misses.

**Theories ranked by subagent (verbatim summary):**
1. Icons replaced visible text on the action bar — `Text` lives only as tooltip, not in the UiAutomator2 tree as `@text`. (Most likely.)
2. `SemanticProperties.Hint` overrides `Text` as the content-desc on Android, so `@content-desc` is the hint sentence, not the short label. (Very likely, stacks with #1.)
3. Three icons on a narrow screen could push one item to the overflow `⋮` menu, hiding it entirely. (Possible but unlikely to affect all three.)
4. *Ruled out:* `ApplyMultiSelectToolbarState` clobbering initial state — it only runs on `PropertyChanged`.

**Net:** `56edb45` gave the Shell icons, which is correct UX, but inadvertently broke the assumption tests made that `@text="Collections"` (or `@content-desc="Collections"`) would exist in the tree. **Not a revertable-as-is regression** — we don't want to go back to text-only toolbar buttons on Android.

---

## Manually observed (not from test log)

### M2 — Test emulator has no user-created Collections → missing-data assertions fail

- **Observed by Ken, 2026-04-16.** The test app on the emulator has zero user-created Collections (only the default system boxes: Quick Add / Loose Cards / Answered). Several "independent" failures may actually be missing-data issues.
- **Suspect failures likely caused or aggravated:**
  - **F18** `PrayerTime_ByCollection_ShowsScopePage` — if there are no user collections, the scope page has nothing to show; test probably asserts the page navigated successfully *and* shows a list, and the list is empty.
  - **F31** `Prayers_CrossTabFreshness` — if no card has a prayer, cross-tab freshness has nothing to verify.
  - Possibly **F25, F27, F29** — the PrayerListTests cluster — if no prayers exist at all, some assertions fail for data reasons, not rendering reasons.
- **Root cause:** there's no baseline test-data seed. Tests rely on prior tests / manual setup to leave data behind. When the emulator is fresh or a destructive test ran recently, later tests start with nothing.
- **Proper fix:** seed a **test-specific SQLite DB** before the suite starts, pushed via the `run-as` + `adb push` pattern (already documented in `feedback_android_sqlite_seeding`; also used for `screenshots/android/prayer_app_seed.db`). Contents:
  - 2–3 user Collections with varied names (e.g., "Family", "Work", "Friends")
  - Each with 2–3 Prayer Cards, each with 1–3 Prayer Requests
  - At least one Answered prayer
  - At least one Favorite
  - Tags attached where reasonable
  - Deterministic IDs / timestamps for reproducibility
- **Wire-in point:** `AppiumSetup` constructor (suite-level fixture). Before any test runs, push the seed DB to the app's data dir and force-stop / restart the process so the app re-reads it. Optional: detect "already seeded" via a marker row so repeat suite runs don't re-push.
- **Prevents future regressions of this kind** — covers K1 (shared mutable DB state) and K2 (name accumulation) from the pre-identified fragility list.
- **Scope decision:** This is a **separate commit** from the Collections toolbar fix. Do the toolbar fix first (unblocks most tests), then land the seed-DB infra, then re-run to see what's genuinely broken.

---

### M1 — Entry forms don't auto-focus the first field on load

- **Observed by Ken, 2026-04-16 during suite run.** New Prayer Card page opens without keyboard focus on the Title field. User has to tap the field to start typing.
- **Expected:** every entry/form page focuses its first editable field on load (Title, Name, etc.) so the keyboard comes up immediately.
- **Scope to audit (all pages with a primary input on load):**
  - `PrayerCardPage` — Title entry (known failing)
  - `PrayerDetailPage` — Title entry
  - `TagDetailPage` — Name entry
  - `QuickAddPage` — Title entry (this one probably already works since it's a modal-of-intent)
  - `PrayerTimeScopePage` — if there's an entry field
  - Any other form-style page in `Views/**`
- **Implementation pattern:** MAUI `Entry.Focus()` called in `OnAppearing` (with a short delay on Android to let the handler fully attach). May need platform-specific handling — iOS is usually fine with immediate `.Focus()`; Android can require `Dispatcher.DispatchDelayed(...)` because of keyboard raise timing.
- **Accessibility note:** if a screen reader is active, auto-focus should still work (VoiceOver/TalkBack handle focus-with-keyboard gracefully), but verify.
- **Do after** the Collections regression fix lands — this is a product improvement, not a test-suite blocker.

---

## Wave 4 (00:12:44 → 00:18:27)

| # | Test | Time | Dur | Category |
|---|------|------|-----|----------|
| F19 | `PrayerCardTests.Cards_EditPrayerFromCard` | 00:15:45 | 31s | Same Collections regression (inner `TapToolbarItem` chain → `FindByText`/`TapByText`) |
| F20 | `PrayerCardTests.Cards_DeleteCard_RemovesFromList` | 00:16:38 | 17s | Same Collections regression |
| F21 | `PrayerCardTests.Cards_ExpandedCard_ShowsActionButtons` | 00:17:01 | 22s | Same — via `EnsureUITestCardExists` |
| **F22** | `PrayerCardTests.Cards_SearchKeyboard_DismissesOnBackground` | 00:17:15 | 13s | **NEW / INDEPENDENT** — `Assert.True() Failure` at `PrayerCardTests.cs:100`. Search keyboard not dismissed on background tap. |
| F23 | `PrayerCardTests.Cards_CreateCard_AppearsInList` | 00:18:27 | 1m 12s | Same Collections regression |

### F22 — `Cards_SearchKeyboard_DismissesOnBackground` — **needs separate triage**

- **Error:** `Assert.True() Failure — Expected: True, Actual: False`
- **Where:** `PrayerCardTests.cs:100` — direct assertion, no helper.
- **Likely meaning:** test tapped on the page background expecting the search bar to unfocus / keyboard to dismiss; it didn't.
- **Possible causes:**
  - Real behavioral regression: `OnBackgroundTapped` handler in `PrayerCardsPage.xaml.cs` not firing or not unfocusing `searchBar`.
  - Timing / emulator soft-keyboard quirk.
  - Test expects a state change that was re-keyed under a different AutomationId in recent work.
- **Defer:** triage after Collections fix lands; re-run and see if still fails in isolation.

---

## Failure tally so far (through 00:18:27)

- **Collections toolbar regression (one root cause):** F3, F6, F7, F8, F9, F10, F11, F12, F13, F14, F15, F16, F17, F18, F19, F20, F21, F23 — **18 tests**
- **Likely collateral from Collections regression:** F1, F2 (test card seed assumed present; `EnsureUITestCardExists` goes via Collections toolbar) — **2 tests**
- **Possibly collateral from app state drift:** F4 (AppSettings nav timeout after earlier failures left app mid-flow) — **1 test**
- **Independent, needs triage:** F5 (HardwareBack discard dialog), F22 (Search keyboard dismiss) — **2 tests**

**~21 of the failures collapse to one fix.** ~2–3 are genuinely independent and we'll triage them after.

---

## Wave 5 (00:18:27 → 00:22:19)

| # | Test | Time | Dur | Category |
|---|------|------|-----|----------|
| F24 | `PrayerCardTests.Cards_EditButton_NavigatesToEditPage` | 00:19:01 | 22s | Same Collections regression (via `EnsureUITestCardExists`) |
| F25 | `PrayerListTests.Prayers_FilterButtons_SwitchViews` | 00:20:16 | 19s | **DIFFERENT — assertion fail** at `List_Filter_Active` not displayed |
| F26 | `PrayerListTests.Prayers_MarkAnswered` | 00:20:34 | 17s | Same regression (via `NavigateToNewPrayer` → `TapToolbarItem`) |
| F27 | `PrayerListTests.Prayers_PageLoads` | 00:20:47 | 13s | **DIFFERENT — assertion fail** at `List_Filter_Active \|\| List_Search_Prayers` not displayed |
| F28 | `PrayerListTests.Prayers_DeletePrayer` | 00:21:05 | 17s | Same regression (via `NavigateToNewPrayer`) |
| F29 | `PrayerListTests.Prayers_EditPrayer` | 00:21:32 | 27s | **DIFFERENT — assertion fail** at `PrayerListTests.cs:166` |
| F30 | `PrayerListTests.Prayers_AddNewPrayer` | 00:21:50 | 17s | Same regression (via `NavigateToNewPrayer`) |
| F31 | `PrayerListTests.Prayers_CrossTabFreshness` | 00:22:19 | 29s | **DIFFERENT — assertion fail** at `PrayerListTests.cs:259` |

### New finding: `NavigateToNewPrayer` also uses text-based toolbar lookup

Line 818 in `AppExtensions.cs` calls `TapToolbarItem` — so the Collections regression cascades through **both** `EnsureUITestCollectionExists` (line 736) and `NavigateToNewPrayer` (line 818). Any toolbar text the helper searches for is broken by `56edb45`'s icon change. Scope of the fix is the helper itself, not the specific string.

### F25, F27, F29, F31 — PrayerListTests assertion cluster

All four use **AutomationId-based lookups** (`List_Filter_Active`, `List_Search_Prayers`, etc.), so they are **NOT** the same locator-text regression. Two possibilities:

1. **Cascade from app-state drift.** Earlier tests died mid-interaction on PrayerCards (toolbar failures), leaving the app in an odd state (modal open, multi-select stuck, dialog unclosed). `EnsureOnTab("Prayers", _setup)` is the only setup call — it may not recover cleanly.
2. **Independent regression on the Prayers page.** Something about the PrayerList page is rendering without expected AutomationIds in the tree.

**Defer:** confirm after Collections fix lands. A clean re-run with all Collections tests passing will show whether these four fail in isolation (real regression) or clear up (cascade).

---

## Running failure tally (through 00:22:19)

- **Collections toolbar regression (one root cause):** F3, F6–F21, F23, F24, F26, F28, F30 — **22 tests**
- **Collateral suspects (cleared by Collections fix):** F1, F2, maybe F4 — **2–3 tests**
- **Independent / needs isolated triage:** F5, F22, F25, F27, F29, F31 — **6 tests**

About **24 of 30 failures** collapse to the one fix. **6 are genuinely separate** and need individual attention after the cascade clears.

---

## Wave 6 (00:22:19 → 00:35:51 — FINAL)

| # | Test | Time | Dur | Category |
|---|------|------|-----|----------|
| F32 | `PrayerTimeTests.PrayerTime_NavigationButtons_Present` | 00:23:50 | 1m 31s | **LIKELY COLLATERAL** — "Could not start Prayer Time session" (dynamic skip firing as fail) |
| F33 | `PrayerTimeTests.PrayerTime_AutoMode_CyclesInterval` | 00:25:55 | 1m 35s | Same — Could not start Prayer Time session |
| F34 | `PrayerTimeTests.PrayerTime_SessionStarts_ShowsCarousel` | 00:27:27 | 1m 31s | Same |
| F35 | `PrayerTimeTests.PrayerTime_FinishButton_ExitsPrayerTime` | 00:28:58 | 1m 31s | Same |
| F36 | `ReminderTests.Reminders_ToggleOff_HidesPickers` | 00:30:20 | 18s | Collections regression (via `NavigateToNewPrayer`) |
| F37 | `ReminderTests.Reminders_ToggleOn_ShowsPickers` | 00:30:38 | 17s | Same |
| F38 | `ReminderTests.Reminders_FrequencyPicker_HasOptions` | 00:30:55 | 18s | Same |
| F39 | `TagTests.Tags_CommaAutoSave` | 00:32:43 | 18s | Collections regression (via `NavigateToNewPrayer`) |
| F40 | `TagTests.Tags_AddTagToPrayer` | 00:33:00 | 18s | Same |
| **F41** | `TagTests.Tags_CreateTag_AppearsInList` | 00:33:40 | 19s | **INDEPENDENT** — "Should return to tag list after saving new tag" at `TagTests.cs:51` |
| F42 | `UnsavedChangesTests.UnsavedChanges_NoChanges_BackNoPrompt` | 00:34:59 | 17s | Collections regression (via `NavigateToNewPrayer`) |
| F43 | `UnsavedChangesTests.UnsavedChanges_SaveThenBack_NoPrompt` | 00:35:17 | 17s | Same |
| F44 | `UnsavedChangesTests.UnsavedChanges_EditTitle_BackShowsDiscardDialog` | 00:35:34 | 17s | Same |
| F45 | `UnsavedChangesTests.UnsavedChanges_EditTitle_TabSwitchShowsDiscardDialog` | 00:35:51 | 17s | Same |

### New finding: PrayerTime tests surface `$XunitDynamicSkip$` messages as FAIL

F32–F35 all error with `$XunitDynamicSkip$Could not start Prayer Time session` — that's the project's own pattern for soft-skipping when Prayer Time can't start (K5 in the pre-identified fragility list). xUnit in the test runner configuration is surfacing these as FAIL instead of SKIP. Two things to fix:
- Real cause: Prayer Time start fails because there's nothing to pray for (no seed data) OR navigation to Prayer Time is broken. **M2 (seed DB) very likely unblocks these.**
- Process bug: the dynamic-skip isn't being properly recognized. Investigate after real tests pass.

### New finding: F41 `Tags_CreateTag_AppearsInList` is independent

Error: `"Should return to tag list after saving new tag"` at `TagTests.cs:51`. No helper in stack — plain assertion. Either:
- Save navigation regression
- Real return-to-list bug
- Assertion looking for a control that moved

Needs isolated triage.

---

## FINAL SUITE SUMMARY (00:35:51)

```
Failed: 45, Passed: 51, Skipped: 0, Total: 96, Duration: 35m 39s
```

### Final categorization (F1–F45)

| Category | Count | Tests | Expected resolution |
|----------|-------|-------|---------------------|
| **Collections toolbar regression** | **31** | F3, F6–F21, F23, F24, F26, F28, F30, F36–F40, F42–F45 | **Commit A** |
| **Collateral — missing/insufficient seed data** | **6** | F1, F2, F32, F33, F34, F35 | **Commit B** (seed DB) |
| **Genuinely independent — triage after A+B** | **8** | F4, F5, F22, F25, F27, F29, F31, F41 | Separate commits after clean baseline |

**37 of 45 failures (~82%) collapse to our two planned commits.** 8 genuinely independent will need individual attention after the cascade clears.

### The 8 independent failures (preview for post-fix triage)

| # | Test | Nature |
|---|------|--------|
| F4 | `AndroidTests.HardwareBack_NavigatesFromSubPages` | Timeout on `AppSettings_Switch_Notifications` — stale AutomationId or nav issue |
| F5 | `AndroidTests.HardwareBack_DirtyDetail_ShowsDiscardDialog` | IEditGuard may not be wired to Android hardware back |
| F22 | `PrayerCardTests.Cards_SearchKeyboard_DismissesOnBackground` | Search bar not unfocusing on background tap |
| F25 | `PrayerListTests.Prayers_FilterButtons_SwitchViews` | `List_Filter_Active` not displayed after taps |
| F27 | `PrayerListTests.Prayers_PageLoads` | `List_Filter_Active \|\| List_Search_Prayers` missing |
| F29 | `PrayerListTests.Prayers_EditPrayer` | Assertion fail (PrayerListTests.cs:166) |
| F31 | `PrayerListTests.Prayers_CrossTabFreshness` | Assertion fail (PrayerListTests.cs:259) |
| F41 | `TagTests.Tags_CreateTag_AppearsInList` | "Should return to tag list after saving new tag" |

Some of these 8 may still be cascades that clear with A+B. We'll know on re-run.

---

## Nothing new and crazy

Suite run confirmed the pattern. One root-cause regression (toolbar text-based locator), significant cascading, and a predictable set of independents. No surprises outside the shapes we already identified. Ready to execute Commit B (seed) → Commit A (toolbar fix) → re-run.

---

## Pre-identified fragility (from project note — investigate during triage)

### K1 — Shared mutable DB state
- No DB reset between tests. Data accumulates across runs → name collisions, stale cards, unbounded emulator state.
- **Options:** per-test-class cleanup, per-suite DB seed, or teardown that removes "UI Test *" prefixed data.

### K2 — Name accumulation
- "UI Test Prayer", "CommaTagA/B", "Empty Card Test" etc. pile up on the emulator.
- **Options:** GUID-suffixed names, or aggressive teardown.

### K3 — Long-press silently skips on Android emulators
- `BoxTests 8.10` and `8.12` silently skip because `longClickGesture` doesn't fire → zero coverage on emulator runs.
- **Options:** native `adb shell input swipe x y x y 1000` fallback, or actionable skip message so CI notices.

### K4 — BoxTests ordering dependency
- `8.8` deletes "UITest Collection"; `8.11` depends on it existing. `EnsureUITestCollectionExists` saves it but is fragile.
- **Options:** explicit setup in `8.11`, or ordering-independent teardown.

### K5 — Prayer Time session recovery masks regressions
- `TryStartPrayerTime` returns `false` and skips instead of asserting failure. A real regression looks like "just a skip."
- **Options:** fail hard if `TryStartPrayerTime` returns false; or emit a test warning artifact.

### K6 — Name-brittle assertions (SAME AS F1)
- `AccessibilityTests 15.3` and `15.4` hardcode "UITest Card" / "UI Test Prayer".
- Already rolled up into F1's fix plan (items B + C).

---

## Cross-cutting themes (emerging)

| Theme | Items | Proposed single fix |
|-------|-------|---------------------|
| Helpers throw where callers expect bool | F1 | Add `TryScrollDown*` variants; audit all `ScrollDown*` call sites |
| Ensure* helpers have weak postconditions | F1, K4 | Tighten `EnsureUITest*Exists` to verify visibility, not just DB existence |
| Shared DB state | K1, K2, K6 | Per-test DB reset OR GUID-suffixed names |
| Silent skips masking regressions | K3, K5 | Policy: skips must be logged with a reason the reviewer can scan |

---

## Triage outcome plan (after suite completes)

1. Group failures by theme (table above).
2. For each theme, one focused fix → one commit.
3. Rerun suite per commit to verify no regressions.
4. Update `BACKLOG.md` with any themes we defer.

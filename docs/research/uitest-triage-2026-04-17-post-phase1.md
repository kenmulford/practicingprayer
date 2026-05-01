# UITest failures — post-Phase-1 triage (2026-04-17)

Data source: `uitest-phase1.log` (run ending 09:09, duration 38m 35s, 31 fails / 62 pass / 3 skip / 96 total, 0 UiAutomator2 instrumentation crashes).

Extraction script: `scripts/triage-failures.py`.

## Summary by cluster

| Cluster | Count | Root-cause candidate | Confidence |
|---|---|---|---|
| I — `NavigateToNewPrayer` timeout (`TapToolbarItem("Add")` text-lookup on Prayers tab) | 9 | Shell ToolbarItem text-tap fails intermittently; some callers pass, some fail. **Cascade-ordering-dependent.** | Medium |
| II — `EnsureUITestPrayerExists` / `EnsureUITestCollectionExists` timeout on `WaitForElement("List_List_Prayers" or "Boxes_List_Boxes")` | 6 | Prayers tab or Collections modal not rendered within 10 s — strong cascade signal (prior test left the app on a detail page or modal) | Medium-High |
| III — `TapByText` or `WaitAndTap` timeout on Cards surface (off-screen item) | 4 | CollectionView virtualization — same issue Phase 1 exposed, now manifesting at test-level instead of Ensure*-level | High |
| IV — Assert.True() Failure on post-action navigation check | 4 | Real product/nav regression — page does not return to expected list state after an edit/delete/tab-switch | Medium |
| V — Custom assertion failures (domain semantic) | 4 | Real product bugs at UI/ViewModel level | High |
| VI — Miscellaneous element-not-found singletons | 4 | Mix — could be cascade (singletons in unlucky positions) or real locator drift | Low |

## Cluster I — `NavigateToNewPrayer` fails (9 tests)

Error signature: `WebDriverTimeoutException : Timed out after 10 seconds` inside `TapToolbarItem("Add")` at `AppExtensions.cs:567`.

| Test | Location | xUnit time |
|---|---|---|
| PrayerListTests.Prayers_AddNewPrayer | PrayerListTests.cs:84 | — |
| ReminderTests.Reminders_FrequencyPicker_HasOptions | ReminderTests.cs:52 | — |
| ReminderTests.Reminders_ToggleOff_HidesPickers | ReminderTests.cs:100 | — |
| ReminderTests.Reminders_ToggleOn_ShowsPickers | ReminderTests.cs:35 | — |
| TagTests.Tags_AddTagToPrayer | TagTests.cs:129 | — |
| UnsavedChangesTests.UnsavedChanges_EditTitle_BackShowsDiscardDialog | UnsavedChangesTests.cs:32 | — |
| UnsavedChangesTests.UnsavedChanges_EditTitle_TabSwitchShowsDiscardDialog | UnsavedChangesTests.cs:75 | — |
| UnsavedChangesTests.UnsavedChanges_NoChanges_BackNoPrompt | UnsavedChangesTests.cs:112 | — |
| UnsavedChangesTests.UnsavedChanges_SaveThenBack_NoPrompt | UnsavedChangesTests.cs:93 | — |

**Counter-evidence (tests that succeed with the same helper):** `HardwareBack_FromPrayerDetail`, `Prayers_MarkAnswered`, `Prayers_DeletePrayer` (reaches L250 assertion), `Tags_CommaAutoSave`, `Tags_RemoveTagInPicker` — all call `NavigateToNewPrayer` and proceed past it successfully in the same run.

**Implication:** the helper is not fundamentally broken. The difference between success and failure is **prior test state**. ReminderTests, UnsavedChangesTests, PrayerListTests.Prayers_AddNewPrayer all happen to be the **first tests in their class** that call `NavigateToNewPrayer`, and the test immediately before them typically ends in a mid-edit or modal state that `ResetAppUIState` (alerts + multi-select only) can't recover from.

**Investigation to confirm (Step 5 Appium Inspector):**
1. Reproduce one failing case (e.g. `UnsavedChanges_EditTitle_BackShowsDiscardDialog`).
2. Immediately before `NavigateToNewPrayer` is called, inspect the page tree: is the app actually on the Prayers tab, or is it still on a PrayerDetail page from the prior test?
3. If it's on DetailPage, the fix is either (a) harden `ResetAppUIState` to handle detail-page escape, or (b) reorder tests so no cross-class contamination occurs.

## Cluster II — `EnsureUITestPrayerExists` / `EnsureUITestCollectionExists` WaitForElement timeout (6 tests)

Post-Phase-1 Option A, these helpers are just `EnsureOnTab + WaitForElement(list_id, 10)`. They now time out on the `WaitForElement` step.

| Test | Location | Waiting for |
|---|---|---|
| PrayerTimeTests.PrayerTime_AutoMode_CyclesInterval | PrayerTimeTests.cs:35 | List_List_Prayers |
| PrayerTimeTests.PrayerTime_FinishButton_ExitsPrayerTime | PrayerTimeTests.cs:35 | List_List_Prayers |
| PrayerTimeTests.PrayerTime_NavigationButtons_Present | PrayerTimeTests.cs:35 | List_List_Prayers |
| PrayerTimeTests.PrayerTime_SessionStarts_ShowsCarousel | PrayerTimeTests.cs:35 | List_List_Prayers |
| PrayerTimeTests.PrayerTime_TagScoped_ShowsScopePage | PrayerTimeTests.cs:273 | List_List_Prayers |
| FeatureGapTests.PrayerTime_ByCollection_ShowsScopePage | FeatureGapTests.cs:136 | Boxes_List_Boxes (via EnsureUITestCollectionExists) |

**All 5 PrayerTimeTests fail at line 35** — the shared `TryStartPrayerTime()` entry. Line 35 is `driver.EnsureUITestPrayerExists(_setup)`, which in Option A reduces to `EnsureOnTab("Prayers") + WaitForElement("List_List_Prayers", 10)`.

The 10-second wait for the CollectionView to appear on Prayers tab is failing. Either:
- The app is not actually on the Prayers tab (EnsureOnTab succeeded at tapping the tab in the tab bar, but Shell actually routed elsewhere — possibly because the prior test left PrayerTime page open, which HIDES the tab bar).
- The tab is right but the list is taking longer than 10s on some transitions.

**Investigation:** Inspector + stepped reproduction. Once on the failing sequence, use `adb logcat` alongside Inspector to see MAUI binding errors or navigation events.

**Likely fix (pending evidence):** harden `EnsureOnTab` when landing on Prayers — if WaitForElement times out, fall back to `driver.Navigate().Back()` (or ActivateApp) and retry. Currently `NavigateToTab` has a 3-stage escalation but doesn't include "am I actually on the right page once the tab-bar tap returned" check.

## Cluster III — `TapByText`/`WaitAndTap` on off-screen Cards items (4 tests)

CollectionView virtualization hides the target from UIAutomator2 tree.

| Test | Location | Helper | Target |
|---|---|---|---|
| PrayerCardTests.Cards_AddPrayerToCard | PrayerCardTests.cs:155 | WaitAndTap | A specific card button by AutomationId |
| PrayerCardTests.Cards_EditButton_NavigatesToEditPage | PrayerCardTests.cs:288 | TapByText | Card title text |
| PrayerCardTests.Cards_EditPrayerFromCard | PrayerCardTests.cs:184 | TapByText | "UI Test Prayer" (proven off-screen in Phase 1 evidence) |
| PrayerCardTests.Cards_ExpandedCard_ShowsActionButtons | PrayerCardTests.cs:236 | TapByText | Similar |

**Fix (pattern established):** for tests that must drive to a specific item in the Cards list, **scroll-to-find** before tap. Either `ScrollDownTo(automationId)` or a new `ScrollToText(text)` helper. This is the right answer regardless of what Inspector says — virtualization is real and unavoidable.

## Cluster IV — Assert.True() Failure on post-action navigation (4 tests)

Generic `Expected: True Actual: False` — test author didn't pass a custom message, so the error is opaque.

| Test | Location | Likely Assert |
|---|---|---|
| BoxTests.Boxes_EditCollection_UpdatesName | BoxTests.cs:176 | Post-edit navigation check |
| PrayerListTests.Prayers_CrossTabFreshness | PrayerListTests.cs:268 | Cross-tab consistency |
| PrayerListTests.Prayers_DeletePrayer | PrayerListTests.cs:250 | `IsDisplayed("List_Filter_Active")` after delete + NavigateToTab |
| PrayerListTests.Prayers_EditPrayer | PrayerListTests.cs:172 | `IsDisplayed("List_Filter_Active")` after edit + NavigateToTab |

The DeletePrayer / EditPrayer pair consistently fails on the SAME assertion — `Assert.True(driver.IsDisplayed("List_Filter_Active", timeoutSeconds: 5))` after `driver.NavigateToTab("Prayers")`. That's a real navigation regression: after editing or deleting a prayer, tapping the Prayers tab in the tab bar does not land on the filter bar state.

**Investigation:** reproduce in Inspector; check if after save/delete the app is stuck on a "View" (read-only detail) page that NavigateToTab can't escape with its current back-button escalation.

**Candidate fix:** either (a) add a `View mode → List` escape path in NavigateToTab, or (b) file as a real product bug if the user would hit the same thing tapping the tab manually after a delete.

## Cluster V — Custom assertion failures (real product bugs) (4 tests)

Clear, semantic failure messages. High confidence these are real product issues, not infra.

| Test | Assertion message | Known? |
|---|---|---|
| AndroidTests.HardwareBack_DirtyDetail_ShowsDiscardDialog | "Hardware back with unsaved changes should show discard dialog or stay on page" | **Yes** — IEditGuard not wired to Android hardware back. In backlog. |
| BoxTests.Cards_ArchivedSection_VisibleAndCollapsed | "Archived section header should be visible on the Cards page" | No — needs investigation |
| PrayerCardTests.Cards_ExpandedCard_ShowsActionButtons (after seed-miss resolved) | "Expanded user card should show Favorite button" | No — needs investigation (but may just be a virtualization victim from Cluster III) |
| TagTests.Tags_CreateTag_AppearsInList | "Should return to tag list after saving new tag" | Noted in prior handoff as save-nav regression |

**Action:** file each as a separate backlog item so they're fixed on the product side, not masked in tests.

## Cluster VI — Miscellaneous element-not-found singletons (4 tests)

| Test | Location | Helper | Likely cluster home |
|---|---|---|---|
| EdgeCaseTests.Cards_EmptySearch_ShowsEmptyState | EdgeCaseTests.cs:28 | EnterText | Unknown — needs inspection |
| EdgeCaseTests.EdgeCase_EmptyCardExpand_ShowsAddPrayer | EdgeCaseTests.cs:120 | TapToolbarItemById | Possibly Cluster II (tab bar hidden on PrayerTime page?) |
| FeatureGapTests.Cards_FavoriteToggle_ChangesState | FeatureGapTests.cs:283 | EnsureUITestCardExists (Cards_List_Cards wait) | Cluster II sibling |
| QuickAddTests.QuickAdd_Cancel_DismissesModal | QuickAddTests.cs:65 | Tap | Unknown — needs inspection |
| SettingsTests.Settings_Backup_ShowsButtons | SettingsTests.cs:68 | WaitAndTap (15s) | Unknown |

## Expected-vs-actual crosscheck

Prior post-C run had **29** unique fails. This run has **31**. The +2 are:
- `PrayerCardTests.Cards_EditButton_NavigatesToEditPage`
- `QuickAddTests.QuickAdd_Cancel_DismissesModal`

Both were "passing" in the prior run purely because the old silent-fallback create was incidentally setting up the state they needed. That's not a real regression — Phase 1 surfaced two additional honest failures. Net: **same 29 problems + 2 honest** rather than **29 + 2 masked**.

## Recommended next-step order

1. **Step 5 Appium Inspector session** — get ground truth on Cluster I + II first (biggest cluster, most impact). Specifically reproduce one `UnsavedChanges_EditTitle_BackShowsDiscardDialog` failure and one `PrayerTime_NavigationButtons_Present` failure with Inspector connected, and capture what page the app is actually on when the helper blows up.
2. **Harden `ResetAppUIState`** — pending Inspector evidence of what state tests are leaking (likely: DetailPage still open, PrayerTime page with hidden tab bar). Add detail-page-escape and PrayerTime-escape paths to the helper, or equivalent escalation inside `NavigateToTab`.
3. **Add `ScrollDownTo` / `ScrollToText` usage in Cluster III tests** — the pattern is clear, no Inspector needed. Can be fixed independently.
4. **File real product bugs** (Cluster V) — four separate backlog items, fixed at the product level, tests stay as canary.
5. **Revisit Cluster IV + VI** — once clusters I–III are resolved, re-run and see what residue remains.

## Prior-art cross-references

Lessons directly relevant to the current clusters — ALL should be consulted before Step 5.

| Lesson | Bears on cluster | Summary |
|---|---|---|
| `maui-toolbaritem-android-rendering.md` | I | Android Shell ToolbarItem rendering table. **Text-only items (no icon) DO expose `@text="..."` in UIAutomator2.** So `TapToolbarItem("Add")` SHOULD work on PrayerListPage (text-only). If it's failing intermittently, iconization is NOT the cause — cascade/state is. |
| `uitest-automation-ids-over-visible-text.md` | I, III | Project convention: "locate chrome by AutomationId, never by visible text." Adding an AutomationId to the Prayers Add toolbar is aligned with this rule regardless of whether text-lookup is currently intermittently broken. |
| `uitest-per-test-ui-state-reset.md` | I, II | Explicit: "What reset does NOT fix: prior test left app on a deep page / modal that ResetAppUIState doesn't escape from." Matches Cluster II exactly. The fix hint is in this lesson's "gotchas" and "what reset does not fix" sections. |
| `maui-shell-goasync-race.md` | IV | Shell GoToAsync racing async save causes intermittent crashes on iOS. On Android, same class of race can cause the page to fail to unmount/re-enter cleanly — a candidate contributor to `Prayers_EditPrayer` / `Prayers_DeletePrayer` post-save assertion failures. |
| `uitest-destructive-tests-use-throwaways.md` | Potentially IV, V | Prior precedent: `BoxTests.Boxes_DeleteCollection_DeleteAllCards` wiped the shared baseline in an earlier run, producing a cascade of unrelated-looking failures. Worth re-verifying that the current throwaway-tier seed still fully insulates the shared baseline. |
| `uitest-dynamic-skip-renders-as-fail.md` | — | Migration status check: the Option B `SkippableFact` work landed. If any residual `throw SkipException.ForSkip(...)` is left in the code, it still manifests as a FAIL. Do a search before Step 5 — a simple source scan confirms zero or flags holdouts. |

### Implications

1. **Cluster I's "add AutomationId to Prayers Add toolbar" move is not speculative** — it's a convention-compliance fix that the project's own lesson prescribes regardless of whether text-lookup works right now. Low risk, high alignment with existing pattern.
2. **Cluster II's root cause is almost certainly documented** — "ResetAppUIState doesn't escape deep pages/modals" is a known limitation, not a new discovery. The fix shape is either an escape-path addition (cheap) or a per-class "NavigateToTabRoot" helper with fallback to app relaunch (heavier).
3. **Cluster IV should be cross-checked against the GoToAsync-race lesson** — even though that lesson is iOS-documented, the race is real on Android too; a defensive `Task.Delay` or `await`-before-nav may help.

## Open questions for Step 5 Inspector session

- On Android Shell with a **text-only** ToolbarItem (`<ToolbarItem Text="Add" />` no AutomationId, no IconImageSource), does UIAutomator2 expose `@text="Add"` on the rendered menu item? If NO → text-lookup is fundamentally broken on MAUI 10 Android, and Cluster I collapses into "add AutomationId to every Shell toolbar item" (a lot of XAML but a clear fix).
- When the Prayers tab is tapped while the app is on a PrayerTime page (tab bar hidden), what does the tab-bar tap actually do? Inspector can show the pre-tap and post-tap tree side by side.
- Is the UIAutomator2 instrumentation crash that hit this morning reproducible? Or was it emulator-local? Important for test reliability claims.

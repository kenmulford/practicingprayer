# iOS UAT: Remaining Failures After Bug Fix Passes

**Last updated:** 2026-03-27
**Latest build tested:** Release config, commit `1b19734` (BUG-1 SIGABRT fix)
**Test result:** 50 passed, 5 failed (55 total)

---

## Test Environment

| Component | Version |
|-----------|---------|
| .NET | 10.0.5 |
| Appium Server | 3.2.2 |
| Appium XCUITest Driver | 10.33.0 |
| Appium.WebDriver (C# client) | 8.1.0 |
| Node.js | 22.22.1 |
| Xcode | 26.4 (Build 17E192) |
| Simulator | iPad (A16), iOS 26.4 |
| xUnit | 2.9.2 |
| Microsoft.NET.Test.Sdk | 17.12.0 |
| Host OS | macOS 26.4 (Darwin 25.4.0), ARM64 |

**Important compatibility notes:**
- Appium.WebDriver 8.1.0 targets Selenium WebDriver 4.x — do not upgrade to 5.x without verifying XCUITest driver compatibility
- XCUITest driver 10.33.0 uses WDA (WebDriverAgent) — `mobile: hideKeyboard` works reliably on iPad but not iPhone (no dismiss button)
- Tests run on iPad specifically because `HideKeyboard` fails on iPhone — see `TestConfig.cs` comment

---

## Run History

| Commit | Result | Notes |
|--------|--------|-------|
| `93c412b` (BUG-1/2/3/4/5) | 47/55 | Bugs 1, 3, 5 fixed. Prayer Time still flaky. |
| `1b19734` (BUG-1 SIGABRT) | **50/55** | All 5 Prayer Time tests now pass. No crashes. No session recoveries. |

---

## Bugs Confirmed Fixed

| Bug | Test | Fix Commit | Notes |
|-----|------|------------|-------|
| Bug #1: SIGABRT crash during tag save | `Tags_CreateTag_AppearsInList` | `1b19734` | Native gesture cleanup. No more crashes across multiple runs. |
| Bug #3: `GoToAsync("..")` unreliable after tag save | Same test | `93c412b` | Passes consistently (9-41s depending on run). |
| Bug #4: Prayer Time action sheet stale element | All 5 PrayerTime tests | `1b19734` | All 5 pass now — `NavigationButtons_Present` (64s), `TagScoped` (14s), `AutoMode` (50s), `SessionStarts` (7s), `FinishButton` (55s). |
| Bug #5: Empty card expand on iPad | `Cards_ExpandCard_ShowsPrayers` | `93c412b` | Passes in 1s. |

---

## Still Failing: 5 Tests

### Failure 1: Unsaved Changes Guard Bypassed on iOS (Bug #2) — APP BUG

**Test:** `UnsavedChanges_EditTitle_BackShowsDiscardDialog`
**Error:** `$XunitDynamicSkip$ iOS Bug #2: Unsaved changes guard bypassed on iOS back navigation — data loss risk`

**What happens:** The test skips on iOS because the bug is confirmed — `GoBack()` on a dirty prayer detail page navigates back without showing the discard confirmation dialog. Changes are silently lost.

**This is an app bug, not a test issue.** On Android, the same flow triggers the dialog.

**Investigate:**
- `Shell.OnNavigating` — does it fire on iOS software back button?
- `BackButtonBehavior.Command` — is it wired up in `PrayerDetailPage.xaml`?
- iOS swipe-back gesture — does it trigger the same navigation path?
- Consider using `Page.OnNavigatingFrom` override instead of Shell-level interception

---

### Failure 2: Tab-Switch Unsaved Changes — Cascade from Failure 1

**Test:** `UnsavedChanges_EditTitle_TabSwitchShowsDiscardDialog`
**Error:** `WebDriverTimeoutException: Timed out after 10 seconds` — can't find "Add" toolbar item on Prayers tab.

**What happens:** Runs after the Bug #2 skip test. The skip at line 31 of `UnsavedChangesTests.cs` throws after `EnterText("Detail_Entry_Title", "Dirty Prayer")` has already been called — leaving the app on the detail page with dirty data. The next test tries `NavigateToNewPrayer()` but the "Add" toolbar button isn't visible because the app is still stuck on/recovering from the detail page.

**Root cause:** This is a cascade from Bug #2's skip, not an independent failure. Fix Bug #2 and this test should pass. Alternatively, the skip in the prior test could be moved before `EnterText` to avoid dirtying the state.

**Stack:** `TapToolbarItem("Add")` at `AppExtensions.cs:388` → `NavigateToNewPrayer` at `AppExtensions.cs:503`

---

### Failure 3: Empty Card Expand — Edge Case Variant

**Test:** `EdgeCase_EmptyCardExpand_ShowsAddPrayer`
**Error:** `Empty card should show '+ Add prayer' button` (assertion failure after 30s)

**What happens:** Creates a brand new empty card, navigates away and back, taps it to expand, and looks for `Cards_Btn_AddPrayer`. The button is not found even after scrolling.

**Key difference from passing test:** `Cards_ExpandCard_ShowsPrayers` (which passes) expands an existing card from the Quick Add system card. This test creates its own card inline. The freshly-created card's expand behavior may differ:
- CollectionView may not have finished updating after card creation
- The new card may be below the fold and the expand tap isn't hitting it
- The "+ Add prayer" button DataTemplate may not render for the brand new empty card until a full page reload

**Likely cause:** App timing/layout issue with freshly-created empty cards on iPad. Could also be a test timing issue — may need more delay after card creation before attempting expand.

---

### Failure 4 & 5: Test Dependency — 'UI Test Prayer' Not Found

**Tests:**
- `Cards_EditPrayerFromCard`
- `Prayers_TapPrayer_ShowsViewMode`

**Error:** `$XunitDynamicSkip$ Precondition: 'UI Test Prayer' not found — depends on earlier QuickAdd test`

**What happens:** These tests expect a prayer named "UI Test Prayer" to exist (created by `QuickAdd_SaveWithTitle_DismissesModal`). All QuickAdd tests pass, but these two tests run at ~2:41 and ~3:47 respectively, while QuickAdd tests run at ~10:15+. **xUnit ran these tests before the QuickAdd tests that create the data they depend on.**

**This is a test design issue.** xUnit doesn't guarantee execution order across test classes within a collection. Options:
1. Make these tests self-contained — create their own prayer as a setup step
2. Use `IClassFixture` with a shared data setup that creates the prayer once
3. Move these tests into the QuickAdd test class so they run after the setup tests

---

## Summary

| # | Test | Category | Root Cause |
|---|------|----------|------------|
| 1 | `UnsavedChanges_EditTitle_BackShowsDiscardDialog` | **App bug** | iOS back nav bypasses unsaved changes guard |
| 2 | `UnsavedChanges_EditTitle_TabSwitchShowsDiscardDialog` | **Cascade** | Dirty state from test 1's skip |
| 3 | `EdgeCase_EmptyCardExpand_ShowsAddPrayer` | **App/timing** | Freshly-created empty card expand on iPad |
| 4 | `Cards_EditPrayerFromCard` | **Test design** | Runs before QuickAdd creates its data |
| 5 | `Prayers_TapPrayer_ShowsViewMode` | **Test design** | Same ordering issue |

**Crash status:** Zero crashes, zero session recoveries across this run. The `1b19734` SIGABRT fix is holding.

**Next priority:**
1. **Bug #2** (unsaved changes guard) — app fix needed, 2 tests blocked
2. **Test dependency** (ordering) — test code fix, 2 tests blocked
3. **Empty card expand** — investigate if app or test timing, 1 test blocked

# iOS UAT: Test Results and Bug Tracking

**Last updated:** 2026-03-27
**Latest build tested:** Release config, commit `8d9bdb6` (BUG-2 swipe-back + iPad PageSheet)
**Test result:** 51 passed, 4 failed (55 total)

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

**Compatibility notes:**
- Appium.WebDriver 8.1.0 targets Selenium WebDriver 4.x — do not upgrade to 5.x without verifying XCUITest driver compatibility
- XCUITest driver 10.33.0 uses WDA — `mobile: hideKeyboard` works reliably on iPad but not iPhone
- Tests run on iPad because `HideKeyboard` fails on iPhone — see `TestConfig.cs`

---

## Run History

| Commit | Result | Notes |
|--------|--------|-------|
| `93c412b` (BUG-1/2/3/4/5) | 47/55 | Bugs 1, 3, 5 fixed. |
| `1b19734` (BUG-1 SIGABRT) | 50/55 | All Prayer Time passes. No crashes. |
| `c8574d7` (EditGuardHelper) | 32/55 | Regression — BackButtonBehavior + GoToAsync corrupts Shell. |
| `767dc6a` (PopAsync fix) | 35/55 | Partial recovery — Reminders fixed, Settings/Tags still cascade. |
| `fab14aa` (Revert EditGuardHelper) | 49/55 | Cascade eliminated. TabSwitch unsaved changes now passes. |
| `8d9bdb6` (BUG-2 swipe-back) | **51/55** | **Current.** Prayer Time intermittent tests now pass. 3 remaining real failures + 1 skip. |

---

## Bugs Confirmed Fixed (Stable Across All Runs)

| Bug | Fix Commit | Status |
|-----|------------|--------|
| Bug #1: SIGABRT crash during tag save | `1b19734` | Stable — no crashes in any run |
| Bug #3: `GoToAsync("..")` after tag save | `93c412b` | Stable |
| Bug #5: Card expand (main case) | `93c412b` | Stable |

---

## Remaining Failures: 4 Tests

### 1. Unsaved Changes Guard on iOS Back Nav — APP BUG (Bug #2)

**Test:** `UnsavedChanges_EditTitle_BackShowsDiscardDialog`
**Error:** `$XunitDynamicSkip$` — test skips on iOS because bug is confirmed
**Duration:** 1ms (immediate skip)

iOS back navigation bypasses the unsaved changes guard. Changes are silently lost. The `EditGuardHelper` approach (BackButtonBehavior) was attempted and reverted because it corrupted the Shell navigation stack. A different approach is needed.

**Note:** `UnsavedChanges_EditTitle_TabSwitchShowsDiscardDialog` now **PASSES** — tab-switching does trigger the guard correctly. Only the back button/gesture is broken.

**Possible approaches (not yet tried):**
- `Shell.Navigating` event with deferral (cancel if dirty)
- `Page.OnNavigatingFrom` override (.NET 10)
- Custom iOS back button in the toolbar (not BackButtonBehavior)

---

### 2. Empty Card Expand Edge Case — 1 test (FIX APPLIED)

**Test:** `EdgeCase_EmptyCardExpand_ShowsAddPrayer` (40s)
**Error:** Assertion — empty card doesn't show "+ Add prayer" after expand

Consistent failure. On iPad, expanding an empty card at the bottom of the list pushes `Cards_Btn_AddPrayer` below the viewport. The `ScrollDownTo` was using full-screen `mobile: swipe` which doesn't reliably scroll the CollectionView.

**Fix:** Changed `ScrollDownTo` to use iOS `mobile: scroll` with `elementId` targeting the `Cards_List_Cards` CollectionView for element-targeted scrolling.

---

### 3. Prayers_TapPrayer_ShowsViewMode — 1 test (FIX APPLIED)

**Test:** `Prayers_TapPrayer_ShowsViewMode` (33s)
**Error:** `WebDriverTimeoutException` at `TapByText` — can't find the prayer to tap

After other tests create data, "UI Test Prayer" gets pushed off-screen. `TapByText` doesn't scroll. `EnsureUITestPrayerExists` uses `IsTextDisplayed` (no scroll) which can miss off-screen items.

**Fix:** Added `ScrollDownToText` call before `TapByText` — scrolls `List_List_Prayers` CollectionView to find the prayer.

---

### 4. PrayerTime_TagScoped_ShowsScopePage — 1 test (FIX APPLIED)

**Test:** `PrayerTime_TagScoped_ShowsScopePage` (12s)
**Error:** `WebDriverTimeoutException` at `TapByText("By Tags")` — race condition

`IsTextDisplayed("By Tags")` found the action sheet element, but by the time `TapByText` re-queried, the element went stale (action sheet animation still settling).

**Fix:** Increased post-tap delay from 500ms to 1000ms for action sheet animation, added 300ms settle delay before tapping, increased timeout to 5s.

---

### Previously Intermittent — Now Stable

**Prayer Time Action Sheet (Bug #4)** — 3 tests that were intermittent in the `fab14aa` run now pass consistently:
- `PrayerTime_NavigationButtons_Present` (1m 4s)
- `PrayerTime_AutoMode_CyclesInterval` (50s)
- `PrayerTime_FinishButton_ExitsPrayerTime` (55s)

---

## Summary

| # | Test | Category | Status |
|---|------|----------|--------|
| 1 | `UnsavedChanges_EditTitle_BackShowsDiscardDialog` | App bug (Bug #2) | Always skips on iOS |
| 2 | `EdgeCase_EmptyCardExpand_ShowsAddPrayer` | Test scrolling bug | **Fix applied** — element-targeted scroll |
| 3 | `Prayers_TapPrayer_ShowsViewMode` | Test scrolling bug | **Fix applied** — scroll to text before tap |
| 4 | `PrayerTime_TagScoped_ShowsScopePage` | Test timing bug | **Fix applied** — longer settle delay |

**No crashes. No session recoveries. No cascade failures.**

**Priority:**
1. Bug #2 (unsaved changes) — needs a non-BackButtonBehavior approach (app bug, not test bug)
2. Verify fixes #2–#4 pass on next iOS test run

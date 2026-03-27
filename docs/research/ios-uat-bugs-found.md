# iOS UAT: Test Results and Bug Tracking

**Last updated:** 2026-03-27
**Latest build tested:** Release config, commit `fab14aa` (Revert EditGuardHelper)
**Test result:** 49 passed, 6 failed (55 total)

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
| `fab14aa` (Revert EditGuardHelper) | **49/55** | **Current.** Cascade eliminated. TabSwitch unsaved changes now passes. |

---

## Bugs Confirmed Fixed (Stable Across All Runs)

| Bug | Fix Commit | Status |
|-----|------------|--------|
| Bug #1: SIGABRT crash during tag save | `1b19734` | Stable — no crashes in any run |
| Bug #3: `GoToAsync("..")` after tag save | `93c412b` | Stable |
| Bug #5: Card expand (main case) | `93c412b` | Stable |

---

## Remaining Failures: 6 Tests

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

### 2. Prayer Time Action Sheet Intermittent (Bug #4) — 3 tests

**Tests:**
- `PrayerTime_NavigationButtons_Present` (54s)
- `PrayerTime_AutoMode_CyclesInterval` (95s)
- `PrayerTime_FinishButton_ExitsPrayerTime` (53s)

**Error:** `$XunitDynamicSkip$ Prayer Time action sheet could not be started`

Intermittent — these passed at `1b19734` but fail here. `TryStartPrayerTime()` taps `Home_Btn_PrayerTime`, waits for the action sheet, tries to tap "All Requests" twice. The action sheet either doesn't appear or elements go stale.

**Note:** `PrayerTime_SessionStarts_ShowsCarousel` and `PrayerTime_TagScoped_ShowsScopePage` PASS in this same run, proving the action sheet can work. The intermittency may be related to test ordering — when these tests run after certain other tests, the Home page state may differ.

---

### 3. Empty Card Expand Edge Case — 1 test

**Test:** `EdgeCase_EmptyCardExpand_ShowsAddPrayer` (32s)
**Error:** Assertion — empty card doesn't show "+ Add prayer" after expand

Consistent failure across all runs. Creates a new empty card, taps to expand, can't find `Cards_Btn_AddPrayer`. The main card expand test (`Cards_ExpandCard_ShowsPrayers`) passes — this is specific to freshly-created empty cards on iPad.

---

### 4. Prayers_TapPrayer_ShowsViewMode — 1 test

**Test:** `Prayers_TapPrayer_ShowsViewMode` (25s)
**Error:** `WebDriverTimeoutException` at `TapByText` — can't find the prayer to tap

This test was rewritten in `c8574d7` to create its own prayer (no longer depends on QuickAdd). The rewrite may have a bug — the prayer is created but then can't be found on the Prayers tab to tap into view mode. Needs investigation: is the prayer being created successfully? Is it visible on the list? Is the text locator matching correctly?

---

## Summary

| # | Test | Category | Consistent? |
|---|------|----------|-------------|
| 1 | `UnsavedChanges_EditTitle_BackShowsDiscardDialog` | App bug (Bug #2) | Yes — always skips on iOS |
| 2 | `PrayerTime_NavigationButtons_Present` | App bug (Bug #4) | Intermittent |
| 3 | `PrayerTime_AutoMode_CyclesInterval` | App bug (Bug #4) | Intermittent |
| 4 | `PrayerTime_FinishButton_ExitsPrayerTime` | App bug (Bug #4) | Intermittent |
| 5 | `EdgeCase_EmptyCardExpand_ShowsAddPrayer` | App/layout bug | Yes — always fails |
| 6 | `Prayers_TapPrayer_ShowsViewMode` | Test code bug? | Yes — always fails |

**No crashes. No session recoveries. No cascade failures.**

**Priority:**
1. Bug #2 (unsaved changes) — needs a non-BackButtonBehavior approach
2. Bug #4 (Prayer Time) — intermittent, may need app-side stabilization of action sheet
3. Empty card expand — investigate iPad layout for freshly-created empty cards
4. `Prayers_TapPrayer_ShowsViewMode` — likely test code issue in the `c8574d7` rewrite

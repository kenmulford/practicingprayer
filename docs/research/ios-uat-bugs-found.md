# iOS UAT: Test Results and Bug Tracking

**Last updated:** 2026-03-27
**Latest build tested:** Release config, commit `c8574d7` (Fix 5 remaining iOS UITest failures)
**Test result:** 32 passed, 23 failed (55 total) — REGRESSION from 50/55

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
| `1b19734` (BUG-1 SIGABRT) | 50/55 | All 5 Prayer Time tests pass. No crashes. Best run. |
| `c8574d7` (Fix remaining 5) | **32/55** | **REGRESSION.** EditGuardHelper causes cascade failure from Reminders onward. |

---

## CRITICAL: Regression Introduced by `c8574d7`

### Root Cause: `EditGuardHelper.AttachEditGuardBackButton()` Breaks Navigation

**File:** `PrayerApp/Helpers/EditGuardHelper.cs` (new file in `c8574d7`)

This helper replaces the native back button behavior on `PrayerDetailPage`, `PrayerCardPage`, and `TagDetailPage` with a custom `Command` that:
1. Checks `IEditGuard.IsDirty`
2. If dirty, calls `guard.CanLeaveAsync()` (shows discard dialog)
3. Navigates via `Shell.Current.GoToAsync("..")`

**The problem:** Even when the page is NOT dirty, the back button now uses `GoToAsync("..")` instead of the native back navigation. `GoToAsync("..")` does not behave identically to native back — it can fail to resolve correctly depending on the Shell navigation stack state.

### Evidence: Cascade Failure Pattern

The failure cascade starts at test #38 (`Reminders_ToggleOn_ShowsPickers`) and every test after it fails:

| Test # | Test | Result | Duration | Notes |
|--------|------|--------|----------|-------|
| 37 | `Reminders_ToggleOff_HidesPickers` | PASS | 27s | Last passing test |
| 38 | `Reminders_ToggleOn_ShowsPickers` | **FAIL** | 13s | Can't find "Add" toolbar — first failure |
| 39 | `Reminders_FrequencyPicker_HasOptions` | **FAIL** | 13s | Same — can't find "Add" toolbar |
| 40-46 | All 7 Settings tests | **FAIL** | ~93s each | Can't navigate to Settings tab |
| 47-51 | All 5 Tag tests | **FAIL** | 5-13s | Can't navigate to Tags tab |
| 52-55 | All 4 UnsavedChanges tests | **FAIL** | 13s | Can't navigate to Prayers tab |

**All failures share the same root:** `NavigateToTab` or `TapToolbarItem` can't find expected elements because the app is stuck in an unrecoverable navigation state after a `GoToAsync("..")` call from the EditGuardHelper fails or leaves a corrupted nav stack.

### What Test #37 Does That Triggers It

`Reminders_ToggleOff_HidesPickers` navigates to a new prayer detail page (which now has EditGuardHelper attached), toggles reminders, then calls `GoBack()`. The `GoBack()` triggers the custom `BackButtonBehavior.Command` → `GoToAsync("..")`. If this doesn't pop back correctly, the navigation stack is corrupted and no subsequent test can find tabs or toolbar items.

### Recommended Fix

The `EditGuardHelper` approach has a fundamental problem: **it replaces native back with `GoToAsync("..")` for ALL back navigations, not just dirty ones.** Options:

1. **Only override when dirty:** Don't set `BackButtonBehavior` at all unless/until `IsDirty` becomes true. Re-attach when dirty, remove when clean.
2. **Use native back for clean pages:** In the Command, if `!guard.IsDirty`, call `Shell.Current.Navigation.PopAsync()` instead of `GoToAsync("..")`.
3. **Use `Shell.Navigating` event instead:** Hook into `Shell.Current.Navigating` on the page and cancel the event when dirty, rather than replacing the back button entirely.

Option 2 is the quickest fix:
```csharp
Command = new Command(async () =>
{
    if (page.BindingContext is IEditGuard guard && guard.IsDirty)
    {
        if (!await guard.CanLeaveAsync())
            return;
    }
    // Use native pop instead of GoToAsync("..") to preserve nav stack
    await Shell.Current.Navigation.PopAsync();
})
```

---

## Pre-Regression Bugs Still Open

These were the 5 failures from the `1b19734` run (50/55) that `c8574d7` attempted to fix:

### Bug #2: Unsaved Changes Guard Bypassed on iOS Back Navigation

**Status:** The `EditGuardHelper` was the attempted fix for this. It needs to be reworked per the options above.

### Empty Card Expand Edge Case

**Test:** `EdgeCase_EmptyCardExpand_ShowsAddPrayer`
**Status:** Still fails (32s). Freshly-created empty card expand on iPad doesn't show the "+ Add prayer" button.

### Test Dependency: 'UI Test Prayer' Not Found

**Tests:** `Cards_EditPrayerFromCard`, `Prayers_TapPrayer_ShowsViewMode`
**Status:** `Cards_EditPrayerFromCard` now PASSES (commit `c8574d7` made it self-contained). `Prayers_TapPrayer_ShowsViewMode` still fails — now with a different error (timeout at `TapByText`, not a precondition skip), likely because the test changes in `c8574d7` have a bug or are affected by the EditGuardHelper regression.

---

## Bugs Confirmed Fixed (Holding Across Runs)

| Bug | Test | Fix Commit |
|-----|------|------------|
| Bug #1: SIGABRT crash during tag save | `Tags_CreateTag_AppearsInList` | `1b19734` |
| Bug #3: `GoToAsync("..")` unreliable after tag save | Same test | `93c412b` |
| Bug #4: Prayer Time action sheet stale element | PrayerTime tests | `1b19734` |
| Bug #5: Empty card expand (main case) | `Cards_ExpandCard_ShowsPrayers` | `93c412b` |

**Note:** Bug #4 (Prayer Time) regressed in this run — `NavigationButtons_Present`, `AutoMode_CyclesInterval`, and `FinishButton_ExitsPrayerTime` failed again. This may be the EditGuardHelper cascade poisoning the app state before these tests run, or it may be intermittent. The previous run (`1b19734`) had all 5 Prayer Time tests passing.

---

## Summary

**The `c8574d7` commit should be reverted or the `EditGuardHelper` reworked.** It regressed from 50/55 to 32/55 by replacing native back navigation with `GoToAsync("..")` on all detail pages. The best baseline is commit `1b19734` at 50/55.

**Priority:**
1. Fix `EditGuardHelper` — use `PopAsync()` for clean pages, only intercept when dirty
2. Retest from the fixed EditGuardHelper
3. Then address the remaining 5 from the `1b19734` baseline

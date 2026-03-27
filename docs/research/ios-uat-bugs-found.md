# iOS UAT: Test Results and Bug Tracking

**Last updated:** 2026-03-27
**Latest build tested:** Release config, commit `767dc6a` (EditGuardHelper PopAsync fix)
**Test result:** 35 passed, 20 failed (55 total) — still regressed from 50/55 baseline

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
| `93c412b` (BUG-1/2/3/4/5) | 47/55 | Bugs 1, 3, 5 fixed. |
| `1b19734` (BUG-1 SIGABRT) | **50/55** | **BEST RUN.** All Prayer Time passes. No crashes. |
| `c8574d7` (Fix remaining 5) | 32/55 | EditGuardHelper regression — cascade from Reminders. |
| `767dc6a` (PopAsync fix) | **35/55** | Reminders recovered (+3). Settings/Tags/UnsavedChanges still cascade. |

---

## CRITICAL: EditGuardHelper Still Breaking Navigation

### What `767dc6a` Fixed

The `PopAsync()` change recovered the **Reminders** tests (all 3 pass now). The Reminders tests open `PrayerDetailPage`, which has `EditGuardHelper` attached. With `PopAsync()` instead of `GoToAsync("..")`, back navigation from the prayer detail page now works correctly for the Reminders flow.

### What's Still Broken: 16 Tests in Cascade

Everything from **Settings** onward fails (16 tests). The cascade starts at `Settings_Backup_ShowsButtons` and continues through all Settings (7), Tags (5), and UnsavedChanges (4) tests.

**Critical finding: The app navigates to the tab but content doesn't render.**

| Test | Error | Duration |
|------|-------|----------|
| `Settings_HubPage_Shows4Rows` | "App Settings row should be visible" | 82s |
| `Settings_Backup_ShowsButtons` | `WaitAndTap("Settings_Row_Backup")` timeout | 92s |
| `Tags_PageLoads_ShowsTagList` | "Tag list should be visible" | 5s |

These tests successfully navigate to the Settings/Tags tab (no `NavigateToTab` error), but the page content (rows, lists) isn't rendering. This suggests the app's UI is in a corrupted state — Shell tabs switch but pages don't load their content.

### What Causes the Corruption

The corruption starts somewhere between `Reminders_FrequencyPicker_HasOptions` (last pass, test #39) and `Settings_Backup_ShowsButtons` (first fail, test #40). The Reminders test ends by calling `driver.GoBack()` from a prayer detail page. Even with `PopAsync()`, the `BackButtonBehavior` + `PopAsync()` combination may be corrupting the Shell's internal navigation state.

### The Real Problem with EditGuardHelper

The `BackButtonBehavior` with a custom `Command` **completely replaces** the native iOS back button behavior. Even for non-dirty pages, the native back gesture/button now goes through a custom code path (`PopAsync()`). This is fundamentally different from the default Shell back behavior:

- **Default Shell back:** Native iOS manages the navigation stack, animations, and page lifecycle
- **Custom BackButtonBehavior:** .NET MAUI intercepts the button, runs C# code, then calls `PopAsync()` programmatically

The `PopAsync()` path may not trigger the same page lifecycle events, Shell state updates, or cleanup that the native back does. Over multiple back navigations (each test navigates forward and back), this drift accumulates until the Shell state is corrupt enough that pages stop loading.

### Recommended Approach

**Don't use `BackButtonBehavior` at all.** Instead:

1. **Use `Shell.Navigating` event** on the page to intercept back navigation:
```csharp
protected override void OnAppearing()
{
    base.OnAppearing();
    Shell.Current.Navigating += OnShellNavigating;
}

protected override void OnDisappearing()
{
    Shell.Current.Navigating -= OnShellNavigating;
    base.OnDisappearing();
}

private async void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
{
    if (BindingContext is IEditGuard guard && guard.IsDirty)
    {
        var deferral = e.GetDeferral();
        if (!await guard.CanLeaveAsync())
            e.Cancel();
        deferral.Complete();
    }
}
```

2. This preserves the **native back button** entirely — no custom Command, no PopAsync, no replacement of iOS navigation behavior.
3. `Shell.Navigating` fires for ALL navigation (back button, tab switch, programmatic), so it also catches the tab-switch discard case.
4. If `Shell.Navigating` doesn't fire on iOS back button (the original Bug #2), then the issue is in MAUI's Shell implementation and needs a different workaround (e.g., `Page.OnNavigatingFrom` in .NET 10).

**Alternative: Remove EditGuardHelper entirely and revert to `1b19734` baseline (50/55).** The unsaved changes guard (Bug #2) is a real issue but the current fix approach is causing more damage than the bug itself.

---

## Other Failures (Not EditGuardHelper Related)

### `EdgeCase_EmptyCardExpand_ShowsAddPrayer` (1 test)
Same as before — freshly-created empty card on iPad doesn't show "+ Add prayer" button after expansion. 31s duration. Unrelated to EditGuardHelper.

### `Prayers_TapPrayer_ShowsViewMode` (1 test)
Timeout at `TapByText` (25s). The test was rewritten in `c8574d7` to be self-contained (creates its own prayer). The new version may have a bug — needs investigation separate from EditGuardHelper.

### `PrayerTime_NavigationButtons_Present` and `PrayerTime_AutoMode_CyclesInterval` (2 tests)
Action sheet intermittency — `TryStartPrayerTime()` can't tap "All Requests". These were passing at `1b19734`. May be re-triggered by EditGuardHelper corrupting state before Prayer Time tests run, or may be genuinely intermittent (Bug #4).

---

## Bugs Confirmed Fixed (Holding Across All Runs)

| Bug | Fix Commit | Status |
|-----|------------|--------|
| Bug #1: SIGABRT crash during tag save | `1b19734` | Stable — no crashes in any run |
| Bug #3: `GoToAsync("..")` after tag save | `93c412b` | Stable |
| Bug #5: Card expand (main case) | `93c412b` | Stable |

---

## Summary

**The EditGuardHelper approach (BackButtonBehavior + custom Command) is fundamentally incompatible with stable iOS Shell navigation.** Both `GoToAsync("..")` and `PopAsync()` cause progressive Shell state corruption over multiple test navigations. The fix needs to use `Shell.Navigating` event interception or be reverted entirely.

**Best baseline: `1b19734` at 50/55.**

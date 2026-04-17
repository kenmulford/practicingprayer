# Phase 2 plan — Shell tab-tap doesn't pop nav stack

Derived from Step 5 (Appium Inspector). Supersedes open questions in `uitest-remediation-2026-04-17-post-phase1.md` for Cluster I and II.

## Finding (live evidence, 2026-04-17)

Ken demonstrated in Appium Inspector: on a MAUI Shell app, **tapping the currently-active tab in the bottom tab bar does NOT pop that tab's navigation stack**. If you're on a PrayerDetailPage pushed from the Prayers tab, tapping "Prayers" at the bottom is a no-op — you stay on the detail page.

This is MAUI Shell's default behavior (not a bug; matches `maui-shell-navigation` skill's documented semantics — absolute routes like `//PrayersPage` reset the stack, but user-level tab-bar taps don't).

### Direct impact on the 31 failures

The test suite's `EnsureOnTab(tab, setup)` calls `NavigateToTab(tab)`, which taps the tab bar. If the previous test left the app on a detail page within that tab, the tap no-ops and the next action (`WaitForElement("List_List_Prayers")`, `TapToolbarItem("Add")`, etc.) times out looking for elements that only exist on the tab's root page.

Clusters explained by this root cause:

| Cluster | Tests | Failure mode |
|---|---|---|
| II | 5 PrayerTimeTests + 1 FeatureGapTests | `WaitForElement("List_List_Prayers", 10)` times out → we're on DetailPage, list element not present |
| I | 9 tests (Unsaved × 4, Reminder × 3, Prayers_AddNewPrayer, Tags_AddTagToPrayer) | `TapToolbarItem("Add", 10)` times out → we're on DetailPage, no "Add" toolbar item |
| IV (part) | PrayerListTests.Prayers_EditPrayer, Prayers_DeletePrayer | `Assert.True(IsDisplayed("List_Filter_Active"))` fails → after save, the test taps Prayers tab expecting list; app stays on DetailPage |

That accounts for **~16 of 31 failures** in one root cause. Large enough that fixing this alone will flip the suite dramatically green.

**Counter-evidence check:** the tests that currently PASS using `NavigateToNewPrayer` / `EnsureOnTab` do so because their predecessor test happened to end on the tab's root page (not a detail page) — so the tap-active-tab no-op is harmless. Confirmed by inspection of test source order.

### Prior-art alignment

- `uitest-per-test-ui-state-reset.md` explicitly lists "prior test left the app on a deep page that ResetAppUIState can't escape" as a known gap.
- `AppExtensions.NavigateToTabRoot` (line ~756) already implements the pattern needed: tap tab, then `GoBack()` repeatedly until the root element appears. Currently called by 5 tests that target the Settings tab (which has deep sub-pages).
- `maui-shell-navigation` skill documents absolute Shell routes (`//PrayersPage`) as the programmatic way to reset and switch tabs.

## Two-pronged remediation

### (A) Test-side — harden `EnsureOnTab` to land on tab root — **DEFERRED**

**Status (2026-04-17):** (B) landed first. Measure the suite after the next run — if the app-side fix alone clears the ~16 cluster I/II failures, (A) may be unnecessary (less infrastructure, fewer moving parts). If residual cascade failures remain, come back to (A) then.


Scope: `PrayerApp.UITests/Helpers/AppExtensions.cs` only.

**Change:** `EnsureOnTab` currently delegates to `NavigateToTab` (which only taps). Change it to delegate to `NavigateToTabRoot` (which taps then pops the stack via `GoBack` until the tab's root element appears). Needs a tab-name → root-element-id lookup, since `NavigateToTabRoot` requires the root element id parameter.

**Shape:**

```csharp
// New: central registry of per-tab root sentinels.
private static readonly Dictionary<string, string> TabRootElementIds = new()
{
    ["Home"]          = "Home_Btn_QuickAdd",
    ["Prayer Cards"]  = "Cards_List_Cards",
    ["Prayers"]       = "List_List_Prayers",
    ["Prayer Time"]   = /* ??? — see open question below */,
    ["Tags"]          = "Tags_List_Tags",
    ["Settings"]      = "Settings_Row_AppSettings",
};

public static void EnsureOnTab(this AppiumDriver driver, string tabTitle, AppiumSetup setup)
{
    setup.EnsureSessionAlive();
    driver.DismissOnboardingIfPresent(setup);

    if (TabRootElementIds.TryGetValue(tabTitle, out var rootId))
        driver.NavigateToTabRoot(tabTitle, rootId, setup);
    else
        driver.NavigateToTab(tabTitle);  // fallback for any tab not yet registered
}
```

The 5 existing `NavigateToTabRoot` callers can stay as-is — they use the Settings tab with a specific sub-root element (AppSettings, Help), which is a DIFFERENT root than the generic Settings root. The new registry is for "tab bare root" only.

**Risk assessment — does this regress passing tests?**
- Currently-passing tests land on the tab's root either (i) directly from prior-test cleanup, or (ii) because the prior test ended on root. Either way, `NavigateToTabRoot` detects root on first iteration (2s timeout check) and returns immediately — **zero added latency**.
- Currently-failing cascade tests: instead of timing out at 10s on `WaitForElement(list)`, they'll pop the stack in <1s per `GoBack()` (up to 5 iterations = 5s worst case), **saving 5s per failing test**.
- Risk of new regression: a test that INTENTIONALLY lands on a sub-page via EnsureOnTab. Searched — no such test exists. All EnsureOnTab callers immediately follow with another action assuming they're on the tab root.

**Verification plan:** run the full suite. Expected:
- PrayerTimeTests (5): all currently fail at WaitForElement in EnsureUITestPrayerExists → should now pass setup (the `TryStartPrayerTime` loop handles the rest).
- UnsavedChangesTests (4), ReminderTests (3), PrayerListTests.Prayers_AddNewPrayer, TagTests.Tags_AddTagToPrayer: currently fail at TapToolbarItem inside NavigateToNewPrayer → should now pass if the preceding EnsureOnTab lands on the list.
- PrayerListTests.Prayers_EditPrayer, Prayers_DeletePrayer: the Assert.True after post-save tab-tap → will remove test-side workaround for the app-side gap; likely PASS once app-side (B) lands, but even without (B), the test can call the same hardened `NavigateToTab` pattern and recover.

**Estimated fail-count drop:** 16 → 0 at best, or 16 → a handful of real residual regressions that were masked.

### (B) App-side — tap-active-tab pops the nav stack (UX fix)

Scope: `PrayerApp/AppShell.xaml.cs`.

**Why it's worth doing independent of tests:** it's a standard mobile-app UX affordance. Instagram, Twitter, most mainstream iOS/Android apps pop the active tab's stack when tapped. Users who don't know about it may tolerate it; users who rely on it will find the app awkward without it.

**Shape (outline — needs validation against MAUI 10 APIs before implementing):**

Hook `Shell.Current.OnNavigating` and detect `ShellItemChanged` where the target ShellContent's route matches the current ShellContent's route. In that case, programmatically `GoToAsync("//<TargetRoute>")` to reset the stack.

```csharp
// AppShell.xaml.cs — sketch, NOT vetted against MAUI 10
protected override async void OnNavigating(ShellNavigatingEventArgs args)
{
    base.OnNavigating(args);

    if (args.Source == ShellNavigationSource.ShellContentChanged &&
        args.Current?.Location == args.Target?.Location)
    {
        // Tab-bar tap on the already-active tab — pop to root
        args.Cancel();
        await GoToAsync($"//{currentTabRoute}");
    }
}
```

**Open question:** whether MAUI 10 fires `ShellContentChanged` at all on a same-tab tap, or whether it no-ops at a lower layer (not reaching OnNavigating). If it no-ops, we'd need a different hook — possibly subscribing directly to TabBar button events. Requires a spike to confirm.

**Risk:** interacts with `IEditGuard`. If the current page is dirty and the user taps the active tab, does the discard dialog fire before the pop? Needs test coverage.

### Recommended order

1. **Implement (A)** first. Smallest scope, fastest verification, unblocks the suite.
2. **Commit (A) as a separate test-infra change.** Re-run the full suite, measure fail drop, update the triage doc.
3. **Spike (B)** — write a ~30-line proof-of-concept in AppShell.xaml.cs, run manually on the emulator, verify the tap-active-tab pops. Don't merge the spike; document behavior.
4. **Implement (B)** properly with IEditGuard interaction, unit/UI test coverage for both "clean stack pops" and "dirty stack shows discard dialog first" paths.
5. **Commit (B)** as a separate product change.

## Decisions Ken should make before I implement

1. **Proceed with (A) now, or wait for more Inspector evidence on Q2/Q3?** (A) unblocks without needing more evidence; Q2/Q3 would confirm tree/locator details that (A) doesn't require. My vote: proceed with (A).
2. **Is the "Prayer Time" tab's root sentinel knowable?** PrayerTimePage has a hidden tab bar, so tapping "Prayer Time" during a test may not be the right entry — tests should go via Home and `Home_Btn_PrayerTime`. For now, **omit Prayer Time from the registry** so `EnsureOnTab("Prayer Time", …)` falls back to the existing `NavigateToTab` path.
3. **Separate PR for (B), or bundle with (A)?** I recommend separate — (A) is test-only, (B) is product UX. Different review lenses.

## Anti-goals (things NOT to do in Phase 2)

- Don't modify `ResetAppUIState` to navigate. Its contract is "state cleanup only, caller navigates" per `uitest-per-test-ui-state-reset.md`. Leave it alone.
- Don't sprinkle `NavigateToTabRoot` calls in individual tests. Fix `EnsureOnTab` once, fan out through the single helper.
- Don't add an `AutomationId` to the Prayers Add toolbar yet (my prior instinct was wrong). If (A) lands and the `TapToolbarItem("Add")` timeouts resolve, we never needed the XAML change. Revisit only if residual failures show text-lookup is still unreliable post-(A).
- Don't touch the two Cluster III `TapByText`-on-off-screen tests in Phase 2. Fix that class in a separate Phase 3 (scroll-to-find or AutomationIds on card cells) — evidence is clear enough to queue, but not this PR.

## Open question logged for Step 5 Inspector session

**Q2 revision:** in light of the Phase 2 finding, Q2 (reproduce the stuck-on-detail scenario) is partially answered. Still valuable to capture a page-source XML dump of "stuck on PrayerDetailPage, Prayers tab tapped in mirror, nothing changes" — becomes permanent evidence in `docs/research/appium-inspector/` for future maintainers.

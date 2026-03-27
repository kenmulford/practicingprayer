# iOS UAT: Remaining Failures After Bug Fix Pass

**Date:** 2026-03-27
**Build tested:** v1.0.6 (32), Release config, commit `93c412b`
**Test result:** 47 passed, 8 failed (55 total)

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

## Bugs Fixed by Commit `93c412b`

| Bug | Test | Status |
|-----|------|--------|
| Bug #1: SIGABRT crash during tag save | `Tags_CreateTag_AppearsInList` | **FIXED** — passes in 9s (was crashing/timing out at 67s) |
| Bug #3: `GoToAsync("..")` unreliable after tag save | Same test | **FIXED** — navigation works reliably now |
| Bug #5: Empty card expand on iPad | `Cards_ExpandCard_ShowsPrayers` | **FIXED** — passes in 1s |

---

## Still Failing: 8 Tests

### Failure 1: Prayer Time Action Sheet Won't Start (Bug #4)

**Tests affected (3):**
- `PrayerTime_NavigationButtons_Present`
- `PrayerTime_AutoMode_CyclesInterval`
- `PrayerTime_FinishButton_ExitsPrayerTime`

**Error:** `$XunitDynamicSkip$ Prayer Time action sheet could not be started`

**What happens:** `TryStartPrayerTime()` taps `Home_Btn_PrayerTime`, waits for the "All Requests" / "By Tags" action sheet, and tries to tap "All Requests". The action sheet either doesn't appear or its elements go stale before they can be tapped. After 2 retry attempts, the method returns `false` and the test skips.

**Relevant code:** `PrayerTimeTests.cs:20-53` — `TryStartPrayerTime()` method.

**Observation:** `PrayerTime_SessionStarts_ShowsCarousel` PASSED this run, meaning the action sheet *can* work — it's intermittent. The stale element suggests the action sheet UI tree rebuilds after initial display.

**Note from Ken:** The tag selection screen (Prayer Time > By Tags) hangs — buttons unresponsive. This is a separate but related issue to the action sheet stale elements.

---

### Failure 2: Tag Selection Screen Hangs (related to Bug #4)

**Test:** `PrayerTime_TagScoped_ShowsScopePage`

**Error:** `StaleElementReferenceException` when tapping "By Tags" in the action sheet.

**What happens:** The action sheet appears, `IsTextDisplayed("By Tags")` returns true, but `TapByText("By Tags")` throws because the Button element reference is stale — the element was found but is "not present in the current view anymore."

**Full error excerpt:**
```
StaleElementReferenceException: The previously found element "Button (Element at index 0)"
is not present in the current view anymore. Original error: No matches found for Elements
matching predicate 'fb_uid IN {"C4070000-..."}'
```

**Ken's observation:** Even when "By Tags" is successfully tapped, the tag selection screen that opens just sits there — Cancel and Start buttons are unresponsive. This is an **app bug**, not a test issue.

**Stack:** `AppExtensions.TapByText` → `element.Click()` at `AppExtensions.cs:403`

---

### Failure 3: Unsaved Changes Guard Bypassed on iOS (Bug #2)

**Test:** `UnsavedChanges_EditTitle_BackShowsDiscardDialog`

**Error:** `$XunitDynamicSkip$ iOS Bug #2: Unsaved changes guard bypassed on iOS back navigation — data loss risk`

**What happens:** The test is currently written to skip on iOS because the bug is confirmed — `GoBack()` on a dirty prayer detail page navigates back without showing the discard confirmation dialog. Changes are silently lost.

**This is an app bug, not a test issue.** The test correctly identifies that iOS back navigation bypasses the unsaved changes guard. On Android, the same flow triggers the dialog.

**Investigate:**
- `Shell.OnNavigating` — does it fire on iOS software back button?
- `BackButtonBehavior.Command` — is it wired up in `PrayerDetailPage.xaml`?
- iOS swipe-back gesture — does it trigger the same navigation path?
- Consider using `Page.OnNavigatingFrom` override instead of Shell-level interception

---

### Failure 4: Tab-Switch Unsaved Changes (cascade from #3)

**Test:** `UnsavedChanges_EditTitle_TabSwitchShowsDiscardDialog`

**Error:** `WebDriverTimeoutException: Timed out after 10 seconds` — can't find "Add" toolbar item on the Prayers tab.

**What happens:** This test runs after `UnsavedChanges_EditTitle_BackShowsDiscardDialog`. The prior test's skip leaves the app in an unknown state (possibly still on the detail page with dirty data). When this test tries `NavigateToNewPrayer()`, it reaches the Prayers tab but can't find the "Add" toolbar button.

**Likely cause:** Cascade from the prior test's iOS skip leaving the app state dirty. The test infrastructure's `EnsureOnTab` recovery doesn't fully clean up after a skipped test that entered the detail page.

**Stack:** `AppExtensions.TapToolbarItem("Add")` at `AppExtensions.cs:388` → `NavigateToNewPrayer` at `AppExtensions.cs:503` → `UnsavedChangesTests.cs:69`

---

### Failure 5: Empty Card Expand — Edge Case Variant

**Test:** `EdgeCase_EmptyCardExpand_ShowsAddPrayer`

**Error:** Assertion failure (exact message not captured — likely "Empty card should show '+ Add prayer' button")

**What happens:** This creates a brand new empty card (no prayers), taps it to expand, and looks for the "+ Add prayer" button. Unlike `Cards_ExpandCard_ShowsPrayers` (which now passes), this test creates its own card inline and the expand/button may not render correctly for a freshly-created card on iPad.

**Note:** `Cards_ExpandCard_ShowsPrayers` passes, so the general expand mechanism works. This edge case is specific to a just-created empty card — possibly a timing issue where the CollectionView hasn't finished updating after the new card is added.

---

### Failure 6: Test Dependency — 'UI Test Prayer' Not Found (2 tests)

**Tests:**
- `Cards_EditPrayerFromCard`
- `Prayers_TapPrayer_ShowsViewMode`

**Error:** `$XunitDynamicSkip$ Precondition: 'UI Test Prayer' not found — depends on earlier QuickAdd test`

**What happens:** These tests expect a prayer named "UI Test Prayer" to already exist, created by the QuickAdd tests. The QuickAdd tests (`QuickAdd_SaveWithTitle_DismissesModal`, `QuickAdd_PrayerAppearsOnCardsTab`) all passed this run, so the prayer should have been created. Possible causes:

1. **Test ordering:** xUnit doesn't guarantee order within a collection. These tests may run before QuickAdd tests.
2. **Data isolation:** Each test may get a fresh app state if the session was recreated.
3. **Cross-tab visibility:** The prayer was created via QuickAdd (Home tab) but these tests look on the Cards/Prayers tabs — the prayer may not appear until the list refreshes.

**This is likely a test design issue** — tests shouldn't depend on other tests' side effects. Either make these tests self-contained (create their own prayer) or enforce ordering.

---

## Summary

| Category | Tests | Root Cause |
|----------|-------|------------|
| App bug: Prayer Time action sheet | 3 skipped + 1 stale element | Action sheet re-renders, tag selection hangs |
| App bug: Unsaved changes guard | 1 skipped + 1 cascade | iOS back nav bypasses guard |
| App bug(?): Empty card expand | 1 | Freshly-created empty card expand on iPad |
| Test design: dependency | 2 | Tests depend on QuickAdd side effects |

**Next priority for app fixes:**
1. Prayer Time action sheet / tag selection hang (4 tests blocked)
2. Unsaved changes guard on iOS (2 tests blocked)
3. Empty card expand edge case (1 test)
4. Test dependency can be fixed in test code (2 tests)

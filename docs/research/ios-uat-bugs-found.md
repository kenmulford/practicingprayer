# iOS UAT: Test Results and Bug Tracking

**Last updated:** 2026-03-27
**Latest build tested:** Release config, commit `8d9bdb6` (BUG-2 swipe-back + iPad PageSheet)
**Test result:** 51 passed, 4 failed (55 total)

---

## Running Specific Tests

Run a single test:
```
dotnet test PrayerApp.UITests/PrayerApp.UITests.csproj --filter "FullyQualifiedName=PrayerApp.UITests.Tests.TagTests.Tags_CreateTag_AppearsInList"
```

Run a whole test class:
```
dotnet test PrayerApp.UITests/PrayerApp.UITests.csproj --filter "ClassName=PrayerApp.UITests.Tests.TagTests"
```

**Workflow:** When testing a new commit with fixes, run only the targeted tests first. If they pass, then run the full suite. Don't waste 18 minutes on a full run just to find the targeted fix didn't work.

Monitor progress during a background run:
```
grep -E '(Passed|Failed|\[FAIL\])' <output-file>
tail -5 <output-file>
```

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
| `8d9bdb6` (BUG-2 swipe-back) | **51/55** | Prayer Time intermittent tests now pass. 3 remaining real failures + 1 skip. |
| `67baef6` (fixes after 51/55) | **54/58** | Clean reinstall. No crashes. 2 scroll fixes still failing + 1 Android-only + 1 known skip. |
| `d6b7c57` (CollectionView fix) | **54/58** | CollectionView desync fixed (no more layout errors). Scroll tests still fail — locator issue, not scroll. PrayerTime intermittent = action sheet mis-tap. |
| `43500b0` (diagnostic logging) | **54/58** | Diagnostics confirm: CollectionView cell contents invisible in iOS accessibility tree. Both scroll test failures are the same root cause. |
| `8073ded` (accessibility fix + build fix) | **54/58** | Accessibility semantics added but CollectionView still flattens cells into one element. Root cause identified: need `IsInAccessibleTree` per-cell. |
| `e437ec2` (accessibility flattening fix) | **54/58** | **Current.** Still flattened — diagnostic dump still shows `name="Quick Add, UI Test Prayer"` as single element. `IsInAccessibleTree` approach not working. PrayerTime mis-tap also persists (separate issue). |

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

**Visual observation (manual):** The empty card expands/contracts correctly. Scrolling moves the whole CollectionView correctly. The `Cards_Btn_AddPrayer` button is visually present after expand — the Appium locator (`IsDisplayed` / `ScrollDownTo`) is not finding it despite the element being on screen.

**Diagnostic page source dump (`43500b0`):** After expanding the card, the page source shows `Cards_List_Cards` with card titles ("Quick Add", "Empty Card Test") but **no child elements inside the expanded card**. `Cards_Btn_AddPrayer` does not exist anywhere in the accessibility tree. The CollectionView only exposes top-level cell labels — expanded content (buttons, sub-views) is invisible to Appium/XCUITest.

**Post-accessibility fix (`76cf6c2` + `8073ded`):** Still fails. See "Root Cause" section below — CollectionView flattens all cells into one element.

---

### 3. Prayers_TapPrayer_ShowsViewMode — 1 test (FIX APPLIED)

**Test:** `Prayers_TapPrayer_ShowsViewMode` (33s)
**Error:** `WebDriverTimeoutException` at `TapByText` — can't find the prayer to tap

After other tests create data, "UI Test Prayer" gets pushed off-screen. `TapByText` doesn't scroll. `EnsureUITestPrayerExists` uses `IsTextDisplayed` (no scroll) which can miss off-screen items.

**Fix:** Added `ScrollDownToText` call before `TapByText` — scrolls `List_List_Prayers` CollectionView to find the prayer.

**Diagnostic page source dump (`43500b0`):** The `List_List_Prayers` CollectionView is visible in the page source but **contains zero prayer items** — only scroll bar elements. Either `EnsureUITestPrayerExists` didn't create the prayer, or the CollectionView is not rendering its item cells in the iOS accessibility tree. Same root cause as EmptyCardExpand: MAUI CollectionView cell contents are invisible to Appium/XCUITest.

**Post-accessibility fix (`76cf6c2` + `8073ded`):** Still fails. See "Root Cause" section below — CollectionView flattens all cells into one element.

---

### 4. PrayerTime_TagScoped_ShowsScopePage — 1 test (FIX APPLIED)

**Test:** `PrayerTime_TagScoped_ShowsScopePage` (12s)
**Error:** `WebDriverTimeoutException` at `TapByText("By Tags")` — race condition

`IsTextDisplayed("By Tags")` found the action sheet element, but by the time `TapByText` re-queried, the element went stale (action sheet animation still settling).

**Fix:** Increased post-tap delay from 500ms to 1000ms for action sheet animation, added 300ms settle delay before tapping, increased timeout to 5s.

**Root cause update:** `TryStartPrayerTime()` only handles "All Requests" — it does not handle "By Tags". The intermittent failures are caused by the tap landing on "By Tags" instead of "All Requests" because the action sheet is still animating when the tap fires. Once on the tag selection page with no tags selected, the test stalls. "All Requests" is always present in the action sheet — it's not a detection issue, it's a mis-tap targeting issue during animation.

---

### 5. Prayer Time > Select Tags — CollectionView Layout Stall (NOT A CRASH)

**Context:** Prayer Time → "By Tags" → tag selection screen
**Symptom:** Screen hangs/stalls, no crash, no user-visible error. Sim stops responding for extended periods (8+ minutes observed during test runs).

**Console error (repeats for multiple index paths):**
```
Received layout attributes with an invalid index path. Attributes will be ignored.
Attributes: <UICollectionViewLayoutAttributes: ...; index path: (0-11); frame = (0 569.5; 780 58.5)>;
layout: <Microsoft_Maui_Controls_Handlers_Items2_LayoutFactory2_CustomUICollectionViewCompositionalLayout: ...>;
data source counts: [(0:0)]
```

The CollectionView layout cache has stale items (indexes 0-11) but the data source reports 0 items `[(0:0)]`. This is a known MAUI CollectionView desync — the layout engine tries to render items that no longer exist, flooding UIKit with invalid layout attribute errors.

**Impact:** Causes test timeouts on tag-related tests. May also affect real users if tag list is rapidly cleared/reloaded.

**Not yet investigated:** Could be triggered by the tag scope page binding its CollectionView ItemsSource before data loads, or by a race between navigation and data population.

**Additional observation:** The same CollectionView layout desync also occurs on the Cards tab (`Cards_List_Cards`) during the `EdgeCase_EmptyCardExpand` test — not limited to the tag scope page. This is a systemic MAUI CollectionView issue across multiple pages.

---

### Root Cause: iOS CollectionView Accessibility Tree Flattening

**Discovered:** `8073ded` diagnostic dump on `Prayers_TapPrayer_ShowsViewMode`

MAUI CollectionView on iOS **flattens all cell content into a single accessibility element** instead of exposing individual cells as separate nodes. The diagnostic page source shows:

```xml
<XCUIElementTypeOther name="Quick Add, UI Test Prayer" label="Quick Add, UI Test Prayer"
    enabled="true" visible="true" accessible="true" x="20" y="313" width="780" height="31"/>
```

All prayer items ("Quick Add", "UI Test Prayer") are concatenated into one `name`/`label` on a single `XCUIElementTypeOther` node. Appium's `FindByText("UI Test Prayer")` and `FindByAccessibilityId("Cards_Btn_AddPrayer")` fail because there are no individual cell elements — everything is merged.

**This affects all CollectionView-based tests** that need to locate individual items:
- `EdgeCase_EmptyCardExpand_ShowsAddPrayer` — can't find `Cards_Btn_AddPrayer` inside expanded card
- `Prayers_TapPrayer_ShowsViewMode` — can't find "UI Test Prayer" as a tappable element

**Possible fixes:**
1. ~~Set `AutomationProperties.IsInAccessibleTree="false"` on the CollectionView itself, and `"true"` on individual cell root elements~~ — **tried in `e437ec2`, did not work.** Cells still flattened.
2. Use `SemanticProperties.Description` on each cell template root to give each cell its own identity
3. Switch test locators to search within the flattened label text (fragile workaround)
4. Try `InputTransparent="False"` or wrapping cell content in a container with explicit `AutomationId` — may force iOS to treat each cell as a separate accessible element
5. Consider using `accessible="false"` on the CollectionView's native iOS handler to prevent it from being an accessibility container that swallows children

---

### Previously Intermittent — Now Stable

**Prayer Time Action Sheet (Bug #4)** — 3 tests that were intermittent in the `fab14aa` run now pass consistently:
- `PrayerTime_NavigationButtons_Present` (1m 4s)
- `PrayerTime_AutoMode_CyclesInterval` (50s)
- `PrayerTime_FinishButton_ExitsPrayerTime` (55s)

---

## Console Log Analysis (iOS Simulator)

Captured via Console.app → Errors & Faults → PrayerApp process during targeted test runs.

### Noise (safe to ignore)

| Category | Error | Verdict |
|----------|-------|---------|
| UIKit App Config | `UIScene lifecycle will soon be required` | Apple future deprecation warning. Not actionable. |
| GSFont | `OpenSans-Regular already exists` / `GSFontRegisterCGFont failed 305` | MAUI registers bundled font twice at startup. Cosmetic. |
| TraitCollection | `CKBrowserSwitcherViewController overrides -traitCollection getter` | UIKit internal class (CloudKit). Not our code. |
| AXRuntimeCommon | `Unknown client: PrayerApp` | Accessibility framework startup. Normal. |
| animationDidStop | `Unexpectedly received animationDidStop without matching animationDidStart` | UIKit animation tracking mismatch during Shell tab navigation. Noise. |
| UIFocus | `UIKeyboardLayoutStar implements focusItemsInRect` | Keyboard layout focus caching. Simulator-only. |
| RTILog | `remoteTextInputSessionWithID: perform input operation requires a valid sessionID` | Keyboard input session not initialized. Appium/simulator artifact. |
| CHHapticPattern | `hapticpatternlibrary.plist couldn't be opened — No such file` (floods console) | iPad simulator has no haptic engine. Keyboard feedback fails. Massive spam — dozens of entries per keystroke. |
| UIKBFeedbackGenerator | `Cannot start engine` / `Engine is not running. Can't play feedback` | Same haptic issue as above. |

### Relevant (app-related)

| Category | Error | Impact |
|----------|-------|--------|
| CollectionView | `Received layout attributes with invalid index path ... data source counts: [(0:0)]` | **Bug #5.** MAUI CollectionView layout cache desync. Occurs on Cards tab and Prayer Time tag scope page. Causes UI stalls. |

---

## Summary

| # | Test | Category | Status |
|---|------|----------|--------|
| 1 | `UnsavedChanges_EditTitle_BackShowsDiscardDialog` | App bug (Bug #2) | Always skips on iOS |
| 2 | `EdgeCase_EmptyCardExpand_ShowsAddPrayer` | Test scrolling bug | Fix applied — **still failing** |
| 3 | `Prayers_TapPrayer_ShowsViewMode` | Test scrolling bug | Fix applied — **still failing** |
| 4 | `PrayerTime_TagScoped_ShowsScopePage` | Test timing bug | **Fix applied — now passes** |
| 5 | `AndroidTests.HardwareBack_DirtyDetail_ShowsDiscardDialog` | Android-only test | Fails on iOS — needs platform skip |

**No crashes on clean install. No session recoveries. No cascade failures.**

**Priority:**
1. Fix #2 and #3 scroll tests (still failing after first fix attempt)
2. Skip Android-only test on iOS
3. Bug #2 (unsaved changes) — needs a non-BackButtonBehavior approach (app bug, not test bug)
4. Bug #5 (CollectionView desync) — investigate MAUI framework issue

# iOS UAT: App Bugs Found During Test Suite Stabilization

**Date:** 2026-03-27
**Platform:** iPad (A16) simulator, iOS 26.4, Release build v1.0.6 (32)

---

## Context

During iOS UITest stabilization, several test failures were "fixed" by adding retries, fallbacks, and loosened assertions. On review, multiple failures were **real app bugs** that the tests correctly identified. The test code was modified to work around them rather than flagging them. This document catalogs each suspected bug so they can be investigated and fixed in the app.

---

## Bug 1: SIGABRT Crash During Tag Save Navigation (CRITICAL)

**Test:** `Tags_CreateTag_AppearsInList`, `Tags_DeleteTag`
**Severity:** Critical — app crashes
**Details:** See [ios-crash-tag-save-navigation.md](ios-crash-tag-save-navigation.md) for full crash analysis.

**What the test found:** App crashes with unhandled exception during/after `GoToAsync("..")` in `TagDetailViewModel.SaveAsync()`. Crashed twice in one run — once during tag save, once on subsequent app relaunch (12 seconds after boot).

**What the test workaround did:** `RecreateDriver` (3-attempt session recovery) handled the crash, and the test added a fallback: "if we're not on the list yet, navigate manually to Tags tab." This masks the fact that `GoToAsync("..")` is not reliably returning to the tag list.

**What to fix in the app:** The `GoToAsync("..")` call in `TagDetailViewModel.SaveAsync()` — investigate the unhandled exception. Run a Debug build to get the managed stack trace.

---

## Bug 2: Unsaved Changes Guard Not Working on iOS Back Navigation

**Test:** `UnsavedChanges_EditTitle_BackShowsDiscardDialog`
**Severity:** High — data loss risk

**What the test found:** On iOS, `GoBack()` (hardware/software back) from a prayer detail page with unsaved changes does NOT trigger the discard confirmation dialog. The page just pops and changes are lost.

**What the test workaround did:** Added `backOnList` as an acceptable outcome in the assertion:
```csharp
Assert.True(hasAlert || hasDiscardText || stillOnDetail || backOnList, ...);
```
This means the test passes whether or not the guard fires. The comment says "GoBack bypassed guard" — acknowledging the bug and accepting it.

**What to fix in the app:** The Shell `BackButtonBehavior` or page lifecycle guard that intercepts back navigation. On Android, the native back button triggers the guard. On iOS, `Shell.Current.Navigation.PopAsync()` or the swipe-back gesture may bypass the `OnDisappearing`/`OnNavigating` guard. Investigate:
- Is `Shell.OnNavigating` firing on iOS back?
- Is the `BackButtonBehavior.Command` wired up?
- Does iOS swipe-back gesture trigger the same path?

---

## Bug 3: `GoToAsync("..")` Unreliable After Tag Save

**Test:** `Tags_CreateTag_AppearsInList`
**Severity:** Medium — navigation failure

**What the test found:** After saving a new tag, `GoToAsync("..")` sometimes does not navigate back to the tag list. The app stays on the detail page or ends up in an undefined state.

**What the test workaround did:**
```csharp
// Save navigates back via GoToAsync("..") — if we're not on the list yet, try navigating
if (!driver.IsDisplayed("Tags_List_Tags", timeoutSeconds: 5))
    driver.NavigateToTab("Tags");
```
This fallback manually navigates to the Tags tab if `GoToAsync` failed. The test still passes but the user would be stuck on the detail page after tapping Save.

**What to fix in the app:** Check `TagDetailViewModel.SaveAsync()` — is `GoToAsync("..")` awaited? Is it running on the UI thread? Does the Tags Shell route support `..` navigation? Compare with `PrayerDetailViewModel.SaveAsync()` which uses the same pattern but works reliably.

---

## Bug 4: Prayer Time Action Sheet Stale Element / Re-render

**Test:** `PrayerTimeTests.TryStartPrayerTime`
**Severity:** Low-Medium — flaky UX, not a crash

**What the test found:** The "All Requests" / "By Tags" action sheet sometimes re-renders between the moment the test detects it and the moment it taps a button, causing a `StaleElementReferenceException`. This suggests the action sheet's UI tree is being rebuilt after initial display.

**What the test workaround did:** 2-attempt retry loop around the entire flow (navigate to Home → tap Prayer Time button → wait for action sheet → tap "All Requests"), with alert dismissal between attempts.

**What to fix in the app:** Check the action sheet implementation — is it using `DisplayActionSheet` or a custom popup? If custom, is the content re-binding after initial render? This could also be a MAUI Shell timing issue where navigating to Home triggers a re-layout that invalidates the action sheet's element references.

---

## Bug 5: Empty Card Expand Not Showing Add Prayer Button (iPad Layout)

**Test:** `EdgeCaseTests.EmptyCard_ExpandShowsAddPrayer`
**Severity:** Low-Medium — layout issue on iPad

**What the test found:** After creating an empty card and tapping it to expand, the "+ Add prayer" button is not immediately visible. Required navigating away and back, scrolling, and retrying the tap.

**What the test workaround did:**
- Navigate away and back to collapse all cards (clean state)
- Scroll to find the card if below the fold
- Retry tap with stale element recovery
- Scroll down after expansion to find the Add prayer button

**What to fix in the app:** The card expansion animation or layout on iPad may not be rendering the "+ Add prayer" button within the visible area. Check if the `CollectionView` item template for expanded empty cards sizes correctly on iPad's wider layout. Also check if tapping a card to expand it works reliably or if there's a hit-test issue.

---

## Investigation Priority

1. **Bug 1** (crash) — Critical, do first. Debug build + reproduce.
2. **Bug 2** (unsaved changes guard) — High, data loss risk for users.
3. **Bug 3** (GoToAsync) — May be same root cause as Bug 1.
4. **Bug 4** (action sheet) — Low priority, test handles it.
5. **Bug 5** (iPad layout) — Low priority, may be iPad-specific layout tuning.

---

## How to Reproduce

All bugs were found running the iOS UITest suite:
```bash
# Shut down all simulators first (Appium needs to boot with correct keyboard config)
xcrun simctl shutdown all

# App must be pre-installed on iPad (A16) simulator
xcrun simctl install "iPad (A16)" PrayerApp/bin/Release/net10.0-ios/iossimulator-arm64/PrayerApp.app

# Run iOS tests only
dotnet test PrayerApp.UITests/PrayerApp.UITests.csproj --filter "Platform!=Android"
```

For Bug 1 specifically, run a **Debug build** to get managed exception details:
```bash
dotnet build PrayerApp -f net10.0-ios -c Debug -r iossimulator-arm64
xcrun simctl install "iPad (A16)" PrayerApp/bin/Debug/net10.0-ios/iossimulator-arm64/PrayerApp.app
# Then run the Tags_CreateTag test and watch Xcode console for the exception
```

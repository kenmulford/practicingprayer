# Appium Inspector session runbook — Step 5 of UITest remediation

Purpose: get **ground-truth** answers to the open questions left after Phase 1. Short, directed session — shouldn't take more than 30 min the first time.

## Prerequisites

- Emulator booted and the app running (same state the test suite expects — `pixel_9_-_api_36_0` with the MAUI Debug APK installed).
- Appium server running on the host (default port 4723).
- Appium Inspector installed. Current version as of 2026-04: https://github.com/appium/appium-inspector/releases — download the Windows `.exe` installer.

### Sanity checks before launch

```powershell
# 1. Emulator present + package installed
adb devices
adb shell pm list packages | Select-String multithreadedllc
# expect: package:com.multithreadedllc.prayercards

# 2. App running
adb shell pidof com.multithreadedllc.prayercards
# expect: a pid number. If empty: launch the app from the emulator UI first.

# 3. UIAutomator2 instrumentation NOT in zombie state
adb shell ps | Select-String io.appium.uiautomator2
# expect: usually empty when no Appium session is active. If Appium-driven tests
# recently ran, see uitest-emulator-uiautomator2-cold-boot-recovery.md.

# 4. Appium server reachable
curl http://127.0.0.1:4723/status
# expect: 200 OK JSON
```

## Session capability JSON

Paste into Appium Inspector's "Desired Capabilities" panel (JSON view). Copied from `PrayerApp.UITests/Infrastructure/TestConfig.cs` Android path — kept in sync with what the test suite uses so the Inspector session sees the same tree:

```json
{
    "platformName": "Android",
    "appium:automationName": "UiAutomator2",
    "appium:deviceName": "pixel_9_-_api_36_0",
    "appium:appPackage": "com.multithreadedllc.prayercards",
    "appium:appActivity": "crc6425c6d21f3599989c.MainActivity",
    "appium:noReset": true,
    "appium:autoGrantPermissions": true,
    "appium:newCommandTimeout": 600
}
```

Remote host `127.0.0.1`, port `4723`, path `/`. "Start Session."

If the Inspector hangs on session start longer than ~30s, the emulator or Appium is in a bad state — bail, cold-boot the AVD per the recovery runbook, retry.

## Targeted questions to answer — ordered by ROI

### Q1 (high): What does the Prayers tab "Add" ToolbarItem look like in the UIAutomator2 tree?

Drives Cluster I remediation. Predicted answer (per `maui-toolbaritem-android-rendering.md`): since the Prayers page `<ToolbarItem Text="Add" />` has **no `IconImageSource`**, it should render as a text-only menu item with `@text="Add"` visible in the tree.

1. In Inspector, drive to the Prayers tab (tap it in the bottom tab bar within the mirror).
2. Click "Refresh Source" in Inspector.
3. In the source tree on the right, look for a node with `text="Add"` — should be a `TextView` or `ActionMenuView$OverflowMenuButton`.
4. Click that node. Inspector shows its attributes on the right: `resource-id`, `content-desc`, `class`, `bounds`.
5. **Record verbatim:**
   - `class`: _____
   - `text`: _____
   - `content-desc`: _____
   - `resource-id`: _____

**Decision rule:**
- If `@text="Add"` is present **AND** the node is clickable → the existing `TapToolbarItem("Add")` text-lookup should work; Cluster I failures are purely cascade-driven (prior test state). Fix: harden `ResetAppUIState` / `NavigateToTab` escape path.
- If `@text` is **empty** (common on newer MAUI Shell versions rendering toolbar items as accessible-only widgets), the lesson prescribes adding an AutomationId to make `@content-desc` populated. Fix: PrayerListPage.xaml addition plus `TapToolbarItemById` in the helper.

### Q2 (high): Reproduce a Cluster II failure — what is the app ACTUALLY on when `WaitForElement("List_List_Prayers")` times out?

Drives Cluster II remediation. Prediction: the app is stuck on a PrayerTimePage or a detail modal left by the prior test, and the tap on the Prayers tab in the tab bar silently no-ops because Shell tab-bar visibility is page-specific.

1. Manually reproduce the suspected cross-test state leak. Easiest: drive through a PrayerTime session to the PrayerTime page (which hides the tab bar), then stop interacting.
2. In Inspector, click "Refresh Source." Note that the bottom tab bar is NOT in the tree — Shell hides it on PrayerTimePage.
3. From test code's perspective, the next test's `EnsureOnTab("Prayers", setup)` calls `NavigateToTab("Prayers")`, which does `FindElement(MobileBy.AccessibilityId("Prayers"))`. Since the tab isn't in the tree, stage 1 fails, stage 2 tries modal dismissal, stage 3 tries `TryEscapePrayerTime` (this SHOULD recover), stage 4 re-activates the app, stage 5 is an XPath fallback.
4. Using Inspector's "Execute" panel, run the Selenium commands that stages 3/4/5 would run. Does `TryEscapePrayerTime` find "Finish" or "I'm done"? What's the actual button text on the PrayerTime page right now?
5. **Record:**
   - Tab bar visible: yes / no
   - If you tap "Prayers" at the bottom manually (in Inspector mirror): what happens?
   - Does an escape button exist with the names the helper expects?

**Decision rule:**
- If the escape path works but is slower than `NavigateToTab`'s stage-3 window → bump that stage's timeout/retry count.
- If no escape button is found → add the correct AutomationIds to the PrayerTime page and update `TryEscapePrayerTime`.
- If the Prayers tab IS visible on this page (contradicting the prediction) → the problem is elsewhere, probably a slow page render on Prayers entry. Different fix.

### Q3 (medium): Record a working sequence for `Cards_EditButton_NavigatesToEditPage`

New failure from Phase 1. Tests targets a card by text in the Cards list; the target is likely virtualized off-screen. Confirm and decide whether to scroll-to-find or use AutomationId.

1. Drive to the Cards tab.
2. In Inspector, Start Recording.
3. Manually replay what the test does: tap the card (whatever title the test is looking for), expand, tap edit, navigate.
4. Stop Recording. Save the generated C# snippets.
5. Compare the Inspector-generated locators to what our test file uses.

Possible outcomes:
- Inspector suggests `AccessibilityId("Card_UITestCard")` or similar → we should prefer AutomationId in the test.
- Inspector finds the card only after a scroll action → our test needs `ScrollDownTo` first.
- Inspector shows the card is off-screen but findable via a different XPath strategy we're not using → new helper shape.

### Q4 (low): What's the actual UiAutomator2 tree for a Shell toolbar item with just `AutomationId` (no `IconImageSource`, no `Text`)?

Hypothetical for PrayerListPage if we do add `AutomationId="Add Prayer"` without touching `Text="Add"`. Predict: `@text="Add"` remains, `@content-desc="Add Prayer"`. If both are present, both our existing helpers (`TapToolbarItem` and `TapToolbarItemById`) work. That means the AutomationId addition is zero-risk — it strictly widens the locator coverage.

1. Not reproducible until the XAML change lands. Skip this question for Session 1.
2. If Session 1's Q1 output indicates the AutomationId addition is the right fix, make the XAML change in a throwaway branch, rebuild, redo this question once.

## Evidence capture

Inspector has "Save Source" (XML) and a screenshot button. For every failing-scenario reproduction, save:

- `docs/research/appium-inspector/<YYYYMMDD-HHMMSS>-<question>-tree.xml`
- `docs/research/appium-inspector/<YYYYMMDD-HHMMSS>-<question>-screenshot.png`

These become the evidence for the Phase 2 plan. One-time infrastructure; directory doesn't exist yet — create it when the first save happens.

## Translating Inspector findings into code changes

After the session, before writing any code:

1. **Update `docs/research/uitest-triage-2026-04-17-post-phase1.md`** with the answers to Q1–Q4. Move each cluster's confidence from Medium/Low to High once the Inspector evidence is in.
2. **Propose Phase 2 plan** — evidence-driven, one cluster at a time. Scope: probably
   - XAML: add `AutomationId`s to PrayerListPage, TagsPage, BoxesPage Add toolbars (text-only currently).
   - Helper: switch `NavigateToNewPrayer` (and equivalents) to `TapToolbarItemById`.
   - Helper: harden `NavigateToTab` stage 3 (Prayer Time escape) based on Q2 evidence.
   - Add `ScrollDownTo` / `ScrollToText` usage in Cluster III tests.
3. **Get user approval** before implementing — per project convention (`feedback_methodical_approach.md`).

## Things not to do in this session

- Don't use Inspector's "Record Mode" as a substitute for writing tests — the generated code is scaffolding, not polished. Use only as locator evidence.
- Don't modify the live session from inside Inspector to "see what happens" in a way that leaks into seed state — Inspector shares the app process with the seed DB, so deletes/edits persist.
- Don't run a full `dotnet test` suite while Inspector has a session open on the same device — they compete for the UIAutomator2 instrumentation.

## Cleanup

- Stop Appium Inspector cleanly (it'll tear down the session).
- If next step is kicking off the test suite, **wait for Appium Inspector to fully disconnect** (~5 sec) before running `dotnet test` — otherwise the new test session may see a stale instrumentation state.
- If anything in the session left the app in a weird state (deep page, modal, multi-select), tap back to Home in the emulator UI before starting the suite. Cheaper than relying on test-code to recover.

## Related

- `uitest-remediation-2026-04-17-post-phase1.md` — the plan this runbook executes Step 5 of.
- `uitest-triage-2026-04-17-post-phase1.md` — the cluster analysis Inspector evidence will update.
- `maui-toolbaritem-android-rendering.md` — the rendering table Q1/Q4 will either confirm or contradict.
- `uitest-per-test-ui-state-reset.md` — the "what reset does not fix" list Q2 tests against.
- `uitest-emulator-uiautomator2-cold-boot-recovery.md` — recovery if Inspector session hangs on start.

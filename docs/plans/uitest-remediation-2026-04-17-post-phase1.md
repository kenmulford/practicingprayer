# UITest remediation — post-Phase-1 re-plan (2026-04-17)

Supersedes `uitest-remediation-2026-04-17-post-C.md`. That plan's hypotheses were based on stack-trace pattern-matching without evidence. Phase 1's diagnostic capture produced the evidence; this plan is built on it.

## What Phase 1 actually proved

Phase 1 added `CaptureDiagnostics` + loud `XunitException` on missing seed fixture + page-ready `WaitForElement`. The first failing test captured:

- **Screenshot:** `prayerapp-uitest-diag/20260417-073218-772-EnsureUITestPrayerExists.png`
- **Page source:** same basename, `.xml`

The screenshot shows the Prayers tab **rendering correctly**, status filter on "Active", list populated with (in order) "Delete Me Card / Throwaway prayer", "Delete Me Card A/B / Throwaway prayer A/B", "General / Sample Prayer Entry 1–4". **"UITest Card / UI Test Prayer" is not in the visible tree.** The page source XML confirms it — the RecyclerView for `List_List_Prayers` exposes only on-screen items (CollectionView virtualization).

**Root cause identified:**

> `IsTextDisplayed("UI Test Prayer", N)` fails **not** because the seed didn't apply, but because CollectionView virtualizes off-screen rows and UIAutomator2 doesn't expose virtualized items. The seeded "UI Test Prayer" exists in the DB and in the bound collection — it's just below the fold ("U" sorts after "S", and the Sample Prayer Entries 1–4 fill the visible area).

**What the old `EnsureUITestPrayerExists` was actually doing:** fast-path visibility check almost always returned false → silent fallback to Home→QuickAdd created a *new* "UI Test Prayer" each test run → tests proceeded on whichever prayer happened to be findable. The helper's name lied: it was a "create" helper, not an "ensure" helper.

**What my Phase 1 loud-throw did wrong:** equated "not visible" with "missing." When the assumption broke, every test using `EnsureUITestPrayerExists` threw a false-positive XunitException, cascading into the huge fail count Ken saw. And when the app crashed mid-run, every subsequent test hit the same false-positive because a crash-recovered Home doesn't have the CollectionView.

## Additional evidence

- Seed pipeline works. `TestDataSeed.cs:118–121` creates `"UITest Card"` at `boxId=0` with one prayer `"UI Test Prayer"`. That's in the DB on every run.
- App stability is flaky. Ken reported the app *crashed* during the phase-1 run; when that happens, every remaining test fails with false seed-missing messages. Root cause of the crash is unknown — not addressed by this plan.
- "Sample Prayer Entry 1–4" in the screenshot are **app first-run onboarding demo data**, not from `TestDataSeed`. They're always there.

## Revised hypotheses (with confidence)

| # | Hypothesis | Confidence | Evidence |
|---|------------|------------|----------|
| H1 | "UI Test Prayer" is virtualized off-screen on the Prayers tab | **High** | Screenshot + page source XML |
| H2 | Some of the original 29 fails were false cascades from exactly this confusion | **Medium** | Multiple Ensure*-dependent tests fail together, old silent-fallback code never verified post-create visibility |
| H3 | The remaining genuine failures (`Prayers_DeletePrayer/EditPrayer` Assert.True False, `Tags_CreateTag` save-nav, `HardwareBack_DirtyDetail`, etc.) are real product-level issues, not infra | **Medium** | Their failure lines are Asserts on post-action state, not setup helpers |
| H4 | App crashes during the run are a separate problem — probably test sequence or emulator-specific, not product | **Low** | No evidence either way yet |

## Plan

### Step 1 — Fix the existence check (drop the visibility equivocation)

`EnsureUITestPrayerExists` and `EnsureUITestCardExists` currently throw on `IsTextDisplayed` returning false. Replace with a real existence proof. Two viable options:

**Option A — Trust the seed.** Remove the check entirely. Just `return` after `EnsureOnTab + WaitForElement("List_List_Prayers")`. The seed DB is the contract; if it's broken, test assertions will fail downstream with their own clear messages (and can call `CaptureDiagnostics` on the way).

**Option B — Use the search bar to force visibility.** Tap `List_Search_Prayers`, type "UI Test Prayer", re-check visibility (now filtered list is short), clear search, return. Verifies existence without throwing on virtualization. Adds ~2s per call.

**Recommendation: Option A.** Simplest, no added test time, and the defensive check was never actually defensive — it was papering over virtualization. If the seed breaks, `TestDataSeed` tests can catch that, not every UI test individually.

Apply same to `EnsureUITestCardExists` — card *is* visible (top-level "Loose Cards", no virtualization) per the existing code's own comment at `TestDataSeed.cs:113–117`, but for consistency drop the visibility-as-existence pattern there too.

### Step 2 — Keep what worked

- `CaptureDiagnostics` — unambiguously proved its worth in Phase 1. Keep as-is. Opt-in everywhere else.
- `WaitForElement("List_List_Prayers")` before toolbar tap in `NavigateToNewPrayer` — defensive page-ready wait, independent of the virtualization issue. Keep.
- `WaitForElement("Cards_List_Cards")` in `EnsureUITestCardExists` — same. Keep.

### Step 3 — Do NOT touch yet

- `PrayerListPage.xaml` — no evidence `TapToolbarItem("Add")` text-lookup is broken. My original "add AutomationId" speculation remains unvalidated. Defer.
- App crash root cause — separate investigation. Plan doesn't touch it.
- Individual assertion failures — wait for a clean run first to see which survive Step 1.

### Step 4 — Verification

After Step 1 lands, re-run the suite. Expected outcome:

| Category | Before Phase 1 | Phase 1 (broken) | After Step 1 fix |
|----------|----------------|------------------|------------------|
| Tests that called `EnsureUITestPrayerExists` and previously passed via silent-create | Pass | **Fail (XunitException false-positive)** | **Pass** (seed already has the prayer) |
| `PrayerTimeTests` (5) | Fail at `Home_Btn_QuickAdd` (old fallback broke on crash) | Fail (XunitException) | Likely pass if app wasn't crashed; fail at real PrayerTime issue if any remains |
| Real assertion regressions (DeletePrayer, EditPrayer, CreateTag, HardwareBack, etc.) | Fail | Fail | Fail — diagnose individually with `CaptureDiagnostics` |
| Cascade false positives from my Phase 1 loud throw | N/A | ~15+ false fails | **Gone** |

**Success criterion:** fail count drops back at or below the pre-Phase-1 baseline of 29. Any test that fails should fail at its *real* assertion, not at an `EnsureUITest*Exists` call.

### Step 5 — Appium Inspector session (if Step 4 still leaves unexplained helper failures)

Stop inferring UIAutomator2 behavior from stack traces. Fire up **Appium Inspector** against a live emulator session:

1. Appium server already running from normal test runs.
2. Launch Appium Inspector, connect with the same capability set as `AppiumSetup`.
3. Drive to the specific surface that's failing (Prayers tab "Add" toolbar, Cards tab search, whatever).
4. Tap the element in Inspector's mirror — it reveals full attribute set and recommends a locator strategy. That's ground truth.
5. Compare to what our helper does. Fix the helper or the XAML based on Inspector's output, not speculation.
6. Use **record mode** to capture a working sequence; adapt generated code to our helper conventions.

This also gives us a **manual reproducer** for the app crash seen during Phase 1's run: step through the same sequence in Inspector and see whether the crash is test-induced or product.

### Step 6 — If Step 5 reveals a locator mismatch on a specific ToolbarItem

If Inspector shows `TapToolbarItem("Add")` can't find the element because UIAutomator2 doesn't expose `@text` for Shell Android toolbar items (for example), add the Prayers-toolbar `AutomationId` in XAML and switch the helper to `TapToolbarItemById`. Only with evidence from Step 5 — no more speculation.

## Commit strategy

Rollback + revised Phase 1 in a single commit:
- `test(ui): drop visibility-as-existence check in Ensure* helpers + keep diagnostic capture`

Message body calls out:
1. Why the original helper design was wrong (virtualization confusion).
2. Why we're deleting the silent-create fallback (it was duplicating seeded data every run).
3. What `CaptureDiagnostics` is for (still on by default for any future Ensure* failure).

## Lessons to capture

- **`IsTextDisplayed` is not an existence check.** On CollectionView/RecyclerView with virtualization, off-screen items don't exist in the page tree. Add to `Lessons/uitest-virtualization-vs-existence.md`.
- **"Ensure X exists" helpers that silently create on miss are lying.** Either trust the fixture or fail loudly — but the check must be a real existence proof, not a proxy.
- **Untested hypotheses dressed as fixes compound the problem.** Phase 1's original loud-throw made the run worse than the baseline. Systematic-debugging's "no fix without root cause" rule is not decorative.

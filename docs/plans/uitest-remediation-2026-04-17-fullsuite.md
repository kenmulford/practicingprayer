# UITest Full-Suite Remediation Plan — 2026-04-17

> **For Claude executing this plan:** Investigation-first. No code changes until Ken approves the approach for each bucket. Stop at every checkpoint (⛔) and present findings before proceeding. This plan is gated evidence→options→fix, not sequential implementation.

**Goal:** Resolve the 9 failures from the 2026-04-17 full-suite Android UITest run (`uitest-phase1.log`), categorized into four priority buckets, with each fix gated on evidence and Ken approval.

**Context:**
- Suite: **96 total** → **80 passed / 9 failed / 7 skipped**, duration 43m51s
- Baseline before this run: cherry-picked subset was at 2 fails (BUG-65, BUG-66 canaries)
- Full-suite fails are **not regressions from the fix work** — they're volatile-fixture contamination + the same nav-stack family (BUG-1/3/64/69) surfacing through new tests
- Log: `C:\repos\PrayerApp\uitest-phase1.log` (UTF-16; read via `iconv -f UTF-16LE -t UTF-8`)
- Fix standard: `/simplify` before commit; test-run commands always include `| Tee-Object -FilePath <name>.log`; no framework syntax without grepping codebase for precedent

**Architecture of this plan:**
- **Stage 0** — evidence prerequisites (one prep task that any of the fixes may need)
- **Priority 1** — nav-return family (3 fails, likely one-fix-clears-three)
- **Priority 2** — BUG-65 canary status check (investigation only)
- **Priority 3** — fixture-contamination cluster (4 fails, per-test triage)
- **Priority 4** — BoxTests collection-picker fail (source read + categorize)

Each priority has three phases: **A. Investigate** → **B. Present options & wait for approval (⛔)** → **C. Implement & verify**.

---

## Failure Inventory (reference)

| # | Test | Error shape | Bucket |
|---|---|---|---|
| 1 | `BoxTests.Cards_CreateCard_WithCollectionPicker` | `Assert.True()` on `IsDisplayed("Card_Picker_Box")` | **P4** |
| 2 | `EdgeCaseTests.EdgeCase_EmptyCardExpand_ShowsAddPrayer` | Timeout 10s | **Canary — BUG-66, skip** |
| 3 | `FeatureGapTests.Cards_FavoriteToggle_ChangesState` | Timeout 5s | **P3** |
| 4 | `FeatureGapTests.Settings_OverdueThreshold_VisibleWithDefault` | Timeout 15s | **P3** |
| 5 | `FeatureGapTests.PrayerTime_ByCollection_ShowsScopePage` | Timeout 10s | **P3** |
| 6 | `FeatureGapTests.Settings_Help_FaqAccordionExpandCollapse` | "Should return to Settings hub after leaving Help page" | **P1** |
| 7 | `PrayerCardTests.Cards_CreateCard_AppearsInList` | "Should return to card list after saving new card" | **P1** |
| 8 | `PrayerCardTests.Cards_SearchFilter_FiltersCards` | `NoSuchElementException` in `EnterText(FindByAutomationId(...))` | **P3** |
| 9 | `TagTests.Tags_CreateTag_AppearsInList` | "Should return to tag list after saving new tag" | **P1** |

BUG-65 `PrayerListTests.Prayers_DeletePrayer` **does not appear in the log** — its status needs confirming (P2).

---

## Stage 0: Evidence Prerequisite

**Fixes for P1/P3/P4 may require diagnostic XML captures** that don't currently exist. Today's 9 fails have **no captured page-source dumps** — `DumpPageSource` is called per-test (e.g., `BoxTests.cs:537`), not automatically on every assertion failure. Before blindly prescribing fixes, we decide per-bucket whether to:

1. **(a) Re-run the failing test with Appium Inspector attached** — Ken drives, we read the tree, no code change needed
2. **(b) Add `DumpPageSource(...)` to the failing assertion in each test before re-running** — requires editing test code first
3. **(c) Read source for the likely-faulty screen flow and reason from code** — fast but weaker evidence (risks hypothesizing; see `feedback_vet_syntax_against_codebase_before_using.md`)

**Decision rule:** default to (a) for the nav-return family (cheapest, highest signal) and (c) for everything else, unless option (a) comes back empty.

No task here yet — this is guidance for the investigation steps below.

---

## Priority 1: Nav-return family (3 fails, highest leverage)

**Hypothesis (to be verified, not assumed):** Three tests across three tabs fail with near-identical "should return to X hub" assertions. This matches the family resolved by BUG-69 (`f6323ac`) — a second page instance on the Shell stack causing `GoToAsync("..")` to behave unexpectedly. One fix likely clears all three, but each page's nav flow is different and needs to be read individually.

### P1-A: Investigate

- [ ] **P1-A.1 — Read each failing test's assertion**
  - `PrayerApp.UITests/Tests/FeatureGapTests.cs` → `Settings_Help_FaqAccordionExpandCollapse` (~line 117)
  - `PrayerApp.UITests/Tests/PrayerCardTests.cs` → `Cards_CreateCard_AppearsInList` (~line 125)
  - `PrayerApp.UITests/Tests/TagTests.cs` → `Tags_CreateTag_AppearsInList`
  - Capture: what's the nav pattern? `GoToAsync("..")`? Toolbar tap? `NavigateToTab`? What AutomationId does the assertion look for?

- [ ] **P1-A.2 — Read each corresponding page's save/close flow**
  - Card form: `PrayerApp/Views/PrayerCardPage.xaml.cs` + its ViewModel Save command
  - Tag form: `PrayerApp/Views/TagDetailPage.xaml.cs` + ViewModel
  - Help page: `PrayerApp/Views/HelpPage.xaml.cs` (if exists) + how it's pushed from Settings
  - For each: does the page push a second instance (like the BUG-69 pattern), or is it a single-push Shell nav?

- [ ] **P1-A.3 — Grep for nav precedent**
  - `grep -rn "GoToAsync" PrayerApp/` — list every use
  - Compare what Cards/Tags/Help do on save vs. what Prayer detail does post-BUG-69 fix
  - Identify which pages use `IsReadOnly` toggle vs. direct-push patterns

- [ ] **P1-A.4 — (Optional) Appium Inspector re-run**
  - If code read is ambiguous after A.1–A.3, ask Ken to re-run one of the three tests with Inspector attached so we can observe the stuck page
  - Default: skip this step unless needed

### ⛔ P1-B: Checkpoint — present findings to Ken

Report per test: what page the test got stuck on, what the nav pattern is, and whether the root cause is (i) BUG-69-family two-instance stack, (ii) a different GoToAsync race, or (iii) something else. Present **2–3 fix options with trade-offs**. Wait for Ken's approval on approach before touching code.

### P1-C: Implement approved fix

Tasks intentionally not pre-written — the specific edits depend on which option Ken chooses. Placeholder structure:

- [ ] Implement the approved fix
- [ ] Run affected tests: `dotnet test PrayerApp.UITests/ --filter "FullyQualifiedName~Settings_Help_FaqAccordion|FullyQualifiedName~Cards_CreateCard_AppearsInList|FullyQualifiedName~Tags_CreateTag_AppearsInList" | Tee-Object -FilePath uitest-p1-fix.log`
- [ ] Confirm all three pass
- [ ] Run `/simplify` on the changed files
- [ ] Commit with message referencing the bug family

---

## Priority 2: BUG-65 canary status (investigation only)

**Question:** `PrayerListTests.Prayers_DeletePrayer` (the BUG-65 canary) doesn't appear in today's log. Is it passing, skipped, renamed, or filtered out?

- [ ] **P2.1 — Grep the log directly for `DeletePrayer` (case-insensitive)**
  ```bash
  iconv -f UTF-16LE -t UTF-8 C:/repos/PrayerApp/uitest-phase1.log 2>/dev/null | grep -i "deleteprayer"
  ```
  Interpret:
  - If it appears as `[SKIP]` → confirm why (conditional skip condition)
  - If it appears as `[PASS]` → **important signal**: something changed — either the IEditGuard dialog no longer fires, or test ordering masked it. Worth a manual repro.
  - If absent entirely → check whether the test still exists in `PrayerListTests.cs`

- [ ] **P2.2 — Verify the test still exists in source**
  ```bash
  grep -rn "Prayers_DeletePrayer" C:/repos/PrayerApp/PrayerApp.UITests/
  ```

- [ ] **P2.3 — Check the run filter**
  - Read the command invocation from Ken (or the test project's default filter in `.runsettings` if present)
  - Confirm whether `PrayerListTests` was even included in this run

### ⛔ P2 Checkpoint — report findings

Three outcomes:
1. **Passed in full suite** — BUG-65 may be order-dependent; recommend a focused repro run
2. **Skipped** — note the skip reason; no action
3. **Filtered out** — re-run with the test explicitly included

Ken decides whether to act on the outcome. **No code changes in P2.**

---

## Priority 3: Fixture-contamination cluster (4 fails, per-test triage)

Each of these four tests needs individual triage — they're grouped by symptom (timeout/not-found) but likely have different root causes.

### P3-A: Investigate each test

- [ ] **P3-A.1 — `FeatureGapTests.Cards_FavoriteToggle_ChangesState`**
  - Read test source: what card does it toggle? Shared `UITest Card` or disposable?
  - If shared: likely contaminated by a prior test (e.g., a favorite-state leftover from an earlier run or another test that toggled the same card and didn't un-toggle)
  - Check for disposable-fixture pattern per `Lessons/uitest-destructive-tests-use-throwaways.md`

- [ ] **P3-A.2 — `FeatureGapTests.Settings_OverdueThreshold_VisibleWithDefault`**
  - Read test source: what AutomationId does the 15s timeout wait for?
  - Check `SettingsPage.xaml` to confirm the element is present by default
  - Possible causes: Settings page changed structure, or a prior test left app on a non-Settings page and `NavigateToTab` didn't recover

- [ ] **P3-A.3 — `FeatureGapTests.PrayerTime_ByCollection_ShowsScopePage`**
  - Read test source: what flow does it exercise? Does it require specific seed data?
  - Check if seed DB has the collections/cards this path needs
  - If test depends on `UITest Collection` being non-empty: could be contaminated by BoxTests delete tests

- [ ] **P3-A.4 — `PrayerCardTests.Cards_SearchFilter_FiltersCards`**
  - Read test source: stack shows `EnterText("???", text)` failed at `PrayerCardTests.cs:70`
  - Identify the AutomationId it's trying to enter text into — is the search bar off-screen? Not yet rendered? Stolen by another element with the same ID?
  - Check for recent renames of `Cards_Search` AutomationId

### ⛔ P3-B: Checkpoint — bucket each test

Per test, classify as:
1. **Contamination** → convert to disposable fixture (Cluster III pattern) or add state reset at test start
2. **Timing** → add wait/retry/scroll-to-visibility guard
3. **Real product bug** → file new BUG row, leave test as canary
4. **Test-code bug** → edit test only

Present all 4 classifications + recommended fix per test. Wait for Ken's approval on which to tackle first.

### P3-C: Implement approved fixes

- [ ] Implement each approved fix one test at a time
- [ ] After each fix, run the single test in isolation + as part of its class: `dotnet test PrayerApp.UITests/ --filter "FullyQualifiedName~<TestName>" | Tee-Object -FilePath uitest-p3-<testname>.log`
- [ ] `/simplify` between tests if >20 lines changed
- [ ] Commit per logical grouping (not all 4 at once unless they share a fix)

---

## Priority 4: BoxTests.Cards_CreateCard_WithCollectionPicker

**What the test asserts:** After tapping toolbar "Add Card" and waiting for `Card_Entry_Title`, the `Card_Picker_Box` element should be displayed within 3 seconds (`BoxTests.cs:259`).

**Why it could fail:**
- (a) `Card_Picker_Box` was renamed or removed from `PrayerCardPage.xaml`
- (b) Picker is conditionally hidden (e.g., based on collection count or user setting) — a state that may differ between isolated and full-suite runs
- (c) Timing — picker takes >3s to render on first open

### P4-A: Investigate

- [ ] **P4-A.1 — Grep for `Card_Picker_Box` in main project**
  ```bash
  grep -rn "Card_Picker_Box" C:/repos/PrayerApp/PrayerApp/
  ```
  Confirm it exists in `PrayerCardPage.xaml` with the expected AutomationId

- [ ] **P4-A.2 — Check conditional visibility**
  - If found, read the XAML binding/trigger around it — is it hidden when Collections collection is empty?
  - Read `PrayerCardViewModel` (or similar) for the property the picker's visibility binds to

- [ ] **P4-A.3 — Check test ordering**
  - `Cards_CreateCard_WithCollectionPicker` is test 8.9 in `BoxTests.cs`
  - Tests 8.8 and 8.11 **delete collections** (`Boxes_DeleteCollection_UnassignCards`, `Boxes_DeleteCollection_DeleteAllCards`) — if these run first and leave zero user collections, 8.9's picker may legitimately be hidden
  - Check seed DB after delete tests: does "UITest Collection" still exist? `Delete Me Collection A/B`?

### ⛔ P4-B: Checkpoint — present cause + option

Likely one of:
1. **Renamed AutomationId** → update test to match
2. **Fixture deletion bleed** → add `EnsureUITestCollectionExists` at start of test OR guard picker visibility in the view
3. **Timing** → bump timeout from 3s

Present finding + recommendation, wait for approval.

### P4-C: Implement approved fix

- [ ] Implement the approved fix
- [ ] Run in isolation: `dotnet test PrayerApp.UITests/ --filter "FullyQualifiedName~Cards_CreateCard_WithCollectionPicker" | Tee-Object -FilePath uitest-p4-fix.log`
- [ ] Run the full `BoxTests` class to catch ordering issues: `dotnet test PrayerApp.UITests/ --filter "FullyQualifiedName~BoxTests" | Tee-Object -FilePath uitest-p4-boxtests.log`
- [ ] `/simplify` + commit

---

## Exit criteria

- **Hard required**: BUG-66 canary is the only remaining fail in a full-suite re-run (or, if Ken accepts some P3 tests stay as-canaries, those are explicitly logged in `BACKLOG.md`)
- **Soft required**: P2 outcome documented, even if no code change
- Every commit preceded by `/simplify`
- `docs/CHANGELOG.md` updated for any user-facing improvements (none expected for pure test/nav fixes, but verify)
- New lessons filed for any non-obvious root cause (per `Lessons/` convention)

## Running the verification full-suite

After all buckets complete, re-run the full suite:

```powershell
dotnet test PrayerApp.UITests/ --framework net10.0 --logger "console;verbosity=detailed" | Tee-Object -FilePath uitest-phase1-post-remediation.log
```

Compare against `uitest-phase1.log` — expect **0–1 fails (canary BUG-66 only)**.

---

## Open questions to raise with Ken before starting

1. **P2**: should we investigate `Prayers_DeletePrayer`'s absence first, since that may change the canary count (and the exit criteria) before we touch other work?
2. **P1 vs P3 ordering**: P1 has the highest leverage (1 fix → 3 tests) but P3-A4 (`Cards_SearchFilter`) may be a test-code rot from a recent rename — quick to confirm and low-risk. Run P3-A4's investigation in parallel with P1-A?
3. **Worktree**: this plan spans multiple test files and touches both test code and possibly app code (P1). Isolate in a worktree or work on `dev` directly?

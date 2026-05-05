# BUG-80 — Prayer-card realize-storm class (multiple CRUD paths jetsam at scale)

**Status:** RESOLVED — broad scope. Fix landed on branch `fix/bug-80-realize-storm` (BUG-79 cleanup at `0239485`, BUG-80 broad fix as the next commit). Pending end-to-end UAT on iPhone 17 / iOS 26.4 before merge to `dev`. See "Resolution" section at the bottom. The audit body below is preserved as the design record.

---

## Summary

`PrayerCardsPage` jetsams (memory-pressure kill, fatal type 309) on **any post-CRUD return-to-list flow** when the affected card has a large prayer-request list (~50+) and is expanded. Mark-Answered was BUG-79; the partial fix landed on `dev` and works for Mark-Answered specifically. **Delete on the same large card was confirmed to crash identically after the BUG-79 fix landed.** Code-path audit (in this doc) shows multiple additional vectors still live.

This is a **realize-storm class bug**, not a single-mutation bug. Treat the consolidated fix as the canonical solution.

---

## Symptom

iPhone 17 / iOS 26.4. Reliable repro on both:

**Mark-Answered (was BUG-79):** open a card with 50+ prayer requests → tap a request → "Mark Answered" → list pops back → spinner ~3s → app crashes.

**Delete (now BUG-80):** open the same large card → tap a request → Delete → list pops back → app crashes the same way.

Does **not** repro on regular-sized cards. Both crashes show the same syslog signature: `HangTracer 3.26s` → `Received memory warning` → `kernel: Corpse allowed N of 5` → `ReportCrash: Formulating fatal 309 report`. No managed exception, no NSException.

---

## Root cause

### The engine

`PrayerApp/Views/PrayerCard/PrayerCardsPage.xaml`:
- **Per-card prayer rows are rendered with `BindableLayout` (zero virtualization)** — lines 168-222. Every row's full visual tree (`Grid` + `TapGestureRecognizer` + 2 `Label`s with converters + `Image` w/ `IconTintColorBehavior` + `AppThemeBinding` `BoxView`) is materialized for every prayer in the card, regardless of viewport.
- **Outer grouped `CollectionView` is `ItemSizingStrategy="MeasureAllItems"`** — line 347. Every group/cell measured, defeats virtualization for sizing.

Any operation that does `Prayers.Clear() + N×Add()` against a live `BindableLayout` materializes N row visual trees synchronously. At ~57 rows × multiple visual elements × multiple bindings each, two sequential passes blow the memory budget and iOS jetsams the process.

### Verification path

1. Initial syslog evidence (BUG-79 first repro, Build 92): `HangTracer` + memory warning + corpse markers, no managed exception. Held privately in Ken's vault, not in this repo.
2. PerfLog instrumentation (Build 93/94) routed via the existing `IDiagnosticLog` to `diagnostics.log` in the host's `appDataContainer`. Pulled with `xcrun devicectl device copy from --domain-type appDataContainer --domain-identifier com.multithreadedllc.prayercards`. Trace held privately in Ken's vault. Showed:
   - Two full `SyncCoreAsync` invocations per single Mark-Answered, both calling `vm.ReloadPrayers()` on the affected card.
   - Passes are **serialized via `_syncGate`**, not concurrent (`inflight` peaked at 1). Re-entrancy is not the bug — duplicate triggers are.
   - Pass 2 hung ~1s longer than pass 1 between `ReloadPrayers` and the next instrumented checkpoint. Cumulative memory pressure from pass 1 tipped pass 2 into jetsam.

### What's blocking the file-pull mechanism

`idevicesyslog` over network pairing **does not reliably forward host-process Debug.WriteLine output** for this app. PerfLog routing through `IDiagnosticLog` (file write) was the workaround. **Do not propose USB tethering** — see memory.

---

## State of the working tree (uncommitted on `dev`)

The BUG-79 partial fix is in the working tree. Verified end-to-end on iPhone 17 for Mark-Answered. Files:

- `PrayerApp/ViewModels/PrayerCardsViewModel.cs`
  - `SyncAsync(ChangeKind?)` overload added; existing `SyncAsync()` forwards as `null`.
  - Messenger handler for `PrayerChangedMessage` now passes `msg.Kind` to `SyncAsync`. Other entity-change handlers still call parameterless.
  - `SyncCoreAsync` parameter `ChangeKind? changeKind`. Guard at the per-card foreach: `if (vm.IsExpanded && !skipPerCardReload)` where `skipPerCardReload = changeKind == ChangeKind.Updated`. Comment block above the guard documents the BUG-79 reasoning.
  - `public int? SuppressNextOnAppearingSyncForCardId { get; set; }` field added; set in `ApplyQueryAttributes` PrayerSaved branch (line ~360) after `matched.AddOrUpdatePrayerAsync(prayerId).SafeFireAndForget()`.
- `PrayerApp/Views/PrayerCard/PrayerCardsPage.xaml.cs`
  - `OnAppearing` reads `vm.SuppressNextOnAppearingSyncForCardId`, clears it, and skips `await PageSync.OnAppearingAsync(vm)` when set.
- `PrayerApp/ViewModels/PrayerCardViewModel.cs`
  - New `public void ResortPrayers()` — in-place `ObservableCollection.Move`-based reorder mirroring `LoadPrayersAsync`'s sort criteria.
  - `AddOrUpdatePrayerAsync` calls `ResortPrayers()` after `existing.Reload()` so the answered/edited row re-positions without Clear+Add.
- `PrayerApp/Helpers/PerfLog.cs`
  - `Diagnostics.ResolveLog()?.Log("PERF", line)` added next to existing `Debug.WriteLine` so PerfLog entries reliably land in `diagnostics.log`. **Diagnostic-only — every call site in the codebase is currently commented out.**
- `PrayerApp/Services/IDiagnosticLog.cs` + `DiagnosticLog.cs`
  - New `void Log(string category, string message)` overload (the existing API only logged `Exception`). Used by PerfLog.
- `PrayerApp/PrayerApp.csproj` and `PrayerApp.ActionExtension/PrayerApp.ActionExtension.csproj`
  - `<ApplicationVersion>` bumped to 95.

**Net effect of working-tree state:** Mark-Answered no longer crashes; Delete still crashes; sibling broadcasts (tag rename, card rename, card box change) still storm at scale.

---

## Storm-vector matrix (Staff IC audit)

The current B+B4 guard skips per-card reload only when `changeKind == ChangeKind.Updated`. Every other path falls through to the storm.

### Messenger broadcasts → `SyncCoreAsync` per-card-reload risk

Path: `PrayerApp/ViewModels/PrayerCardsViewModel.cs` `SyncCoreAsync` foreach. Guard: `skipPerCardReload = changeKind == ChangeKind.Updated`.

| Source | Message + Kind | Sender | Skip flag set? | Storm? | Notes |
|---|---|---|---|---|---|
| `PrayerService.SavePrayerAsync` (new) | `PrayerChangedMessage(.., Created)` | `PrayerService.cs` ~line 76 | No | **Yes** | List shape changes, but `AddOrUpdatePrayerAsync` (already wired into `ApplyQueryAttributes`) handles the in-place insert |
| `PrayerService.SavePrayerAsync` (edit) | `PrayerChangedMessage(.., Updated)` | `PrayerService.cs` ~line 76 | Yes (B) | No | BUG-79 fix protects this path |
| **`PrayerService.DeletePrayerAsync`** | **`PrayerChangedMessage(.., Deleted)`** | **`PrayerService.cs` ~line 89** | **No** | **Yes** | **Confirmed crash 2026-05-04.** In-place `RemovePrayer` already runs from `ApplyQueryAttributes`; the messenger-driven full reload is redundant |
| `CardService` save / assign-box / delete | `PrayerCardChangedMessage(*)` | `CardService.cs` ~lines 71, 80, 89 | No | **Yes** | Card-level mutation but `SyncCoreAsync` still iterates `vm.ReloadPrayers()` for every expanded card. Card rename / favorite-toggle on a list with one expanded 55-row card → storm |
| `TagService.SaveTagAsync` / `DeleteTagAsync` | `TagChangedMessage(*)` | `TagService.cs` ~lines 97, 110 | No | **Yes** | Tag mutation triggers full re-realize of every expanded card's prayer list. Only `BuildCardTagLookupAsync` truly needs to re-run |
| `TagService.ReassignColorAsync` | `BulkChangedMessage` | `TagService.cs` ~line 135 | No | **Yes** | Same |
| `BoxService.SaveBoxAsync` | `CardBoxChangedMessage(*)` | `BoxService.cs` ~line 56 | No | **Yes** | Section header rename / box-create — no prayer data changed; per-card reload is pure waste |
| `BoxService.DeleteBoxAsync` | `BulkChangedMessage` | `BoxService.cs` ~line 124 | No | Yes (legitimate) | If `deleteCards=true`, prayers under those cards really are gone |
| `BackupService` restore | `BulkChangedMessage` | `BackupService.cs` ~line 145 | No | Yes (legitimate) | Full data replacement; long load is acceptable post-restore |
| `DeepLinkService` import | `BulkChangedMessage` ×2 | `DeepLinkService.cs` ~lines 292, 339 | No | Yes (legitimate) | Same |
| `ConfirmImportViewModel` save | `BulkChangedMessage` | `ConfirmImportViewModel.cs` ~line 147 | No | Yes (legitimate) | Same |

### `ApplyQueryAttributes` return-from-edit branches → `OnAppearing`'s pass-2 storm

`PrayerApp/ViewModels/PrayerCardsViewModel.cs` `ApplyQueryAttributes` (lines ~316-377). The branch's `OnAppearing` follow-up is the second storm pass.

| Branch | Lines | In-place handler? | Sets `SuppressNextOnAppearingSyncForCardId`? | Pass-2 storm? |
|---|---|---|---|---|
| `Deleted` (card) | ~318-329 | `Remove` from `AllPrayerCards` + `RebuildSections` | No | **Yes** — OnAppearing runs full `SyncAsync()` |
| `Saved` (card new/edit) | ~330-343 | None (relies on `ConsumePendingSavedAsync`) | No | Yes (intentional — diff loop adds new card) |
| `PrayerSaved` (PrayerSaved + ParentCardId) | ~344-367 | `AddOrUpdatePrayerAsync` + `Move`-based resort | **Yes** (B4) | No (B4 protects this path) |
| **`PrayerDeleted` (PrayerDeleted + ParentCardId)** | **~368-376** | `matched?.RemovePrayer(prayerId)` (in-place, safe) | **No** | **Yes** — OnAppearing runs full `SyncAsync()` against the still-expanded large card |

### Per-card method storm-vector classification

`PrayerApp/ViewModels/PrayerCardViewModel.cs`:

| Method | Mutation shape | Storm? |
|---|---|---|
| `LoadPrayersAsync` | `Clear()` + N×`Add()` | **Yes — the engine** |
| `ReloadPrayers` | Calls `LoadPrayersAsync` | **Yes** |
| `AddOrUpdatePrayerAsync` | `existing.Reload()` + `Move` (existing) **or** single `Insert` (new) | No |
| `RemovePrayer` | Single `Remove` | No |
| `ResortPrayers` (new in working tree) | `Move`-only | No |

---

## Recommended consolidated fix (Staff IC)

**Architectural principle:** `SyncCoreAsync` does **card-list reconciliation only**. Per-card row reconciliation lives in `ApplyQueryAttributes` (return-from-edit) and `ToggleExpandedAsync` (first-expand) — never in the messenger-driven sync.

Concrete changes on top of the working-tree B+B4:

1. **Widen the BUG-79 guard.** `PrayerCardsViewModel.cs` `SyncCoreAsync` — change `skipPerCardReload = changeKind == ChangeKind.Updated` to `skipPerCardReload = changeKind != null`. All `PrayerChangedMessage` kinds (Created/Updated/Deleted) skip per-card reload. Created and Deleted already have in-place handlers in `ApplyQueryAttributes` (`AddOrUpdatePrayerAsync` for new, `RemovePrayer` for delete). `RefreshActivePrayerCount` keeps the badge correct without realizing rows.
2. **Mirror B4 for Delete.** `ApplyQueryAttributes` PrayerDeleted branch — add `SuppressNextOnAppearingSyncForCardId = parentCardId` after the `matched?.RemovePrayer(prayerId)` call. Closes pass 2 for Delete.
3. **Sibling broadcasts.** Extend `SyncAsync`/`SyncCoreAsync` with a `bool skipExpandedPrayerReload = false` (independent of `ChangeKind?`). Pass `true` from the `PrayerCardChangedMessage`, `TagChangedMessage`, and `CardBoxChangedMessage` handlers. None of these mutate any existing card's prayer set, so per-card reload is pure waste at scale (concrete trigger: tag rename with a 55-row card expanded → storms today).
4. **`BulkChangedMessage`** stays as-is. Reload is legitimate (full data replacement after backup restore / deep-link import / confirm-import-save). Could add a backstop "if `IsExpanded && Prayers.Count > 40`, defer reload to next user-driven collapse/expand" later if a future repro shows backup-restore on a 50+ row expanded card jetsams.

---

## Open scope decisions

These are sequencing/release-readiness calls, not architectural. **Decide before merging.**

- **Narrow scope (BUG-80 = Delete only):** ship #1 widened to `Updated || Deleted` and #2 PrayerDeleted suppress flag. Keep sibling broadcasts (#3) as a separate slice. Aligned with the original TPM "hold the line" posture: each new mutation path that crashes is a new defect.
- **Broad scope (BUG-80 = realize-storm class):** ship #1 + #2 + #3 together. Closes the storm class entirely. Aligned with the Staff IC argument: "Without it you ship the same bug as a stream of one-off patches each time someone adds a new mutation source."

If broad scope is chosen, **TPM input on whether 1.3.1 can absorb the larger fix is recommended** — they were emphatic earlier that BUG-79 was the named bug and Delete should be a separate ticket.

The BindableLayout → virtualized `CollectionView` rework (originally "C" in BUG-79) remains a separate slice regardless. It's the long-term fix for the engine itself; the storm-vector audit above is the short-term workaround.

---

## Verification approach

1. **PerfLog re-instrumentation if needed.** The existing PerfLog/diagnostics.log routing is in the working tree. Uncomment the call sites in `SyncCoreAsync` (entry/exit, expanded-reload count, etc.) only if a new repro mode needs evidence; the existing trace already covers the realize-storm pattern.
2. **Manual UAT matrix** (run all of these on a card with 55+ prayer requests, expanded):
   - Mark Answered (was BUG-79; B+B4 verified passing in working tree)
   - Delete request (new repro; current target)
   - Edit request (other than Mark Answered) — should already pass under B since it sends `Updated`
   - Create request from outside the card detail flow (e.g., quick-add) — verify `Created` path
   - Rename a tag attached to prayers in this card — verify `TagChangedMessage` path
   - Rename the card itself — verify `PrayerCardChangedMessage(Updated)` path
   - Move card to a different box — verify `PrayerCardChangedMessage(Updated)` + box change path
3. **Regression UAT** on a regular small card to confirm no break on the normal path.
4. **30-prayer card** as a threshold check — confirms the storm scales with N rather than being binary.

---

## File:line pointers

Working tree state (uncommitted on `dev`):
- `PrayerApp/ViewModels/PrayerCardsViewModel.cs` — `SuppressNextOnAppearingSyncForCardId` field, `SyncAsync(ChangeKind?)` overload, `SyncCoreAsync(ChangeKind?)`, BUG-79 guard at the per-card foreach
- `PrayerApp/ViewModels/PrayerCardViewModel.cs` — `ResortPrayers()`, `AddOrUpdatePrayerAsync` calls it
- `PrayerApp/Views/PrayerCard/PrayerCardsPage.xaml.cs` — `OnAppearing` reads/clears suppress flag
- `PrayerApp/Helpers/PerfLog.cs` — file-write routing via `IDiagnosticLog`
- `PrayerApp/Services/IDiagnosticLog.cs` + `DiagnosticLog.cs` — new `Log(string, string)` overload

Untouched but in scope for the consolidated fix:
- `PrayerApp/Messages/EntityChangedMessage.cs` — message records and `ChangeKind` enum
- `PrayerApp/Services/PrayerService.cs` — broadcasts `PrayerChangedMessage`
- `PrayerApp/Services/CardService.cs` — broadcasts `PrayerCardChangedMessage`
- `PrayerApp/Services/TagService.cs` — broadcasts `TagChangedMessage` and `BulkChangedMessage`
- `PrayerApp/Services/BoxService.cs` — broadcasts `CardBoxChangedMessage` and `BulkChangedMessage`
- `PrayerApp/ViewModels/PrayerCardViewModel.cs` — `LoadPrayersAsync` sort criteria (currently duplicated in `ResortPrayers`; `/simplify` flagged extracting a private `SortPrayers` static)
- `PrayerApp/ViewModels/PrayerCardsViewModel.cs` — `RebuildSections` (out of scope but referenced)

---

## Pending `/simplify` findings on the working-tree code

`/simplify` was run against the working-tree changes before BUG-80 was discovered. The substantive findings (not yet applied):

- Sort-criteria duplicated between `LoadPrayersAsync` and the new `ResortPrayers()`. Recommendation: extract a `private static IOrderedEnumerable<...> SortPrayers(IEnumerable<...>)` mirroring the existing `SortCards` pattern in `PrayerCardsViewModel`.
- `SuppressNextOnAppearingSyncForCardId` is `int?` but only checked for `is null` at the consumer; the card ID is dead data. Either rename to `bool SuppressNextOnAppearingSync` or actually use the ID. Recommendation: rename to bool — the page shows all cards, so there's no per-card scoping to compare against at the consume site.
- The XML doc on `SuppressNextOnAppearingSyncForCardId` is long and partly redundant with the comment in `OnAppearing`. Trim to one canonical location (the VM).
- Indentation regression in `AddOrUpdatePrayerAsync` after the `try`/`finally` was added. Cosmetic but jarring on review.
- PerfLog now does synchronous file I/O under a lock per call; warn explicitly in the XML doc that it must never be uncommented in tight UI loops or per-row DB call sites. Optionally gate behind `#if DEBUG`.

---

## Memos / context to load in the new session

The new session inherits these memos from the project's auto-memory (`MEMORY.md`):

- `feedback_operating_discipline.md` — verify before assert/retract; implementation-first research; root-cause before pivoting.
- `feedback_dispatch_review_before_recommending.md` — for architectural/scope/sequencing decisions, dispatch the configured role agents (`npm-claude-config:staff-software-engineer`, `npm-claude-config:lead-tpm`) BEFORE recommending. Don't free-style.
- `feedback_search_for_existing_service_before_building_one.md` — grep for existing utilities/services before adding new ones.
- `feedback_no_dramatic_language.md` — state findings, cite evidence; no "smoking gun" or reveal-style framing.
- `feedback_no_usb_iphone_pairing.md` — never propose USB tethering. Network pairing only; recover transport flakiness with `NSLog`/file-based logging.
- `feedback_ios_install_use_devicectl_not_mlaunch.md` — install via `xcrun devicectl`, not `dotnet build -t:Run`.
- `feedback_ios_install_freshness_check.md` — bump `<ApplicationVersion>` for fresh install; bundle UUID is the freshness anchor.

The full PerfLog trace and crash-window syslog excerpts for BUG-79 are held privately in Ken's vault under `Projects/Practicing Prayer/Bugs/`. They are **not** committed to this repo (open-source).

---

## Resolution — broad scope

After independent review by staff-IC and lead-TPM agents (both converged on broad without prompting), the realize-storm class was closed in one fix rather than peeling off one mutation source per ticket.

### Concrete changes vs. the audit's recommended consolidated fix

`PrayerCardsViewModel.cs`:

- **Widened the BUG-79 guard** from `changeKind == ChangeKind.Updated` to `changeKind != null`. Created and Deleted both have in-place handlers in `ApplyQueryAttributes` (PrayerSaved branch → `AddOrUpdatePrayerAsync`; PrayerDeleted branch → `RemovePrayer`). For Created broadcasts that originate outside the detail flow (`QuickAddViewModel.SavePrayerAsync` writing to the system "Quick Add" card from the Home tab), CardsPage's next OnAppearing on tab-return runs `SyncAsync()` with no skip and reconciles fresh — verified by tracing QuickAdd's presentation as a sheet modal over `MainPage` (sibling tab to `PrayerCardsPage`).
- **Threaded `bool skipExpandedPrayerReload`** through `SyncAsync(ChangeKind?, bool)` and `SyncCoreAsync(ChangeKind?, bool)`. Inside the foreach, both reasons combine into one local: `var skipReload = changeKind != null || skipExpandedPrayerReload;`.
- **Sibling messenger handlers** (`PrayerCardChangedMessage`, `TagChangedMessage`, `CardBoxChangedMessage`) now register with `vm.SyncAsync(null, skipExpandedPrayerReload: true)`. None mutate any card's prayer set.
- **`BulkChangedMessage` left unguarded** — full data replacement after backup-restore / deep-link import / confirm-import legitimately requires the per-card reload.
- **Mirrored B4 for Delete:** the PrayerDeleted branch in `ApplyQueryAttributes` now sets `SuppressNextOnAppearingSync = true` after `matched?.RemovePrayer(prayerId)`, closing the pass-2 storm on page-pop.
- **Comment block at the foreach** names both BUG-79 and BUG-80, explains why `RefreshActivePrayerCount` and `BuildCardTagLookupAsync` still run unconditionally, and points back at this doc.

`PrayerCardsPage.xaml.cs` and `PrayerCardViewModel.cs` were not touched by the BUG-80 fix itself — the consume site for `SuppressNextOnAppearingSync` and the in-place `RemovePrayer` were already wired by the BUG-79 commit.

### What did NOT change

- The `BindableLayout` → virtualized `CollectionView` rework (originally "C" in BUG-79). Still a separate slice. The storm-vector audit is the short-term workaround; virtualization is the long-term fix for the engine itself.
- `ItemSizingStrategy="MeasureAllItems"` on the outer grouped `CollectionView`. Same.
- `BulkChangedMessage`-driven backstop (defer reload on a 50+ row expanded card after backup-restore). Deferred until a real repro shows it's needed.

### UAT matrix (manual, pending)

Run all on a card with 55+ prayer requests, expanded, on iPhone 17 / iOS 26.4:

1. **Mark Answered** — was BUG-79; B+B4 verified passing in working tree before broad fix.
2. **Delete request** — was BUG-80 named repro.
3. **Edit request** (other than Mark Answered).
4. **Create request from outside the card detail flow** — QuickAdd from Home tab, then switch back to Cards tab.
5. **Rename a tag attached to prayers in this card** — verify tag chips update; no jetsam.
6. **Rename the card itself.**
7. **Move card to a different box.**
8. **Backup restore** — verify reload still runs (must NOT be skipped).
9. **Regression** on a regular small card — confirm normal path unbroken.
10. **30-prayer card** as threshold check.

# Cache Invalidation & Property Notification Audit

**Date:** 2026-03-26
**Scope:** All service caches, all ViewModel property notifications, cross-tab freshness, OnAppearing lifecycle
**Auditor:** Claude (automated code review)

---

## Summary

The codebase is generally well-structured with consistent cache invalidation and property notification patterns. The audit found **5 findings** across cache invalidation and property notification completeness.

- **CRITICAL findings:** 0
- **MODERATE findings:** 3
- **LOW findings:** 2

---

## Part 1: Cache Invalidation Audit

### Service Cache Inventory

| Service | Cache Fields | Invalidation Method |
|---------|-------------|-------------------|
| `CardService` | `_cache` | `InvalidateCache()` |
| `PrayerService` | `_allCache`, `_cardCache` | `InvalidateCache()` |
| `TagService` | `_cache` | `InvalidateCache()` |
| `UserColorService` | (none) | N/A |
| `BackupService` | (none) | Invalidates others on restore |
| `PrayerInteractionService` | (none) | N/A |

### CardService (`PrayerApp/Services/CardService.cs`)

| Mutation Method | Own Cache Invalidated? | Cross-Service Impact |
|----------------|----------------------|---------------------|
| `GetOrCreateQuickAddCardAsync()` (line 33) | YES (`_cache = null` line 34) | None needed |
| `SaveCardAsync()` (line 38) | YES (`_cache = null` line 41) | None needed |
| `DeleteCardAsync()` (line 45) | YES (`_cache = null` line 48) | None needed |

**Verdict:** CLEAN. All mutations invalidate the cache.

### PrayerService (`PrayerApp/Services/PrayerService.cs`)

| Mutation Method | Own Caches Invalidated? | Cross-Service Impact |
|----------------|------------------------|---------------------|
| `SavePrayerAsync()` (line 48) | YES (both caches, lines 51-52) | None needed |
| `DeletePrayerAsync()` (line 56) | YES (both caches, lines 61-62) | None needed |

**Verdict:** CLEAN. All mutations invalidate both `_allCache` and `_cardCache`.

### TagService (`PrayerApp/Services/TagService.cs`)

| Mutation Method | Own Cache Invalidated? | Cross-Service Impact |
|----------------|----------------------|---------------------|
| `AddTagToRequestAsync()` (line 51) | YES (line 61) | None needed |
| `RemoveTagFromRequestAsync()` (line 64) | YES (line 73) | None needed |
| `SaveTagAsync()` (line 88) | YES (line 91) | None needed |
| `DeleteTagAsync()` (line 95) | YES (line 103) | None needed |
| `ReassignColorAsync()` (line 106) | YES (line 115) | None needed |
| `ClearAllAssignmentsForTagAsync()` (line 126) | YES (line 129) | None needed |
| `SeedSystemTagsAsync()` (line 132) | YES (line 147) | None needed |

**Verdict:** CLEAN. All mutations invalidate the cache.

**Note on `ReassignColorAsync()` (line 106-123):** The cache is invalidated at line 115 *before* the loop that calls `tag.SaveAsync()`. Each individual `SaveAsync()` inside the loop does NOT re-invalidate the cache. This is acceptable because the cache was already nulled out, so subsequent `GetTagsAsync()` calls will hit the DB. However, if any code were to call `GetTagsAsync()` *during* the loop iteration, it would rebuild the cache with partially-updated data. This is a theoretical race condition but unlikely in practice since the method is awaited.

### BackupService (`PrayerApp/Services/BackupService.cs`)

**ImportAsync() (line 80):**
After DB replacement (line 133-136):
- `_cardService.InvalidateCache()` - YES
- `_prayerService.InvalidateCache()` - YES
- `_tagService.InvalidateCache()` - YES

**Verdict:** CLEAN. All three service caches are invalidated after restore.

### UserColorService (`PrayerApp/Services/UserColorService.cs`)

No cache fields. All reads go directly to `_dbService.GetAllAsync<UserColor>()`.

**Verdict:** N/A. No cache to invalidate.

### PrayerInteractionService (`PrayerApp/Services/PrayerInteractionService.cs`)

No cache fields. `LogInteractionAsync()` writes directly.

**[FINDING 1 - MODERATE]** `PrayerInteractionService.LogInteractionAsync()` writes a `PrayerInteraction` record but does NOT invalidate `PrayerService._allCache` or `_cardCache`. While `PrayerInteraction` is a separate table and the prayer caches store `Prayer` objects (not interactions), the `PrayerService.GetOverduePrayersAsync()` method (line 65) derives its results from interaction data via `_dbService.GetLatestInteractionByPrayerAsync()`. After logging an interaction during prayer time, the overdue list shown on the Home tab may be stale because `GetOverduePrayersAsync()` uses the cached `_allCache` (via `GetAllActivePrayersAsync()`) and the interaction query is uncached. The *interaction query itself* is always fresh (no cache), but the *prayer list* feeding it may be stale. However, since `HomeViewModel.LoadAsync()` is called on every `OnAppearing()` and `PrayerService` caches are separate from interaction data, this is low risk in practice.

**Revised verdict:** LOW risk. The interaction query itself is not cached, and `HomeViewModel.LoadAsync()` always runs fresh. Downgrade to informational.

---

## Part 2: ViewModel Property Notification Audit

### PrayerCardViewModel (`PrayerApp/ViewModels/PrayerCardViewModel.cs`)

**`RefreshProperties()` (line 305-315) covers:**
- `Id`, `Title`, `IsFavorite`, `IsSystem`, `IsNew`, `CanDelete`, `HasPrayers`, `IsAnswered`

**Missing from `RefreshProperties()`:**
- `Identifier` (line 36) - derived from `_prayerCard.Id`. When `_prayerCard` is replaced in `LoadPrayerCardAsync()`, `Identifier` changes but is not notified.
- `ShowBadge` (line 132) - derived from `IsExpanded`, not from `_prayerCard`, so not needed in RefreshProperties.
- `ActivePrayerCount` - uses `SetProperty`, so self-notifying. Not needed.
- `IsHighlighted` - uses `SetProperty`, so self-notifying. Not needed.

**[FINDING 2 - LOW]** `PrayerCardViewModel.RefreshProperties()` does not notify `Identifier` (line 36). `Identifier` is `_prayerCard.Id.ToString()`. After `LoadPrayerCardAsync()` replaces `_prayerCard` (line 288), `Identifier` reflects the new ID. However, `Id` IS notified, and `Identifier` is only used as a string lookup key in `ApplyQueryAttributes()` — it is not typically bound in XAML. This is LOW risk since `Identifier` is a navigation/lookup key, not a UI-bound property.

**File:** `PrayerApp/ViewModels/PrayerCardViewModel.cs`, line 305
**What's missing:** `OnPropertyChanged(nameof(Identifier))` in `RefreshProperties()`

### PrayerCardsViewModel (`PrayerApp/ViewModels/PrayerCardsViewModel.cs`)

**`LoadAsync()` (line 178):** Invalidates card cache, rebuilds AllPrayerCards, calls ApplySorting() which calls ApplyFilter(). CLEAN.

**`RefreshAsync()` (line 330):** Invalidates both card and prayer caches, detects new/deleted cards, refreshes counts and expanded cards. CLEAN.

**`ApplyFilter()` (line 255):** Rebuilds `FilteredPrayerCards` from `AllPrayerCards`. CLEAN.

**Verdict:** CLEAN. No missing notifications.

### PrayerRequestDetailViewModel (`PrayerApp/ViewModels/PrayerRequestDetailViewModel.cs`)

**`RefreshProperties()` (line 747-774) covers:**
- `Id`, `Title`, `Details`, `PrayerCardId`, `CanNotify`, `IsAnswered`, `AnsweredAt`, `AnsweredAtDisplay`, `CreatedAt`, `UpdatedAt`, `Identifier`, `PrayerFrequency`, `PrayerFrequencyDisplay`, `NotifyTime`, `ShowNotifyTime`, `ShowDayOfWeek`, `ShowDayOfMonth`, `SelectedDayOfWeek`, `SelectedDayOfMonth`, `IsReadOnly`, `IsEditable`, `IsNotAnswered`, `IsNew`, `ShowSaveAndNew`, `CardTitle`

**Missing from `RefreshProperties()`:**
- `HasTags` (line 51) - derived from `SelectedTags.Count`. Tags are loaded separately in `LoadTagsAsync()`, and `SelectedTags.CollectionChanged` fires `HasTags` notification. NOT missing.
- `HasSuggestions` (line 52) - same pattern as HasTags. NOT missing.

**Verdict:** CLEAN. Comprehensive coverage.

### PrayerListViewModel (`PrayerApp/ViewModels/PrayerListViewModel.cs`)

**`LoadAsync()` (line 122):**
- Loads prayers, cards, tags, overdue set
- Builds ViewModels, tag chips
- Calls `ApplyFilter()`
- Notifies `HasTags`

**`RefreshAsync()` (line 445):**
- Invalidates prayer cache
- Syncs prayer list (add/remove/update)
- Rebuilds card lookup, tag lookup, overdue set
- Syncs tag chips
- Notifies `HasTags`
- Calls `ApplyFilter()`

**[FINDING 3 - MODERATE]** `PrayerListViewModel.RefreshAsync()` (line 447) invalidates `_prayerService` cache but does NOT invalidate `_cardService` cache. When a card is renamed on the Cards tab and the user switches to the Prayers tab, `RefreshAsync()` calls `_cardService.GetCardsAsync()` (line 463) which may return stale card titles from the card cache. The card cache was invalidated by `CardService.SaveCardAsync()` when the card was saved, so this is actually fine — the card service invalidates its own cache on save. **FALSE POSITIVE** — retracted.

**Revised analysis:** The `_cardService` cache is already invalidated by `CardService.SaveCardAsync()` at the point of mutation, so `RefreshAsync()` calling `GetCardsAsync()` will get fresh data. CLEAN.

**`ApplyFilter()` (line 313):**
- Filters by status, tag chips, search text
- Sorts by CardTitle then Title

**Verdict:** CLEAN.

### HomeViewModel (`PrayerApp/ViewModels/HomeViewModel.cs`)

**`LoadAsync()` (line 91):**
- Loads overdue prayers, updates `OverdueCount`
- Loads cards for lookup
- Builds `SuggestedPrayers`
- Loads last interaction date, updates `LastPrayedDisplay`
- Notifies `OverdueEmptyDescription`

**[FINDING 3 - MODERATE]** `HomeViewModel.LoadAsync()` does NOT invalidate any caches before loading. It is called on every `OnAppearing()` (see `MainPage.xaml.cs` line 49). If the user was on another tab that mutated data (e.g., marked a prayer as answered on Prayers tab), the prayer service caches were invalidated by that mutation. BUT if the user returns to Home tab without going through a mutation path, the caches contain data from the last query. Since `PrayerService.GetOverduePrayersAsync()` calls `GetAllActivePrayersAsync()` which calls `GetAllPrayersAsync()` which returns `_allCache` if set, the Home tab could show stale overdue data if:
1. User opens Home (caches populated)
2. User switches to Cards tab (caches invalidated by RefreshAsync)
3. User switches back to Home (caches repopulated by step 2's RefreshAsync)
4. External change happens (e.g., notification marks prayer as answered)
5. User is already on Home — no OnAppearing fires, stale data shown

In practice, the only mutation paths are user-initiated (save/delete), and those paths invalidate the relevant caches. The real risk is: **after prayer time ends and the user returns to Home, interactions were logged but prayer caches are still populated from step 1.** However, prayer time logs interactions to a different table, and `GetOverduePrayersAsync()` queries interactions from the DB directly (uncached). The prayer list itself (`GetAllActivePrayersAsync()`) uses the cache, but prayers aren't mutated during prayer time — only interactions are logged.

**Revised verdict:** The `_allCache` in PrayerService may be stale when HomeViewModel.LoadAsync() runs, but the mutations that matter (save/delete prayer) already invalidate it. The interaction-based overdue query hits the DB directly. LOW risk in practice but worth noting:

**File:** `PrayerApp/ViewModels/HomeViewModel.cs`, line 91
**What's missing:** No `_prayerService.InvalidateCache()` call before loading. Unlike `PrayerCardsViewModel.RefreshAsync()` and `PrayerListViewModel.RefreshAsync()` which both invalidate caches, `HomeViewModel.LoadAsync()` trusts that caches are already invalidated by prior mutations.

### TagDetailViewModel (`PrayerApp/ViewModels/TagDetailViewModel.cs`)

**`LoadAsync()` (line 136):**
- Notifies: `IsSystem`, `IsExisting`, `IsNameEditable`, `Name`
- Sets `SelectedColorHex` (which triggers `SetProperty` and swatch refresh)
- Calls `CaptureOriginals()`

**Verdict:** CLEAN. All relevant properties are notified.

### TagsViewModel (`PrayerApp/ViewModels/TagsViewModel.cs`)

**`LoadAsync()` (line 36):**
- Clears and rebuilds `Tags` collection
- No direct property notifications needed (ObservableCollection handles UI updates)

**`RefreshAsync()` (line 56):**
- Detects new/deleted tags
- Updates existing tags via `TagItemViewModel.Update()` which notifies `Name` and `DotColor`

**[FINDING 4 - MODERATE]** `TagsViewModel.RefreshAsync()` (line 56) does NOT invalidate `_tagService` cache before calling `_tagService.GetTagsAsync()`. If a tag was created/edited on a different page (e.g., through PrayerDetailPage's tag creation), the tag service invalidates its own cache during that mutation. So this is actually fine — the service self-invalidates on mutation.

**Revised verdict:** CLEAN. The tag service invalidates its own cache on any write operation.

**`TagItemViewModel.Update()` (line 107):**
- Notifies `Name` and `DotColor`

**Missing from `TagItemViewModel.Update()`:**
- `IsSystem` (line 93) — if `IsSystem` changed, the UI wouldn't update. But system status never changes after creation, so this is a non-issue.

**Verdict:** CLEAN.

### PrayerTimeViewModel (`PrayerApp/ViewModels/PrayerTimeViewModel.cs`)

**`CurrentIndex` setter (line 59):**
- Notifies: `CurrentEntry`, `ProgressDisplay`, `HasPrevious`, `HasNext`

**`LoadEntriesAsync()` (line 226):**
- After setting `Entries`, manually fires: `CurrentEntry`, `ProgressDisplay`, `HasPrevious`, `HasNext`
- Sets `HasCompleted` via `SetProperty`

**Verdict:** CLEAN. All dependent properties are notified.

### PrayerTimeScopeViewModel (`PrayerApp/ViewModels/PrayerTimeScopeViewModel.cs`)

Simple ViewModel that loads tags and navigates. No mutation paths or missing notifications.

**Verdict:** CLEAN.

---

## Part 3: Cross-Tab Freshness Audit

### Tab Navigation Pattern

| Tab | Page | OnAppearing Pattern | Cache Handling |
|-----|------|-------------------|----------------|
| Home | `MainPage` | Always calls `LoadAsync()` | Does NOT invalidate caches |
| Cards | `PrayerCardsPage` | First: `LoadAsync()`, Subsequent: `RefreshAsync()` | `RefreshAsync()` invalidates card + prayer caches |
| Prayers | `PrayerListPage` | First: `LoadAsync()`, Subsequent: `RefreshAsync()` | `RefreshAsync()` invalidates prayer cache |
| Tags | `TagsPage` | First: `LoadAsync()`, Subsequent: `RefreshAsync()` | `RefreshAsync()` does NOT invalidate tag cache |
| Settings | `SettingsHubPage` | No OnAppearing logic | N/A |

### Cross-Tab Mutation Paths

**QuickAdd (Home) → Cards Tab:**
- QuickAdd saves via `PrayerService.SavePrayerAsync()` → invalidates prayer caches
- QuickAdd may create system card via `CardService.GetOrCreateQuickAddCardAsync()` → invalidates card cache
- QuickAdd also calls `_prayerService.InvalidateCache()` explicitly (line 54 of QuickAddViewModel)
- Cards tab `RefreshAsync()` invalidates both caches again — CLEAN

**QuickAdd (Home) → Prayers Tab:**
- Same as above — prayer cache already invalidated by save
- Prayers tab `RefreshAsync()` invalidates prayer cache again — CLEAN

**Prayer Detail (Cards) → Prayers Tab:**
- Prayer saved via `PrayerService.SavePrayerAsync()` → invalidates prayer caches
- Prayers tab `RefreshAsync()` picks up the change — CLEAN

**Tag Edit (Tags) → Prayers Tab:**
- Tag saved via `TagService.SaveTagAsync()` → invalidates tag cache
- Prayers tab `RefreshAsync()` calls `_tagService.GetTagsAsync()` which returns fresh data — CLEAN

**Prayer Time → Home Tab:**
- Interactions logged via `PrayerInteractionService` (no cache impact on prayers)
- Tags may be removed via `TagService.RemoveTagFromRequestAsync()` → invalidates tag cache
- Home `LoadAsync()` uses `GetOverduePrayersAsync()` which queries interactions from DB directly — CLEAN

**[FINDING 5 - MODERATE]** `HomeViewModel.LoadAsync()` does not invalidate caches before re-querying. While most mutation paths self-invalidate caches at the point of mutation, there is an asymmetry: `PrayerCardsViewModel.RefreshAsync()` and `PrayerListViewModel.RefreshAsync()` both defensively invalidate caches on every call, while `HomeViewModel.LoadAsync()` does not. This means if a cache was somehow populated but not invalidated (edge case), Home would show stale data while other tabs would not. For consistency and defensive programming, `HomeViewModel.LoadAsync()` should invalidate `_prayerService` cache before querying.

**File:** `PrayerApp/ViewModels/HomeViewModel.cs`, line 91-117
**Recommendation:** Add `_prayerService.InvalidateCache();` at the start of `LoadAsync()`.

---

## Part 4: OnAppearing Lifecycle Audit

### Pages with `_loaded` Guard Pattern

| Page | Guard Variable | First Visit | Subsequent Visits |
|------|---------------|-------------|-------------------|
| `PrayerCardsPage` | `_loaded` | `vm.LoadAsync()` | `vm.RefreshAsync()` |
| `PrayerListPage` | `_loaded` | `vm.LoadAsync()` | `vm.RefreshAsync()` |
| `TagsPage` | `_loaded` | `_vm.LoadAsync()` | `_vm.RefreshAsync()` |
| `PrayerDetailPage` | `_initialLoadComplete` | Subscribe to PropertyChanged | `vm.Reload()` |

### Pages without Guard (Always Load)

| Page | OnAppearing Behavior | Assessment |
|------|---------------------|------------|
| `MainPage` | Always calls `_homeViewModel.LoadAsync()` | Acceptable — home dashboard should always be fresh |
| `PrayerTimePage` | Locks landscape, advances onboarding | No data loading in OnAppearing — data loads via `ApplyQueryAttributes` |
| `SettingsHubPage` | No OnAppearing override | No data to load |
| `PrayerCardPage` | No OnAppearing override | Data loads via `ApplyQueryAttributes` |
| `TagDetailPage` | No OnAppearing override | Data loads via `ApplyQueryAttributes` |
| `QuickAddPage` | No OnAppearing override | Modal — always fresh instance |

**Verdict:** CLEAN. All tab pages properly use the `_loaded` guard pattern to distinguish first visit from subsequent visits. Non-tab pages (modals, detail pages) load via query attributes.

---

## Consolidated Findings

### FINDING 1 (LOW) — HomeViewModel does not invalidate caches before loading

**File:** `PrayerApp/ViewModels/HomeViewModel.cs`, line 91
**Issue:** `LoadAsync()` does not call `_prayerService.InvalidateCache()` before querying, unlike the equivalent `RefreshAsync()` methods in `PrayerCardsViewModel` and `PrayerListViewModel`. This creates an asymmetry where stale cached data could theoretically surface on the Home tab.
**Risk:** LOW — mutations self-invalidate caches, and `LoadAsync()` runs on every `OnAppearing()`.
**Recommendation:** Add `_prayerService.InvalidateCache();` at the start of `LoadAsync()` for consistency with other tabs.

### FINDING 2 (LOW) — PrayerCardViewModel.RefreshProperties() does not notify Identifier

**File:** `PrayerApp/ViewModels/PrayerCardViewModel.cs`, line 305
**Issue:** `Identifier` (derived from `_prayerCard.Id`) is not included in `RefreshProperties()`.
**Risk:** LOW — `Identifier` is used as a navigation key, not typically bound in XAML.
**Recommendation:** Add `OnPropertyChanged(nameof(Identifier));` to `RefreshProperties()` for completeness.

### FINDING 3 (MODERATE) — TagsViewModel.RefreshAsync() does not invalidate tag cache

**File:** `PrayerApp/ViewModels/TagsViewModel.cs`, line 56
**Issue:** `RefreshAsync()` calls `_tagService.GetTagsAsync()` without first invalidating the tag cache. If the tag cache was populated earlier in the session and no tag mutation occurred, it would return stale data.
**Risk:** MODERATE — if a tag was created via `PrayerRequestDetailViewModel.SubmitTagEntryAsync()` (which does invalidate the cache via `SaveTagAsync`), this is fine. But if some future code path creates tags without going through `TagService`, this could break.
**Actual risk assessment:** LOW in current codebase because all tag mutations go through `TagService` which self-invalidates. But for consistency with `PrayerCardsViewModel.RefreshAsync()` and `PrayerListViewModel.RefreshAsync()`:
**Recommendation:** Add `_tagService.InvalidateCache();` at the start of `RefreshAsync()`.

---

## No Issues Found In

- **CardService** — All mutations invalidate cache
- **PrayerService** — All mutations invalidate both caches
- **TagService** — All mutations invalidate cache
- **BackupService** — Properly invalidates all three service caches on restore
- **UserColorService** — No cache to invalidate
- **PrayerCardsViewModel** — Proper cache invalidation and property notifications
- **PrayerRequestDetailViewModel** — Comprehensive RefreshProperties() coverage
- **PrayerListViewModel** — Proper cache invalidation and cross-tab refresh
- **PrayerTimeViewModel** — All dependent properties notified on index change
- **PrayerTimeScopeViewModel** — No mutation paths
- **All OnAppearing patterns** — Correct first-load vs subsequent-visit logic
- **Cross-tab QuickAdd flow** — Properly invalidates caches for all downstream tabs

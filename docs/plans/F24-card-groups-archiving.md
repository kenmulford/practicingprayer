# F-24: Boxes (Card Groups & Archiving) — Implementation Plan

## Context

Users accumulate 50–100+ prayer cards with no organizational layer above them. Search and tag filters help discovery but don't solve layout density. Multiple users asked for "a folder or something." This feature introduces **Boxes** — collapsible section headers on the existing Cards page — as the top-level organizational structure. The word "Box" is intentional: prayer cards are a real physical object, people keep them in boxes.

---

## Mockups

Interactive HTML mockup with 7 screens, light/dark mode, and Boxes/Folders naming toggle:
**`mockups/boxes.html`** — served via `.claude/launch.json` config `f24-boxes-mockup` on port 3457.

### Mockup screens

| Screen | What it shows |
|--------|---------------|
| **Cards** | Normal state — collapsible box sections, card-level accordion (expand to see requests), sticky headers, System badges on Quick Add/Shared with Me |
| **Upgrade** | First launch after update — warm gold banner explaining the feature, all user cards in Unboxed/Unorganized (expanded), System box expanded with Quick Add + Shared with Me, empty Archived collapsed |
| **Multi-select** | Long-press mode — green action bar with selection count + "Move to…" button. Tapping "Move to…" opens folder picker sheet (user folders + Unorganized + Cancel). Works across all expanded sections. |
| **Create Card** | New card form — Title entry, optional folder picker defaulting to "Unboxed"/"Unorganized" (implying that's where it goes unless changed), Notifications toggle |
| **Manage** | Box list — swipe right = Edit (green), swipe left = Delete (red). System rows are read-only with badges. Delete triggers action sheet. |
| **New Box** | Name entry form, Save greyed (empty), keyboard |
| **Edit Box** | Pre-filled name, active Save, keyboard |

### Key decisions reflected in mockup

- **Internal model**: `CardBox`. External naming TBD (leaning Folders). `BoxStrings` class makes rename a 1-file edit.
- **Unboxed label**: "Unboxed" (Boxes mode) / "Unorganized" (Folders mode). NOT "Unsorted" — that implies sort order.
- Card meta shows request count only — no "last prayed" date (prayer time is per-request, not per-card)
- Toolbar: "Boxes"/"Folders" + "Add Card" text buttons (matches real app nav pattern)
- **Card-level accordion**: tapping a card expands it to show its prayer requests inline (▸/▾ arrow). One card per box can be open at a time; requests show title + Answered badge.
- **Sticky box section headers**: section header pins to top while scrolling through its cards, pushed off by the next section. MAUI grouped CollectionView default (note: iOS only by default — Android needs workaround, see Technical Risks #2).
- **System chip on prayer cards**: Quick Add and Shared with Me cards display a "System" chip badge (matching existing TagsPage `Border` + "System" label style). Applied via `IsSystem` property already on `PrayerCard`.
- **System card icons**: Quick Add → ⚡ bolt icon (`bolt_solid_full.svg` — saved). Shared with Me → 🔗 link icon (`link_solid_full.svg` — saved). Both use existing `.png` + `IconTintColorBehavior` pattern. Requires `SystemKey` property on `PrayerCard` (`"quick_add"` / `"shared_with_me"`) for icon mapping. SVG assets are committed to `Resources/Images/FontAwesomeIcons/` and wired in `.csproj`.
- **Manage swipe pattern**: swipe RIGHT → Edit (green, `SwipeView.LeftItems`), swipe LEFT → Delete (red, `SwipeView.RightItems`) — matches TagsPage exactly. **BUG-7 must be resolved first** (pan/swipe gesture conflict on Android confirmed by gestures skill).
- **Delete confirmation**: action sheet with "Unassign Cards" (safe), "Delete All Cards & Requests" (danger red), "Cancel".
- **Upgrade banner**: warm gold, dismissable, explains feature + calls out Quick Add/Shared with Me migration. System folder stays expanded while banner is visible.
- **Multi-select**: cross-section (not limited to one folder). Primary use case is post-upgrade bulk organization from Unboxed. "Move to…" picker lists user folders + Unorganized + Cancel.
- **Create Card picker**: defaults to Unboxed/Unorganized — implicitly communicates where the card goes if no folder is chosen.
- **Archived box**: no muted/greyed styling. Cards inside look identical to any other card. The box name "Archived" is the only signal.

---

## Design Summary

- **Boxes are collapsible section headers** on the Cards page — not a new navigation level. The core loop (open → cards → pray) is unchanged.
- **Two system boxes** are seeded automatically: **System** (home for Quick Add and Shared with Me cards) and **Archived** (hidden cards). Users cannot rename or delete them.
- **Card.IsImported is retired**: imported full-card shares now live in the System box instead of using a flag. Prayer.IsImported is unaffected.
- **One box per card.** Cards with no box assigned appear in an "Unboxed" section at the top of the list. Unboxed is collapsible like any other section.
- **Box assignment is optional** on the card creation screen. Unboxed is the default.
- **Box headers**: full-width, flat (no card border), triangle ▶ right = collapsed, ▼ down = expanded. Card count shown in header.
- **Sort order on Cards page**: Unboxed (top) → User boxes (A→Z) → System box → Archived box (bottom, always visible, collapsed by default, no special muting — it's a normal system folder).
- **Box deletion prompt**: two options — "Unassign All Cards" (default) or "Delete All Cards & Requests" (danger style).
- **Manage Boxes** entry point: Settings Hub row (same pattern as AppSettings, Backup, About, Help) + toolbar icon on Cards page.

---

## Data Model

### New: `CardBox`
**File to create:** `PrayerApp/Models/CardBox.cs`

```
CardBox
  Id          int  [PK, AutoIncrement]
  Name        string [MaxLength(50)]
  IsSystem    bool [default false]
  SystemKey   string? [MaxLength(20)] — "system" | "archived" | null
  SortOrder   int [default 0]
  CreatedAt   DateTime
  UpdatedAt   DateTime
```

Active Record pattern (matches PrayerTag.cs exactly): `SetDBService()`, `SaveAsync()`, `DeleteAsync()`, `LoadAsync(id)`, `LoadAllAsync()`.

### Modified: `PrayerCard`
**File:** `PrayerApp/Models/PrayerCard.cs`

Add three columns:
- `BoxId` (int, default 0 — 0 = Unboxed)
- `IsArchived` (bool, default false — derived flag kept as a fast filter; true when BoxId = ArchivedBoxId)
- `SystemKey` (string?, nullable — `"quick_add"` or `"shared_with_me"` for the two system cards, null for all user cards). Used by `PrayerCardViewModel.SystemIconSource` to return the right icon asset.

**Archive semantics (confirmed):** Archiving is purely a box assignment — `card.BoxId = archivedBoxId`. Reassigning a card to *any* other box (including Unboxed) automatically unarchives it. No separate "unarchive" operation exists. Prayer request status (IsAnswered, etc.) is never affected by card box assignment.

`IsImported` stays in the model for DB compatibility but is no longer set on new cards. Existing IsImported=true cards get migrated to the System box.

### DB Migrations (in `DBService.UpdateSchema()`)
**File:** `PrayerApp/Services/DBService.cs`

```sql
-- New table
CREATE TABLE IF NOT EXISTS CardBox (...)  -- via CreateTableAsync<CardBox>()

-- New columns on PrayerCard
ALTER TABLE PrayerCard ADD COLUMN BoxId INTEGER DEFAULT 0
ALTER TABLE PrayerCard ADD COLUMN IsArchived INTEGER DEFAULT 0
```

Plus a one-time data migration:
- Seed "System" box (IsSystem=true, SystemKey="system", SortOrder=900) if not exists
- Seed "Archived" box (IsSystem=true, SystemKey="archived", SortOrder=999) if not exists
- `UPDATE PrayerCard SET BoxId = [SystemBoxId] WHERE IsImported = 1`
- `UPDATE PrayerCard SET BoxId = [SystemBoxId] WHERE IsSystem = 1` (Quick Add + Shared with Me)

`CardBox.SetDBService()` called in `MauiProgram.cs` alongside other models.

---

## Service Layer

### New: `IBoxService` / `BoxService`
**Files to create:** `PrayerApp/Services/IBoxService.cs`, `PrayerApp/Services/BoxService.cs`

```csharp
public interface IBoxService
{
    Task<IReadOnlyList<CardBox>> GetBoxesAsync();         // all boxes, sorted
    Task<CardBox?> GetSystemBoxAsync(string systemKey);   // "system" | "archived"
    Task<CardBox> SaveBoxAsync(CardBox box);
    Task DeleteBoxAsync(int boxId, bool deleteCards);       // false = unassign, true = delete cascade
    void InvalidateCache();
}
```

Cache pattern matches `CardService` / `TagService`.

`DeleteBoxAsync(boxId, deleteCards: false)` → calls `IDBService.UnassignBoxFromCardsAsync(boxId)` then deletes box.
`DeleteBoxAsync(boxId, deleteCards: true)` → loads cards in box, deletes each (cascades to prayers via existing `PrayerService.DeletePrayerAsync`), then deletes box.

### Modified: `ICardService` / `CardService`
**Files:** `PrayerApp/Services/ICardService.cs`, `PrayerApp/Services/CardService.cs`

```csharp
Task AssignBoxAsync(PrayerCard card, int boxId);
// boxId = ArchivedBoxId  → sets BoxId + IsArchived=true
// boxId = anything else  → sets BoxId + IsArchived=false (unarchive is implicit)
// boxId = 0              → Unboxed, IsArchived=false
```

No separate `UnarchiveCardAsync` — reassignment to any box handles it.

### Modified: `IDBService` / `DBService`
**Files:** `PrayerApp/Services/IDBService.cs`, `PrayerApp/Services/DBService.cs`

```csharp
Task UnassignBoxFromCardsAsync(int boxId);          // UPDATE PrayerCard SET BoxId=0 WHERE BoxId=?
Task<List<PrayerCard>> GetCardsByBoxIdAsync(int boxId);
```

---

## Cards Page Restructure

### New: `BoxSectionViewModel`
**File to create:** `PrayerApp/ViewModels/BoxSectionViewModel.cs`

```csharp
public class BoxSectionViewModel : ObservableObject, IEnumerable<PrayerCardViewModel>
{
    public string Name { get; }
    public int CardCount { get; }
    public bool IsExpanded { get; set; }   // [ObservableProperty]
    public bool IsSystem { get; }
    public string? SystemKey { get; }
    public ObservableCollection<PrayerCardViewModel> Cards { get; }
    // IEnumerable<PrayerCardViewModel> required by CollectionView grouping
}
```

`IsExpanded` defaults: user boxes → true, System box → true, Archived box → false (always collapsed until user taps).

### Modified: `PrayerCardsViewModel`
**File:** `PrayerApp/ViewModels/PrayerCardsViewModel.cs`

- `FilteredPrayerCards` (flat `ObservableCollection<PrayerCardViewModel>`) replaced by `BoxSections` (`ObservableCollection<BoxSectionViewModel>`)
- `ApplySorting()` builds sections in order: Unboxed → user boxes (A→Z) → System → Archived
- `ApplyFilter()` filters within each section; empty sections hidden (except Archived — always shown even when empty)
- Archived section collapsed by default; `ShowArchived` toggle not needed since it's always visible at bottom
- **Search and tag filter auto-expand:** When any search text or tag chip filter is active, any section that contains a qualifying card is automatically expanded, overriding its user-set collapsed state. When the filter is cleared, sections return to their previous expansion states (saved in `BoxSectionViewModel`). This ensures users never miss a match hidden inside a collapsed box.
- Tag chip filter works as today — shows cards that have unanswered prayer requests matching the selected tags, across all sections.

### Modified: `PrayerCardsPage.xaml`
**File:** `PrayerApp/Views/PrayerCard/PrayerCardsPage.xaml`

Switch CollectionView to grouped:
```xml
<CollectionView IsGrouped="True" ItemsSource="{Binding BoxSections}">
    <CollectionView.GroupHeaderTemplate>
        <DataTemplate x:DataType="viewModels:BoxSectionViewModel">
            <!-- Full-width tap target, flat (no border), triangle arrow + name + count -->
            <Grid ColumnDefinitions="Auto, *, Auto" Padding="0,12,0,8">
                <Label Text="{Binding IsExpanded, Converter={StaticResource BoolToTriangle}}"
                       FontSize="11" VerticalOptions="Center" Margin="0,0,8,0"/>
                <Label Grid.Column="1" Text="{Binding Name}" Style="{StaticResource SectionHeader}"/>
                <Label Grid.Column="2" Text="{Binding CardCount, StringFormat='{0}'}"
                       Style="{StaticResource MutedText}"/>
            </Grid>
        </DataTemplate>
    </CollectionView.GroupHeaderTemplate>
    <!-- Existing ItemTemplate unchanged -->
</CollectionView>
```

`BoolToTriangle` converter: true → "▼", false → "▶".

Tapping a box header toggles `IsExpanded`. When `IsExpanded = false`, the section's `Cards` collection is set to empty (items hidden); when true, restored. This is the standard MAUI grouped CollectionView collapse pattern.

**New toolbar icon** on Cards page: "Boxes" (or folder icon) → navigates to `BoxesPage`.

### Modified: `PrayerCardViewModel`
**File:** `PrayerApp/ViewModels/PrayerCardViewModel.cs`

- Add `IsArchived` property (mirrors `card.IsArchived`)
- Add `ArchiveCommand` — calls `CardService.AssignBoxAsync(card, archivedBoxId)`; no separate UnarchiveCommand
- Add "Archive" action chip alongside Favorite/Share/Edit/Delete (shown when `!IsArchived && !IsSystem`)
- Cards in the Archived section show a "Move to…" or box-picker action instead (reassigning to any box unarchives implicitly)
- Prayer request status (IsAnswered, AnsweredAt, etc.) is never touched by archive/reassign

---

## Box Management UI

### New: `BoxesPage` + `BoxDetailPage`
**Files to create:**
- `PrayerApp/Views/Boxes/BoxesPage.xaml` + `.cs`
- `PrayerApp/Views/Boxes/BoxDetailPage.xaml` + `.cs`

Follows `TagsPage` / `TagDetailPage` pattern exactly:
- List with swipe Edit / Delete per item
- System boxes (System, Archived): edit disabled, delete disabled, "System" badge shown
- User boxes: full edit + delete with prompt
- "+" toolbar item → creates new box
- `BoxDetailPage`: name entry (FormLabel + Entry), Save toolbar button

**Delete prompt** (in `BoxItemViewModel.DeleteCommand`):
```
Title: "Delete "[Name]"?"
Message: "What should happen to the cards in this box?"
Button 1: "Unassign Cards" (default/secondary style)
Button 2: "Delete All Cards" (danger style)
Button 3: "Cancel"
```

### New: `BoxesViewModel` + `BoxDetailViewModel` + `BoxItemViewModel`
**Files to create:** `PrayerApp/ViewModels/BoxesViewModel.cs`, `BoxDetailViewModel.cs`, `BoxItemViewModel.cs`

Pattern: identical to `TagsViewModel`, `TagDetailViewModel`, `TagItemViewModel`.

### Modified: `SettingsHubPage`
**Files:** `PrayerApp/Views/Settings/SettingsHubPage.xaml` + `.cs`

Add "Manage Boxes" row (same tap-to-navigate pattern as existing rows).

---

## Card Creation/Edit: Box Picker

### Modified: `PrayerCardPage.xaml`
**File:** `PrayerApp/Views/PrayerCard/PrayerCardPage.xaml`

Add optional Picker below Title:
```xml
<!-- FormLabel above, full-width Picker below -->
<Label Text="Box (optional)" Style="{StaticResource FormLabel}"/>
<Picker ItemsSource="{Binding AvailableBoxes}"
        ItemDisplayBinding="{Binding Name}"
        SelectedItem="{Binding SelectedBox}"/>
```

`AvailableBoxes` = user-created boxes only (System and Archived excluded — you can't create a card directly into Archived). Blank option at top = Unboxed.

### Modified: `PrayerCardDetailViewModel` (card create/edit VM)
- Add `AvailableBoxes` (loaded from `IBoxService`)
- Add `SelectedBox` (nullable `CardBox`)
- On save: `card.BoxId = SelectedBox?.Id ?? 0`

---

## Routing & Registration

### Modified: `Routes.cs`
```csharp
public const string BoxesPage = "BoxesPage";
public const string BoxDetailPage = "BoxDetailPage";
```

### Modified: `AppShell.xaml.cs`
```csharp
Routing.RegisterRoute(nameof(BoxesPage), typeof(BoxesPage));
Routing.RegisterRoute(nameof(BoxDetailPage), typeof(BoxDetailPage));
```

### Modified: `MauiProgram.cs`
```csharp
builder.Services.AddSingleton<IBoxService, BoxService>();
builder.Services.AddTransient<BoxesViewModel>();
builder.Services.AddTransient<BoxDetailViewModel>();
builder.Services.AddTransient<BoxesPage>();
builder.Services.AddTransient<BoxDetailPage>();
// ...
CardBox.SetDBService(myDBService);
```

---

## New/Modified Files Summary

| File | Status |
|------|--------|
| `Models/CardBox.cs` | **New** |
| `Models/PrayerCard.cs` | Modified — BoxId, IsArchived |
| `Services/IBoxService.cs` | **New** |
| `Services/BoxService.cs` | **New** |
| `Services/ICardService.cs` | Modified — archive methods |
| `Services/CardService.cs` | Modified — archive methods |
| `Services/IDBService.cs` | Modified — 2 new queries |
| `Services/DBService.cs` | Modified — new table, migrations, seeding |
| `ViewModels/BoxSectionViewModel.cs` | **New** |
| `ViewModels/BoxesViewModel.cs` | **New** |
| `ViewModels/BoxDetailViewModel.cs` | **New** |
| `ViewModels/BoxItemViewModel.cs` | **New** |
| `ViewModels/PrayerCardsViewModel.cs` | Modified — grouped sections |
| `ViewModels/PrayerCardViewModel.cs` | Modified — archive commands |
| `ViewModels/PrayerCardDetailViewModel.cs` | Modified — box picker |
| `Views/Boxes/BoxesPage.xaml` + `.cs` | **New** |
| `Views/Boxes/BoxDetailPage.xaml` + `.cs` | **New** |
| `Views/PrayerCard/PrayerCardsPage.xaml` | Modified — grouped CollectionView |
| `Views/PrayerCard/PrayerCardPage.xaml` | Modified — box picker |
| `Views/Settings/SettingsHubPage.xaml` + `.cs` | Modified — Manage Boxes row |
| `Converters/BoolToTriangleConverter.cs` | **New** (or inline) |
| `Routes.cs` | Modified |
| `AppShell.xaml.cs` | Modified |
| `MauiProgram.cs` | Modified |
| `PrayerApp.Tests.csproj` | Modified — Compile Include for new files |

---

## Phasing

**Phase 1 — Data + Archiving** (implement first, ship independently)
- CardBox model, DBService migrations + seeding
- IBoxService / BoxService (read + archive operations only)
- PrayerCard.BoxId + IsArchived, CardService archive methods
- PrayerCardViewModel archive/unarchive commands + action chip
- Data migration: IsImported cards → System box
- Unit tests for all of the above

**Phase 2 — Cards Page Restructure**
- BoxSectionViewModel + BoolToTriangleConverter
- PrayerCardsViewModel grouped sections
- PrayerCardsPage grouped CollectionView + box header template
- Unit tests for section building and filtering

**Phase 3 — Box Management UI + Card Assignment**
- BoxesPage, BoxDetailPage, all three ViewModels
- SettingsHubPage "Manage Boxes" row
- Cards page toolbar entry point
- PrayerCardPage box picker + PrayerCardDetailViewModel changes
- Routes, AppShell, MauiProgram registration
- Unit tests for BoxService CRUD + delete-with-prompt logic

---

## Verification

1. Fresh install: System and Archived boxes seeded; Quick Add + Shared with Me cards in System box
2. Upgrade from existing build: IsImported cards migrated to System box; existing cards unboxed (BoxId=0)
3. Cards page: Unboxed section at top, user boxes A→Z, System box, Archived box collapsed at bottom
4. Box header tap: collapses/expands section; triangle arrow updates
5. Archive a card → moves to Archived section; reassign to any other box → automatically unarchived; prayer request status unchanged throughout
6. Create card → optional box picker shows user boxes only, not System/Archived
7. Create box → appears in list alphabetically; edit name → updates everywhere
8. Delete box (unassign) → cards move to Unboxed section
9. Delete box (delete all) → cards + requests gone; danger confirmation required
10. Search → filters across all sections; any section with a matching card auto-expands; empty sections hidden (Archived always stays visible)
11. Tag filter chips → same auto-expand behavior as search
12. `dotnet test` — all existing 283 tests pass + new tests green

---

## External Naming (TBD)

Internal model is `CardBox` throughout. The user-visible word ("Box", "Folder", etc.) is **not decided** and must be easy to change at release time.

**Approach:** All visible strings go through a single `BoxStrings` static class (or equivalent `AppStrings` entry):

```csharp
public static class BoxStrings
{
    public const string Word        = "Box";          // "Box" or "Folder"
    public const string Plural      = "Boxes";        // "Boxes" or "Folders"
    public const string Unorganized = "Unboxed";      // "Unboxed" (Boxes) or "Unorganized" (Folders)
    // Note: NOT "Unsorted" — that implies a sort order. Cards have no enforced sort.
}
```

All XAML strings and ViewModel display text reference this. Changing the visible word at any point is a single-file edit. Current leaning: **Folders** — but "Boxes" remains the working name until release.

---

## Mass Card Assignment

**Problem:** Upgrading users with 50+ cards in "Unorganized" need a way to assign them to folders without editing each card one-by-one. Without this, the Folders feature creates more friction than it solves on day one.

**Recommended approach: Multi-select mode**
- Long-press any card on the Cards page → enters selection mode (checkmarks appear on all visible cards across all expanded sections)
- Tap additional cards to select/deselect — works across section boundaries (e.g., 2 from Unorganized + 1 from Family)
- Toolbar (green bar): **Cancel** (left, exits selection mode entirely — no deselecting one-by-one) + **selection count** (center) + **"Move to…"** (right, opens folder picker)
- Tapping "Move to…" opens a picker listing all user folders + "Unorganized"
- Confirm → all selected cards get `BoxId` updated in one batch
- Exit selection mode (tap "Done" or back)

This is the standard pattern from email, file managers, and photo apps — zero learning curve.

**Deferred:** Drag-and-drop reordering and drag-to-folder. Platform gesture conflicts (pan vs drag on Android) make this risky. Multi-select covers the same use case more reliably. Drag-and-drop can be a future polish item if demand warrants it.

**Phasing:** Multi-select should ship in **Phase 3** alongside the Manage Folders UI, or as an immediate follow-up. It's critical for the upgrade experience — the feature is incomplete without it.

---

## User Education (UX-28)

F-24 changes the Cards page layout visibly for all existing users. Two distinct audiences:

**New users** — Folders can be introduced in onboarding as a light optional step. No urgency; they have no existing habits to disrupt.

**Upgrading users** — Higher priority. Their card list changes on first launch: section headers appear, System folder is new, all their existing cards are in "Unboxed." They need reassurance that nothing was lost.

Pattern: one-time dismissable banner on the Cards page (same as UX-15 Quick Add tip), shown only on the first launch after the F-24 upgrade. Suggested copy:
> *"We've added Folders to help organize your cards. Your existing cards are all in Unboxed — nothing was moved or deleted."*

**Decision before F-24 ships:** whether UX-28 ships simultaneously or as an immediate dot release. It should not be left open-ended — users will be confused without it.

---

## Technical Risks

Genuine challenges that need careful handling during implementation. Everything else follows established patterns.

### 1. Grouped CollectionView collapse — no native MAUI support
MAUI's grouped `CollectionView` does not natively collapse groups. The plan uses **collection mutation**: when `BoxSectionViewModel.IsExpanded = false`, its `Cards` ObservableCollection is cleared (triggering a CollectionChanged notification); when expanded, cards are restored from a private backing list. This works but requires the backing list to be maintained accurately at all times, especially during filter/sort rebuilds.

### 2. Sticky section headers — iOS only by default
Grouped `CollectionView` section headers are sticky on iOS but **scroll with content on Android**. Ken confirmed sticky headers are desired. This needs a cross-platform solution during Phase 2 — either a custom ItemsLayout, a platform handler, or (simplest) a flat `ObservableCollection` with a `DataTemplateSelector` distinguishing header vs card rows instead of grouped CollectionView.
> **Risk level: Medium.** May require revisiting the grouped CollectionView approach entirely in favour of a flat list + DataTemplateSelector. Decide this before writing Phase 2 code.

### 3. Search/filter must save and restore box expansion state
When a search is active, all boxes with matches auto-expand. When the search is cleared, boxes return to what they were before. `BoxSectionViewModel` needs a `_userIsExpanded` field that survives filter cycles. Rebuild logic must distinguish "expanded because of a filter" from "expanded by user choice."

### 4. System box seeding order in UpdateSchema()
`DBService.UpdateSchema()` runs migrations sequentially. The seed for System + Archived `CardBox` rows must execute **before** the `UPDATE PrayerCard SET BoxId = ...` data migration that references their IDs. Order:
1. `CreateTableAsync<CardBox>()`
2. Seed System box, capture its Id
3. Seed Archived box, capture its Id
4. `ALTER TABLE PrayerCard ADD COLUMN BoxId`
5. `ALTER TABLE PrayerCard ADD COLUMN IsArchived`
6. `UPDATE PrayerCard SET BoxId = [systemId] WHERE IsSystem = 1 OR IsImported = 1`

### 5. BUG-7 gate
The Boxes management page uses SwipeView (same left/right split pattern as TagsPage). BUG-7 must be resolved and verified on both platforms before Phase 3 ships the Boxes list. Do not repeat the Tags swipe problem in a new page.

### 6. Notifications continue through archive
Archived cards' prayer requests retain their notification schedules. Archiving ≠ muting. This is intentional and confirmed. No notification changes needed for F-24.

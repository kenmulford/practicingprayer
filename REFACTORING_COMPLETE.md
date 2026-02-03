# Prayer App Schema Refactoring - Complete

## Summary of Changes

This document outlines the complete refactoring from a Category-based architecture to a Card-based architecture with a flexible tag system.

## 1. Database Schema Changes

### Old Schema
```
PrayerCategory (categories for grouping prayers)
  - Prayer (renamed to "Prayer Request")
    - PrayerCategoryId (FK to PrayerCategory)
  - PrayerInteraction (removed - will revisit later)
```

### New Schema
```
PrayerCard (formerly Prayer, now primary container for prayer requests)
  - Title
  - Details
  - PrayerFrequency
  - CanNotify
  - IsAnswered
  - Timestamps

PrayerTag (new - global reusable tags)
  - Name (unique)
  - Color (hex code, optional)
  - Timestamps

PrayerCardTag (junction table - many-to-many relationship)
  - PrayerCardId (FK)
  - PrayerTagId (FK)
  - CreatedAt
```

## 2. Model Changes

### New Models Created
- **`PrayerCard.cs`** - Renamed from Prayer, removed PrayerCategoryId
- **`PrayerTag.cs`** - New model for reusable tags with optional color coding
- **`PrayerCardTag.cs`** - Junction table for many-to-many relationship

### Models Removed
- **`Prayer.cs`** (functionality moved to PrayerCard)
- **`PrayerCategory.cs`** (cards now replace categories)
- **`PrayerInteraction.cs`** (deferred - to be revisited when feature is designed)

## 3. Service Changes

### New Services
- **`ITagService`** / **`TagService`** - Manages tag operations and card-tag relationships
- **`ICardService`** / **`CardService`** - Replaces ICategoryService for card operations

### Updated Services
- **`IDBService`** - Added methods for junction table queries:
  - `GetByCardIdAsync(int prayerCardId)`
  - `GetByTagIdAsync(int prayerTagId)`
  - `DeleteByCardIdAsync(int prayerCardId)`
  - `DeleteByTagIdAsync(int prayerTagId)`

- **`DBService`** - Updated schema creation and seeding:
  - Now creates PrayerCard, PrayerTag, and PrayerCardTag tables
  - Removed PrayerCategory and PrayerInteraction tables
  - Updated seed data (old categories become prayer cards)
  - Added commented-out stub for seeding tags (ready for implementation)

## 4. ViewModel Changes

### Renamed ViewModels
- `PrayerListViewModel` ? **`PrayerCardListViewModel`**
  - Now works with PrayerCard collection
  - Binds to `AllPrayerCards` instead of `AllPrayers`

- `PrayerDetailViewModel` ? **`PrayerCardDetailViewModel`**
  - Removed category picker and related logic
  - Added `TagSelectionViewModel` for multi-select tag UI
  - Now uses `PrayerCard` model

- `PrayerCategoryViewModel` ? **`PrayerCardViewModel`**
  - Updated to work with PrayerCard properties
  - Removed category-specific fields

- `PrayerCategoriesViewModel` ? **`PrayerCardsViewModel`**
  - Works with PrayerCard collection
  - Displays all prayer cards instead of categories

### New ViewModels
- **`PrayerTagSelectionViewModel`** - Manages multi-select tag UI
  - `PrayerTagSelectionViewModel` - Main ViewModel for tag selection
  - `PrayerTagItemViewModel` - Individual tag item with toggle logic
  - Separated for testing and separation of concerns
  - Handles tag toggling and persistence via `ITagService`

## 5. View Changes

### Directory Restructuring
- Old: `Views/PrayerCategory/` ? New: `Views/PrayerCard/`
  - `PrayerCategoriesPage` ? **`PrayerCardsPage`**
  - `PrayerCategoryPage` ? **`PrayerCardPage`**

### Updated Views
- **`PrayerCardsPage.xaml`** - New card management page
  - Lists all prayer cards
  - Shows title and preview of details
  - Swipe to delete functionality

- **`PrayerCardPage.xaml`** - New individual card editor
  - Edit title and details
  - Save/delete options

- **`PrayerDetailPage.xaml`** - Updated prayer request editor
  - Replaced category picker with tag multi-select
  - Tag selection uses CheckBox UI
  - Integrated `PrayerTagSelectionViewModel`

- **`PrayerListPage.xaml`** - Updated prayer list
  - Now uses `PrayerCardListViewModel`
  - Updated data bindings to use PrayerCard properties

### Shell & Navigation
- **`AppShell.xaml`** - Updated routes and tab labels
  - Tab: "Categories" ? "Cards"
  - Route: "CategoriesPage" ? "CardsPage"
  - Updated DataTemplate bindings

- **`AppShell.xaml.cs`** - Updated route registration
  - Now registers `PrayerCardPage` from `PrayerCard` namespace

## 6. Startup Configuration

### `MauiProgram.cs` Updates
- Service registration:
  - Removed: `ICategoryService` (replaced with `ICardService`)
  - Added: `ITagService`
  - Added: `ICardService`

- Model setup:
  - `Prayer.SetDBService()` ? `PrayerCard.SetDBService()`
  - Added: `PrayerTag.SetDBService()`
  - Added: `PrayerCardTag.SetDBService()`
  - Removed: `PrayerInteraction.SetDBService()`

## 7. Migration Path

### Database Reset
- Since this is early-stage development with no production data:
  - Database is completely rebuilt on first run
  - `Settings.FirstRun = true` triggers full migration
  - Old PrayerCategory records don't migrate (they become prayer cards instead)

### Seed Data
- **Old categories** ? **New prayer cards** (as card titles)
  - "General" ? Prayer card titled "General"
  - "Where I Live" ? Prayer card titled "Where I Live"
  - etc.

- **Tags** - Stubbed out for later implementation
  - Commented code ready to define initial tags
  - Can be populated when tag taxonomy is finalized

## 8. Architecture Benefits

### Improved Design
1. **Flexibility**: Cards are first-class citizens, not just containers for prayers
2. **Tag System**: Many-to-many relationships allow rich categorization
3. **Color Coding**: Optional tag colors for visual organization
4. **Separation of Concerns**: `PrayerTagSelectionViewModel` separated for testing
5. **Scalability**: Easy to add more tag properties (icon, description, etc.) later

### Future-Ready
- Tag system foundation ready for:
  - Tag creation UI
  - Filtering by tags
  - Tag hierarchies/nesting
  - Tag statistics and analytics

## 9. Files Modified Summary

### New Files (12)
- `Models/PrayerCard.cs`
- `Models/PrayerTag.cs`
- `Models/PrayerCardTag.cs`
- `Services/ITagService.cs`
- `Services/TagService.cs`
- `Services/ICardService.cs`
- `Services/CardService.cs`
- `ViewModels/PrayerTagSelectionViewModel.cs`
- `ViewModels/PrayerCardsViewModel.cs`
- `ViewModels/PrayerCardViewModel.cs`
- `Views/PrayerCard/PrayerCardsPage.xaml` & `.xaml.cs`
- `Views/PrayerCard/PrayerCardPage.xaml` & `.xaml.cs`

### Modified Files (11)
- `Services/IDBService.cs` (added junction table methods)
- `Services/DBService.cs` (new schema)
- `Services/CardService.cs` (new)
- `ViewModels/PrayerDetailViewModel.cs` (renamed to PrayerCardDetailViewModel)
- `ViewModels/PrayerListViewModel.cs` (renamed to PrayerCardListViewModel)
- `Views/Prayer/PrayerDetailPage.xaml` (tag selection UI)
- `Views/Prayer/PrayerDetailPage.xaml.cs` (updated binding context)
- `Views/Prayer/PrayerListPage.xaml` (updated bindings)
- `Views/Prayer/PrayerListPage.xaml.cs` (updated bindings)
- `AppShell.xaml` (updated routes and tabs)
- `AppShell.xaml.cs` (updated route registration)
- `MauiProgram.cs` (service registration and model setup)

### Files to Delete (Optional Cleanup)
- `Models/Prayer.cs`
- `Models/PrayerCategory.cs`
- `Models/PrayerInteraction.cs`
- `Services/ICategoryService.cs`
- `Services/CategoryService.cs`
- `ViewModels/PrayerCategoryViewModel.cs`
- `ViewModels/PrayerCategoriesViewModel.cs`
- `Views/PrayerCategory/` (entire directory)

## 10. Next Steps / TODO

### Immediate
- [ ] Test the application end-to-end
- [ ] Verify database creation and seeding
- [ ] Test tag selection UI in prayer detail page
- [ ] Clean up old files (optional)

### Short-term
- [ ] Implement tag creation UI
- [ ] Add filtering by tags
- [ ] Seed initial tags in DBService (uncomment stub code)

### Medium-term
- [ ] Add tag management page
- [ ] Tag statistics/analytics
- [ ] Prayer card templates

### Revisit (As Designed)
- [ ] PrayerInteraction feature (determine use case and schema)


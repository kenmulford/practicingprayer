# Prayer App — Session Context

> **Read this at the start of every session.**
> This file captures permanent architectural decisions, coding conventions, and
> known gotchas so they don't need to be rediscovered. Update it when new decisions
> are made. See BACKLOG.md for the current work queue.

---

## 0. Git Workflow

- **Feature branches**: `feature/<short-name>` off `dev`
- **PRs always target `dev`** — never open a PR directly to `master`
- `master` is only updated by merging `dev` at release milestones
- Branch naming examples: `feature/f7-home-personalization`, `feature/f9-ui-review`

---

## 1. Project Overview

**Prayer App** — a personal devotional mobile app for managing prayer requests,
prayer cards, and prayer sessions. Built for **personal use first**, eventual
App Store publishing later.

- **Target platforms**: Android + iOS only (`net10.0-android`, `net10.0-ios`)
- **Framework**: .NET MAUI 10
- **Architecture**: MVVM with CommunityToolkit.Mvvm + service layer + SQLite persistence
- **Solution**: `PrayerApp.sln` contains `PrayerApp/` (app) and `PrayerApp.Tests/` (tests)

---

## 2. Key Packages (locked versions)

| Package | Version | Notes |
|---------|---------|-------|
| `Microsoft.Maui.Controls` | 10.0.41 | Minimum required by CommunityToolkit.Maui 14. Do not downgrade. |
| `CommunityToolkit.Maui` | 14.0.1 | Requires `UseMauiCommunityToolkit()` in `MauiProgram.cs` |
| `CommunityToolkit.Maui.Core` | 14.0.1 | |
| `CommunityToolkit.Mvvm` | 8.4.0 | `[ObservableProperty]`, `[RelayCommand]`, `ObservableObject` |
| `sqlite-net-pcl` | 1.9.172 | Active Record model pattern |
| `Plugin.LocalNotification` | 13.0.0 | |
| `Microsoft.Extensions.Http` | 10.0.2 | Ready for future API calls (Bible verse, etc.) |

---

## 3. Architecture

### 3.1 Layer Overview

```
Views (XAML)
  └── ViewModels (CommunityToolkit.Mvvm)
        └── Services (singletons, cached)
              └── Models (Active Record, SQLite)
                    └── IDBService / DBService
```

### 3.2 Active Record Models

Models carry their own persistence logic via a static `IDBService` reference.
**All models must be initialized at startup** in `MauiProgram.cs`:

```csharp
PrayerCard.SetDBService(myDBService);
PrayerTag.SetDBService(myDBService);
PrayerCardTag.SetDBService(myDBService);
Prayer.SetDBService(myDBService);
PrayerInteraction.SetDBService(myDBService);
```

Each model has: `SaveAsync()`, `DeleteAsync()`, `LoadAsync(id)`, `LoadAllAsync()`,
and type-specific query methods (e.g. `Prayer.LoadByCardIdAsync(cardId)`).

### 3.3 Service Layer (Singletons with Cache)

All services are registered as **singletons** in `MauiProgram.cs`.
Cache pattern: read → populate `_cache` → return. Write → null `_cache` → return.

| Service | Cache type |
|---------|-----------|
| `CardService` | `IReadOnlyList<PrayerCard>?` single list |
| `PrayerService` | `Dictionary<int, List<Prayer>>?` per-card + `List<Prayer>?` all |
| `TagService` | `IReadOnlyList<PrayerTag>?` single list |

### 3.4 ViewModel DI

ViewModels resolve services via service locator (not constructor injection):
```csharp
_cardService = IPlatformApplication.Current!.Services.GetRequiredService<ICardService>();
```
This is intentional — MAUI's XAML requires parameterless constructors. When adding a
new ViewModel, resolve all services this way. **Do NOT inject via constructor unless
you also provide a parameterless constructor for XAML.**

### 3.5 Database Schema

| Table | Key columns |
|-------|-------------|
| `PrayerCard` | Id, Title, CanNotify, PrayerFrequency, IsAnswered, IsFavorite, CreatedAt, UpdatedAt |
| `Prayer` (table: "PrayerRequest") | Id, PrayerCardId (FK), Title, Details, CanNotify, PrayerFrequency, IsAnswered, AnsweredAt, CreatedAt, UpdatedAt |
| `PrayerTag` | Id, Name, Color (hex string, nullable) |
| `PrayerCardTag` | Id, PrayerCardId (FK), PrayerTagId (FK), CreatedAt |
| `PrayerInteraction` | Id, PrayerId (FK), InteractionType ("Prayed"), InteractionAt |

Migration logic lives in `DBService.UpdateSchema()`. Schema changes go there.

---

## 4. Navigation

- App uses **Shell navigation** with tabs (Cards, Prayers, Prayer Time, Settings)
- **Modal pages** pushed via: `Shell.Current.Navigation.PushModalAsync(page)`
  - ⚠️ `Shell.Current.PushModalAsync` was **removed in MAUI 10.0.41**. Always use
    `Shell.Current.Navigation.PushModalAsync`.
- Route registration: `Routing.RegisterRoute("RouteName", typeof(PageClass))` in
  `AppShell.xaml.cs`
- Navigation with query params: `await Shell.Current.GoToAsync($"RouteName?key=value")`
- ViewModels that receive params implement `IQueryAttributable.ApplyQueryAttributes()`

---

## 5. Styling Conventions

- **Theme**: dark-first with `AppThemeBinding` for light mode support
- **Color tokens**: always use `{StaticResource Primary}`, `{StaticResource Secondary}`,
  `{AppThemeBinding Light=..., Dark=...}` — **never hardcode hex values in XAML or C#**
- `BoolToMutedColorConverter`: resolves `Gray400` / `OffBlack` from
  `Application.Current.Resources` at runtime (supports theme switching)
- **Card borders**: use `Style="{StaticResource PrayerCardBorder}"` on every `Border`
  element that wraps a card. Defined in `Resources/Styles/Styles.xaml`.
  Properties: Secondary bg, Primary stroke 1.5px, 8px corner radius.
- **Named styles** for reusable UI patterns go in `Styles.xaml` with `x:Key`

---

## 6. Prayer Time Page

- Forces **landscape orientation** on enter; restores portrait on exit
- `IOrientationService` has platform implementations in `Platforms/Android/` and
  `Platforms/iOS/OrientationService.cs`
- **Auto mode**: `IDispatcherTimer` (MAUI, not `System.Timers.Timer`). 30s hardcoded
  (TD-5 in backlog will make this user-selectable). Methods: `StartAutoMode()`,
  `StopAutoMode()`, `PauseAutoMode()`, `ResumeAutoMode()`.
- **Background pause**: `Window.Stopped` → `PauseAutoMode()`;
  `Window.Resumed` → `ResumeAutoMode()`. Wired in `PrayerTimePage.xaml.cs`.
- `PrayerTimeViewModel` implements `IQueryAttributable` receiving `scope` ("all"/"tags")
  and `tagIds` (comma-separated)

---

## 7. Unit Tests (`PrayerApp.Tests/`)

- Target: `net10.0` — no MAUI, no platform dependencies
- Source files linked from main project via `<Compile Include="../PrayerApp/...">` —
  no project reference (main project targets mobile only)
- **Mocking**: `NSubstitute 5.3.0` mocks `IDBService`; static `SetDBService()` called
  per-test constructor
- **Parallelization disabled**: `[assembly: CollectionBehavior(DisableTestParallelization = true)]`
  because model static `_dbService` fields are shared state
- **Not tested** (Shell/Application dependencies): ViewModels, Views, DBService,
  NotificationService, Settings, OrientationService
- **CI**: `.github/workflows/ci.yml` — runs on push to `master`/`dev`/`feature/**`
  and PRs to `master`/`dev`

---

## 8. Known Gotchas

| Gotcha | Detail |
|--------|--------|
| `Shell.PushModalAsync` removed | Use `Shell.Current.Navigation.PushModalAsync(page)` |
| `DisplayAlert` deprecated | Use `DisplayAlertAsync` instead (warnings in existing code) |
| `IDispatcherTimer` for timers | Not `System.Timers.Timer` — timers must run on the UI thread in MAUI |
| Package version lock | `CommunityToolkit.Maui 14.0.1` requires `Microsoft.Maui.Controls >= 10.0.41` |
| Prayer table name mismatch | `Prayer` model uses `[Table("PrayerRequest")]` — the SQLite table is named `PrayerRequest`, not `Prayer` |
| SQLite page size warning | `SQLitePCLRaw.lib.e_sqlite3.android 2.1.2` emits XA0141 warning re: Android 16 16KB pages. Third-party issue; no action needed yet. |
| PAT scope for CI | Pushing `.github/workflows/` requires the `workflow` scope on your GitHub PAT |
| Test project source links | If you add a new non-MAUI service/model to the main project, add a matching `<Compile Include>` in `PrayerApp.Tests.csproj` and write tests for it |

---

## 9. File Structure

```
PrayerApp/
  Models/
    Prayer.cs             — prayer request ("PrayerRequest" table)
    PrayerCard.cs         — card grouping prayer requests
    PrayerCardTag.cs      — junction: card ↔ tag
    PrayerFrequency.cs    — enum: Daily, Weekly, Monthly, etc.
    PrayerInteraction.cs  — session log per prayer
    PrayerTag.cs          — reusable tag with optional color
  Services/
    DBService.cs / IDBService.cs       — SQLite CRUD + schema mgmt
    CardService.cs / ICardService.cs   — card CRUD + cache
    PrayerService.cs / IPrayerService.cs
    TagService.cs / ITagService.cs
    PrayerInteractionService.cs / IPrayerInteractionService.cs
    NotificationService.cs / INotificationService.cs
    Settings.cs                         — Preferences-backed app settings
  Platforms/
    Android/OrientationService.cs      — lock landscape
    iOS/OrientationService.cs
  ViewModels/
    PrayerCardsViewModel.cs            — cards list + accordion
    PrayerCardViewModel.cs             — single card + prayers list
    PrayerListViewModel.cs             — flat prayer list
    PrayerRequestDetailViewModel.cs    — view/edit a prayer request
    PrayerTagSelectionViewModel.cs     — multi-select tag picker
    PrayerTimeViewModel.cs             — session state machine + auto-mode
    PrayerTimeScopeViewModel.cs        — scope selector (all / tags)
    QuickAddViewModel.cs
  Views/
    PrayerCard/PrayerCardsPage.xaml    — card grid with accordion rows
    PrayerCard/PrayerCardPage.xaml     — edit a card's metadata
    Prayer/PrayerListPage.xaml
    Prayer/PrayerDetailPage.xaml       — view/edit a prayer request
    PrayerTime/PrayerTimeScopePage.xaml
    PrayerTime/PrayerTimePage.xaml     — landscape session player
    MainPage.xaml                      — home (greeting + quick actions)
    QuickAddPage.xaml
    Settings.xaml
  Resources/
    Styles/Colors.xaml                 — Primary, Secondary, Gray*, etc.
    Styles/Styles.xaml                 — named styles incl. PrayerCardBorder
    AppIcon/, Fonts/, Images/, Splash/

PrayerApp.Tests/
  Services/
    CardServiceTests.cs          — 8 tests
    PrayerServiceTests.cs        — 12 tests
    TagServiceTests.cs           — 14 tests
    PrayerInteractionServiceTests.cs — 4 tests

.github/workflows/ci.yml
BACKLOG.md                       — prioritized work queue (update every session)
CONTEXT.md                       — this file (update when architecture changes)
```

---

*Last updated: 2026-03-09*

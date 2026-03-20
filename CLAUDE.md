# Practicing Prayer — CLAUDE.md

> Read this at the start of every session. See BACKLOG.md for the current work queue.

---

## Vision

- Digital tool for personal prayer/worship — prayer cards, requests, prayer time sessions
- Privacy-first: fully offline, no accounts, no backend, no social features
- Will NOT integrate with Church CRM/ChMS systems — others don't need access to this data
- Core loop: **prayer cards → prayer requests → prayer time**. Everything else supports this.
- Tags, colors, and organization features serve user personalization — not the core
- Audience: niche app store listing for anyone seeking a prayer journaling tool
- May integrate with strategic online tools that deepen the experience without violating privacy-first principle
- Priorities: security, stability, UX above all

---

## Tech Stack

| Package | Version | Notes |
|---------|---------|-------|
| .NET MAUI | 10 | `net10.0-android`, `net10.0-ios` |
| CommunityToolkit.Mvvm | 8.4.0 | `[ObservableProperty]`, `[RelayCommand]`, `ObservableObject` |
| CommunityToolkit.Maui | 14.0.1 | Requires `Microsoft.Maui.Controls >= 10.0.41` — do not downgrade |
| sqlite-net-pcl | 1.9.172 | Active Record model pattern |
| Plugin.LocalNotification | 13.0.0 | Local push notifications |
| xUnit | 2.9.2 | Unit tests |
| NSubstitute | 5.3.0 | Test mocking |

---

## Architecture

```
Views (XAML)
  └── ViewModels (CommunityToolkit.Mvvm)
        └── Services (singletons, cached)
              └── Models (Active Record, SQLite)
                    └── IDBService / DBService
```

- **Models** carry persistence via a static `IDBService` reference set in `MauiProgram.cs`. Each model has `SaveAsync()`, `DeleteAsync()`, `LoadAsync(id)`, `LoadAllAsync()`, plus type-specific queries.
- **Services** are registered as singletons. Cache pattern: read → populate `_cache` → return; write → null `_cache` → return.
- **ViewModels** resolve services via service locator (`IPlatformApplication.Current!.Services.GetRequiredService<>()`), not constructor injection. This is intentional — MAUI XAML requires parameterless constructors.
- **Navigation**: Shell with 5 tabs. Modals via `Shell.Current.Navigation.PushModalAsync(page)`. Route params via `IQueryAttributable.ApplyQueryAttributes()`.

---

## Database Schema

| Table | Key columns |
|-------|-------------|
| `PrayerCard` | Id, Title, CanNotify, PrayerFrequency, IsAnswered, IsFavorite, CreatedAt, UpdatedAt |
| `PrayerRequest` | Id, PrayerCardId (FK), Title, Details, CanNotify, PrayerFrequency, IsAnswered, AnsweredAt, CreatedAt, UpdatedAt |
| `PrayerTag` | Id, Name, Color (hex, nullable) |
| `PrayerCardTag` | Id, PrayerCardId (FK), PrayerTagId (FK), PrayerRequestId (FK), CreatedAt |
| `PrayerInteraction` | Id, PrayerId (FK), InteractionType, InteractionAt |

- The `Prayer` model uses `[Table("PrayerRequest")]` — the SQLite table is `PrayerRequest`, not `Prayer`
- Schema migrations live in `DBService.UpdateSchema()`

---

## Conventions

### Git
- Feature branches off `dev`, PRs target `dev`, `master` updated at release milestones
- Branch naming: `feature/<short-name>` (e.g., `feature/f7-home-personalization`)

### Styling
- Dark-first theme with `AppThemeBinding` for light mode — never hardcode hex in XAML or C#
- Use `StaticResource` color tokens from `Resources/Styles/Colors.xaml`
- Card borders: `Style="{StaticResource PrayerCardBorder}"` on every card-wrapping `Border`
- Named styles for reusable patterns in `Resources/Styles/Styles.xaml`

### Forms
- Label above field, both full-width (`VerticalStackLayout` with `FormLabel` above input)
- Never use side-by-side label+field layout

---

## Testing

- `PrayerApp.Tests/` targets `net10.0` — links source files from main project (not project reference, since main targets mobile only)
- NSubstitute mocks `IDBService`; static `SetDBService()` called per-test constructor
- Parallelization disabled (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`) — model static `_dbService` is shared state
- When adding a new non-MAUI service/model, add a matching `<Compile Include>` in `PrayerApp.Tests.csproj`
- CI: `.github/workflows/ci.yml` runs on push/PR to `master`/`dev`/`feature/**`

---

## Known Gotchas

| Gotcha | Detail |
|--------|--------|
| `Shell.PushModalAsync` removed | Use `Shell.Current.Navigation.PushModalAsync(page)` |
| `DisplayAlert` deprecated | Use `DisplayAlertAsync` instead |
| Timers must use `IDispatcherTimer` | Not `System.Timers.Timer` — MAUI requires UI thread timers |
| Package version lock | `CommunityToolkit.Maui 14.0.1` requires `Microsoft.Maui.Controls >= 10.0.41` |
| Prayer table name mismatch | `Prayer` model → `[Table("PrayerRequest")]` SQLite table |
| SQLite page size warning | XA0141 re: Android 16 16KB pages — third-party issue, no action needed |
| PAT scope for CI | Pushing `.github/workflows/` requires `workflow` scope on GitHub PAT |

---

## Development Discipline

- **TDD**: Write tests first, then implementation. All new features, classes, and models must be designed for testability from the start.
- **Feature-completeness**: Finish what you start. Don't leave partial implementations or TODO stubs.
- **Ask first, assume never**: When requirements are ambiguous, ask clarifying questions before writing code.
- **Before every git commit**: Run `/simplify` to review changed code for reuse, quality, and efficiency. Fix any issues found before committing.

---

## Key Files

- `BACKLOG.md` — prioritized work queue (update every session)
- `docs/plans/` — active feature implementation plans (F-10, F-13)
- `docs/research/` — investigation notes
- `docs/tester-feedback/feedback-log.md` — verbatim tester feedback
- `docs/archive/` — completed plan/spec docs (historical reference)

---

## Learning Context

Ken is growing his understanding of MVVM, service injection patterns, and .NET MAUI best practices. When explaining architectural decisions or suggesting patterns, include brief "why" context — not just "what." This codebase doubles as a learning vehicle.

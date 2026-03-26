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

| Package                  | Version | Notes                                                            |
| ------------------------ | ------- | ---------------------------------------------------------------- |
| .NET MAUI                | 10      | `net10.0-android`, `net10.0-ios`                                 |
| CommunityToolkit.Mvvm    | 8.4.0   | `[ObservableProperty]`, `[RelayCommand]`, `ObservableObject`     |
| CommunityToolkit.Maui    | 14.0.1  | Requires `Microsoft.Maui.Controls >= 10.0.41` — do not downgrade |
| sqlite-net-pcl           | 1.9.172 | Active Record model pattern                                      |
| Plugin.LocalNotification | 13.0.0  | Local push notifications                                         |
| xUnit                    | 2.9.2   | Unit tests                                                       |
| NSubstitute              | 5.3.0   | Test mocking                                                     |

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

| Table               | Key columns                                                                                                     |
| ------------------- | --------------------------------------------------------------------------------------------------------------- |
| `PrayerCard`        | Id, Title, CanNotify, PrayerFrequency, IsAnswered, IsFavorite, CreatedAt, UpdatedAt                             |
| `PrayerRequest`     | Id, PrayerCardId (FK), Title, Details, CanNotify, PrayerFrequency, IsAnswered, AnsweredAt, CreatedAt, UpdatedAt |
| `PrayerTag`         | Id, Name, Color (hex, nullable)                                                                                 |
| `PrayerCardTag`     | Id, PrayerCardId (FK), PrayerTagId (FK), PrayerRequestId (FK), CreatedAt                                        |
| `PrayerInteraction` | Id, PrayerId (FK), InteractionType, InteractionAt                                                               |

- The `Prayer` model uses `[Table("PrayerRequest")]` — the SQLite table is `PrayerRequest`, not `Prayer`
- Schema migrations live in `DBService.UpdateSchema()`

---

## Conventions

### Git

- Feature branches off `dev`, PRs target `dev`, `master` updated at release milestones
- Branch naming: `feature/<short-name>` (e.g., `feature/f7-home-personalization`)

### Styling

- Full light and dark mode support via `AppThemeBinding` — never hardcode hex in XAML or C#
- Use `StaticResource` color tokens from `Resources/Styles/Colors.xaml`
- Card borders: `Style="{StaticResource PrayerCardBorder}"` on every card-wrapping `Border`
- Named styles for reusable patterns in `Resources/Styles/Styles.xaml`
- Any time a new style is defined in code, always verify the key exists in Styles.xaml or Colors.xaml.

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

| Gotcha                             | Detail                                                                       |
| ---------------------------------- | ---------------------------------------------------------------------------- |
| `Shell.PushModalAsync` removed     | Use `Shell.Current.Navigation.PushModalAsync(page)`                          |
| `DisplayAlert` deprecated          | Use `DisplayAlertAsync` instead                                              |
| Timers must use `IDispatcherTimer` | Not `System.Timers.Timer` — MAUI requires UI thread timers                   |
| Package version lock               | `CommunityToolkit.Maui 14.0.1` requires `Microsoft.Maui.Controls >= 10.0.41` |
| Prayer table name mismatch         | `Prayer` model → `[Table("PrayerRequest")]` SQLite table                     |
| SQLite page size warning           | XA0141 re: Android 16 16KB pages — third-party issue, no action needed       |
| PAT scope for CI                   | Pushing `.github/workflows/` requires `workflow` scope on GitHub PAT         |

---

## Development Discipline

- **TDD**: Write tests first, then implementation. All new features, classes, and models must be designed for testability from the start.
- **Feature-completeness**: Finish what you start. Don't leave partial implementations or TODO stubs.
- **Ask first, assume never**: When requirements are ambiguous, ask clarifying questions before writing code.
- **Propose before implementing**: Before writing any fix or feature that touches more than one file or involves cross-component communication, describe the approach in 2-3 sentences and wait for approval. This prevents wasted effort on over-engineered solutions. When multiple approaches exist, present them with trade-offs and a recommendation.
- **Before every git commit**: Run `/simplify` to review changed code for reuse, quality, and efficiency. Fix any issues found before committing. Parallel sub-agents are allowed for `/simplify` reviews.
- **Parallel agents**: Allowed for this project — override the global single-agent rule. Use parallel agents for independent tasks to maximize efficiency.

---

## Skills

A `UserPromptSubmit` hook (`.claude/hooks/maui-skill-hint.py`) automatically detects MAUI-related keywords in each message and injects the relevant skill names as `additionalContext`. When skills are injected, invoke them via the `Skill` tool before implementing.

**Always invoke for any MAUI/XAML implementation work:**
- `maui-skills:maui-current-apis` — guardrail against deprecated/removed APIs

**Triggered automatically by keyword detection:**

| Topic | Skill |
| ----- | ----- |
| Animations, transitions | `maui-skills:maui-animations` |
| Local notifications | `maui-skills:maui-local-notifications` |
| Accessibility / screen readers | `maui-skills:maui-accessibility` |
| CollectionView, lists, scrolling | `maui-skills:maui-collectionview` |
| Shell, navigation, tabs, modals, routes | `maui-skills:maui-shell-navigation` |
| SQLite, database, migrations | `maui-skills:maui-sqlite-database` |
| Data binding, MVVM, ViewModels | `maui-skills:maui-data-binding` |
| Runtime permissions | `maui-skills:maui-permissions` |
| Theming, dark/light mode | `maui-skills:maui-theming` |
| Gestures (swipe, drag, pinch) | `maui-skills:maui-gestures` |
| Safe area, insets, edge-to-edge | `maui-skills:maui-safe-area` |
| Secure storage | `maui-skills:maui-secure-storage` |
| Unit testing, xUnit, NSubstitute | `maui-skills:maui-unit-testing` |
| Performance, optimization | `maui-skills:maui-performance` |
| Custom handlers, native views | `maui-skills:maui-custom-handlers` |
| App lifecycle, background/foreground | `maui-skills:maui-app-lifecycle` |
| App icons, splash screens | `maui-skills:maui-app-icons-splash` |
| File picker, file system | `maui-skills:maui-file-handling` |
| Dependency injection, service registration | `maui-skills:maui-dependency-injection` |

**Not applicable to this project** (privacy-first, offline): `maui-authentication`, `maui-rest-api`, `maui-push-notifications`, `maui-maps`, `maui-geolocation`, `maui-aspire`, xamarin-* migration skills.

---

## Changelog

- **File:** `docs/CHANGELOG.md` — app store release notes, updated continuously
- After every commit that adds user-facing changes (features, improvements, bug fixes), update `docs/CHANGELOG.md` with a concise, user-friendly summary
- Write for end users, not developers — no code references, backlog IDs, or technical jargon
- When Ken tags a new version/build, reset the changelog: update the baseline tag, clear the entries, and start fresh

---

## Key Files

- `BACKLOG.md` — prioritized work queue (update every session)
- `docs/CHANGELOG.md` — app store release notes (update every session with user-facing changes)
- `docs/plans/` — active feature implementation plans (F-10, F-13)
- `docs/research/` — investigation notes
- `docs/tester-feedback/feedback-log.md` — verbatim tester feedback
- `docs/archive/` — completed plan/spec docs (historical reference)

---

## Learning Context

Ken is growing his understanding of MVVM, service injection patterns, and .NET MAUI best practices. When explaining architectural decisions or suggesting patterns, include brief "why" context — not just "what." This codebase doubles as a learning vehicle.

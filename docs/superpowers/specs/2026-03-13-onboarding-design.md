# Onboarding / First-Time Experience — Design Spec

**Date:** 2026-03-13
**Feature ID:** F-2
**Approach:** Bottom-sheet coaching hints over the live app (user-confirmed)

---

## Overview

On first launch the user is greeted with a welcome popup, then guided through three real actions: creating a prayer card, adding a prayer request, and experiencing Prayer Time. Coaching is delivered via a bottom banner that appears on each participating page while onboarding is active. The user performs real actions in the real UI — never blocked, just prompted.

Onboarding completes automatically when all steps are done, or can be skipped at any point. It resets when the user taps "Clear Settings" (which calls `Preferences.Clear()`).

---

## Step Flow

```
[Welcome popup — shown from MainPage.OnAppearing on first visit]
      ↓  "Get Started"
[Prayer Cards tab] — coaching: "Tap 'Add Card' to create your first prayer card"
      ↓  user taps Add Card  →  Advance() called in PrayerCardsViewModel.NewPrayerCardAsync()
[PrayerCardPage] — coaching: "Give it a name — a person, a topic, your church. Tap Save."
      ↓  user saves new card (Id==0 guard)  →  Advance() in PrayerCardViewModel.SaveCommand
[Prayer Cards tab] — coaching: "Great! Expand your card and tap '+ Add prayer'"
      ↓  user taps + Add prayer  →  Advance() in PrayerCardViewModel.AddPrayerAsync()
[PrayerDetailPage] — coaching: "Enter your prayer request. Tap Save."
      ↓  user saves new request (Id==0 guard)  →  Advance() in PrayerRequestDetailViewModel.SaveCommand
[Home tab] — coaching: "Tap 'Prayer Time' when you're ready to pray"
      ↓  PrayerTimePage.OnAppearing fires  →  Advance() in PrayerTimePage code-behind
[PrayerTimePage] — coaching: "Swipe through your cards. Tap ✓ when you're done."
      ↓  session ends  →  Advance() in PrayerTimeViewModel.EndSessionAsync() before GoToAsync
[Closing popup — shown via Shell.Current.CurrentPage.ShowPopupAsync when step == Complete]
      "You're all set! May God bless your prayer life. 🙏"  →  Done
```

Skip is available at any step via "Skip tour" link in the banner. Calls `Skip()` which jumps to `Complete`.

---

## Architecture

### `OnboardingStep` (enum, `Models/OnboardingStep.cs`)

```csharp
public enum OnboardingStep
{
    None, Welcome, CreateCard, NameCard, AddRequest, NameRequest,
    PrayerTime, PrayerTimeActive, Complete
}
```

### `IOnboardingService` / `OnboardingService` (singleton)

```csharp
public interface IOnboardingService
{
    OnboardingStep CurrentStep { get; }
    bool IsActive { get; }                    // true when step is between Welcome and PrayerTimeActive inclusive
    bool WelcomeShownThisSession { get; }     // in-memory; prevents welcome popup showing on every OnAppearing
    void Advance();                           // move to next step in sequence
    void Skip();                              // jump directly to Complete
    void Reset();                             // clear persisted step + in-memory state, restart at Welcome
    event EventHandler StepChanged;
}
```

**Initialization logic:** On construction, read `Preferences.Get("OnboardingStep", "None")`. If the persisted value is `"None"` and `Settings.OnboardingComplete == false`, set `CurrentStep = OnboardingStep.Welcome`. Otherwise restore from the persisted string. (`Settings.FirstRun` is NOT referenced here — it is already set to `false` before `App` is constructed in `MauiProgram.cs`.)

**Persistence:** `CurrentStep` is written to `Preferences` on every `Advance()` / `Skip()` / `Reset()` call using `nameof(OnboardingStep)` as the key (consistent with the `Settings` class convention).

**`Reset()`:** Clears the persisted step key, sets `Settings.OnboardingComplete = false`, sets `WelcomeShownThisSession = false`, and sets in-memory `CurrentStep = Welcome`. The Welcome popup is **not** shown by the caller of `Reset()` — it appears naturally the next time `MainPage.OnAppearing` fires and finds `CurrentStep == Welcome && !WelcomeShownThisSession`.

### `Settings` additions (`Services/Settings.cs`)

```csharp
public static bool OnboardingComplete
{
    get => Preferences.Get(nameof(OnboardingComplete), false);
    set => Preferences.Set(nameof(OnboardingComplete), value);
}
```

**`ClearSettings()` and re-seeding:** `Preferences.Clear()` resets `FirstRun` to `true`, which causes `MauiProgram.cs` to call `SeedDataAsync` on the next launch. Required co-changes to make this safe:

1. **Remove `DropSyncDataAsync`** from inside `SeedDataAsync` entirely — it must never drop user data on re-entry.
2. **Change `SeedDataAsync` to row-count-gate every insert**: before inserting any seed entity, check `await connection.Table<T>().CountAsync() == 0`. Only insert if the table is empty. This makes re-runs safe.
3. **Keep the `FirstRun` check in `MauiProgram.cs` as-is** — it still gates the seed call on first install. After `ClearSettings()` resets `FirstRun`, the re-run of `SeedDataAsync` is now harmless because it will find existing rows and skip all inserts.

---

## UI Components

### Welcome popup (`Views/Onboarding/OnboardingWelcomePopup`)

- CommunityToolkit `Popup`.
- **Shown from `MainPage.OnAppearing`** (guaranteed active page).
- **One-shot guard:** `IOnboardingService` exposes `bool WelcomeShownThisSession { get; }` (in-memory, not persisted). `MainPage.OnAppearing` only shows the popup if `CurrentStep == Welcome && !WelcomeShownThisSession`. The service sets `WelcomeShownThisSession = true` the moment the popup is requested. `Reset()` sets it back to `false`, so re-running onboarding via Reset shows the welcome popup again on the next `OnAppearing`.
- Content: app icon, "Welcome to Prayer App", "Let's set up your first prayer card.", "Get Started" (primary) + "Skip" (muted text).
- "Get Started": calls `_onboardingService.Advance()` → `CurrentStep = CreateCard`, dismisses popup, navigates to `Shell.Current.GoToAsync("//CardsPage")`.
- "Skip": calls `_onboardingService.Skip()`.

### Closing popup (`Views/Onboarding/OnboardingCompletePopup`)

- CommunityToolkit `Popup`.
- **Shown by subscribing to `OnboardingService.StepChanged`** in `AppShell`'s constructor. When step becomes `Complete`, call `Shell.Current.CurrentPage.ShowPopupAsync(new OnboardingCompletePopup())`.
- **Thread safety:** `AppShell`'s `StepChanged` handler wraps the popup call in `MainThread.BeginInvokeOnMainThread(...)`. This is required because `Advance()` may be called from ViewModels executing on background threads (e.g., inside `async Task SaveAsync()`), and `ShowPopupAsync` must run on the UI thread.
- Content: "You're all set! May God bless your prayer life. 🙏", "Done" button.
- "Done": sets `Settings.OnboardingComplete = true`, dismisses.

### Coaching banner (`Views/Onboarding/OnboardingBanner`)

A `ContentView` placed at the **bottom** of each participating page's layout stack.

**Reactivity:** The banner's code-behind subscribes to `IOnboardingService.StepChanged` in `OnHandlerChanged` (MAUI lifecycle equivalent of OnAttached). On each `StepChanged` event it calls `UpdateVisibility()` which checks `IsActive && CurrentStep == ExpectedStep` and sets `IsVisible` directly. Unsubscribes in `OnHandlerChanging` (detach/cleanup).

**`ExpectedStep` bindable property** (int/enum): set from XAML on each page, e.g.:
```xml
<onboarding:OnboardingBanner ExpectedStep="CreateCard" HeadlineText="Tap 'Add Card' to get started" SubText="..." />
```

**Content:**
- Step label: "Step X of 3" — mapping: CreateCard/NameCard → 1, AddRequest/NameRequest → 2, PrayerTime/PrayerTimeActive → 3
- Headline (`HeadlineText` bindable)
- Sub-text (`SubText` bindable, optional)
- "Skip tour" link → calls `_onboardingService.Skip()`
- Styled: `Tertiary` (#3F4A34) background, 12px top rounded corners, gold step label, white text

---

## Advance Trigger Reference

| Step | Advance called from | Guard condition |
|------|-------------------|-----------------|
| `CreateCard` → `NameCard` | `PrayerCardsViewModel.NewPrayerCardAsync()` | None (command is new-card only) |
| `NameCard` → `AddRequest` | `PrayerCardViewModel.SaveAsync()` | `_prayerCard.Id == 0` before save |
| `AddRequest` → `NameRequest` | `PrayerCardViewModel.AddPrayerAsync()` | None (command is new-request only) |
| `NameRequest` → `PrayerTime` | `PrayerRequestDetailViewModel.SaveAsync()` | `_prayer.Id == 0` before save |
| `PrayerTime` → `PrayerTimeActive` | `PrayerTimePage.OnAppearing` (code-behind) | Covers both direct and scope-page paths |
| `PrayerTimeActive` → `Complete` | `PrayerTimeViewModel.EndSessionAsync()` before `GoToAsync` | None |

All `Advance()` calls are no-ops when `!IsActive` — the service ignores them.

---

## Participating Pages

| Page | `ExpectedStep` on banner | Notes |
|------|--------------------------|-------|
| `PrayerCardsPage` | `CreateCard` then `AddRequest` | Two different banners, each `IsVisible` for its own step |
| `PrayerCardPage` | `NameCard` | |
| `PrayerDetailPage` | `NameRequest` | |
| `MainPage` | `PrayerTime` | |
| `PrayerTimePage` | `PrayerTimeActive` | `OnAppearing` also calls `Advance()` from `PrayerTime` |

`PrayerCardsPage` needs **two** `OnboardingBanner` instances (one for `CreateCard`, one for `AddRequest`) — only one is visible at a time.

`PrayerTimeScopePage` does **not** need a banner. The `PrayerTime` step advances when `PrayerTimePage.OnAppearing` fires regardless of whether the user took the direct or scope-selection path.

---

## Persistence

| Setting key | Type | Default | Notes |
|-------------|------|---------|-------|
| `OnboardingStep` | string | `"None"` | Written via `nameof(OnboardingStep)` |
| `OnboardingComplete` | bool | `false` | Written via `nameof(OnboardingComplete)` |

Both cleared by `Preferences.Clear()` in `ClearSettings()`. Onboarding restarts on next cold launch after clear (in-memory state remains until then, unless `Reset()` is called explicitly).

---

## Required Co-Change: `SeedDataAsync` Idempotency

`DBService.SeedDataAsync` must check for existing rows before inserting seed data. This prevents duplicate cards/prayers if the user clears settings and relaunches. Implementation: wrap inserts with a row-count check per entity type, or use `INSERT OR IGNORE` semantics via SQLite-net.

---

## Files to Create / Modify

| File | Change |
|------|--------|
| `Models/OnboardingStep.cs` | New enum |
| `Services/IOnboardingService.cs` | New interface |
| `Services/OnboardingService.cs` | New singleton implementation |
| `Services/Settings.cs` | Add `OnboardingComplete` property |
| `Services/DBService.cs` | Make `SeedDataAsync` idempotent |
| `MauiProgram.cs` | Register `IOnboardingService` as singleton |
| `AppShell.xaml.cs` | Subscribe to `StepChanged`; show closing popup on `Complete`; call `Reset()` if needed |
| `Views/Onboarding/OnboardingWelcomePopup.xaml` + `.cs` | New popup |
| `Views/Onboarding/OnboardingCompletePopup.xaml` + `.cs` | New popup |
| `Views/Onboarding/OnboardingBanner.xaml` + `.cs` | New ContentView with `ExpectedStep`, `HeadlineText`, `SubText` bindable properties |
| `Views/MainPage.xaml` + `.cs` | Add `OnboardingBanner` (PrayerTime step); show welcome popup in `OnAppearing` |
| `ViewModels/HomeViewModel.cs` | Inject `IOnboardingService` if needed |
| `Views/PrayerCard/PrayerCardsPage.xaml` | Add two `OnboardingBanner` instances |
| `ViewModels/PrayerCardsViewModel.cs` | Inject `IOnboardingService`; call `Advance()` in `NewPrayerCardAsync` |
| `Views/PrayerCard/PrayerCardPage.xaml` | Add `OnboardingBanner` |
| `ViewModels/PrayerCardViewModel.cs` | Inject `IOnboardingService`; call `Advance()` in `SaveAsync` (Id==0 guard) and `AddPrayerAsync` |
| `Views/Prayer/PrayerDetailPage.xaml` | Add `OnboardingBanner` |
| `ViewModels/PrayerRequestDetailViewModel.cs` | Inject `IOnboardingService`; call `Advance()` in `SaveAsync` (Id==0 guard) |
| `Views/PrayerTime/PrayerTimePage.xaml` + `.cs` | Add `OnboardingBanner`; call `Advance()` in `OnAppearing` |
| `ViewModels/PrayerTimeViewModel.cs` | Inject `IOnboardingService`; call `Advance()` in `EndSessionAsync` |

---

## Out of Scope

- Feedback / rating prompt (no mechanism exists yet)
- Re-playable interactive tutorial post-onboarding
- Deep-link / share-based onboarding

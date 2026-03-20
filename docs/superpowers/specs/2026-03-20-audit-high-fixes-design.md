# Audit High-Priority Fixes — Design Spec

**Date:** 2026-03-20
**Scope:** H-2, H-6, H-1, H-3, H-4 from the pre-app-store audit
**Status:** Approved for implementation

---

## Context

Session 14 identified 27 findings across 4 severity levels. The 3 critical items (C-1/C-2/C-3) were fixed immediately. This spec covers the 5 highest-priority remaining fixes, ordered by Ken's priority: H-2, H-6, H-1, H-3, H-4.

---

## H-2: iOS AppDelegate Missing `e.SetObserved()`

**Problem:** The `UnobservedTaskException` handler in `Platforms/iOS/AppDelegate.cs` (line 22-27) logs the exception but doesn't call `e.SetObserved()`. While .NET 5+ suppresses unobserved task exceptions by default (no crash), failing to call `SetObserved()` means the exception still generates runtime noise and leaves handling behavior implementation-defined. The real risk is silent data loss or inconsistent state, not a crash.

**Fix:**
- Add `e.SetObserved()` after the existing logging in the iOS handler.
- Add an equivalent `UnobservedTaskException` handler on Android (`Platforms/Android/MainApplication.cs`) — there isn't one currently. Same pattern: log + `SetObserved()`.
- Both handlers will be updated to use `IDiagnosticLog` (see H-1) instead of `Preferences.Set("LastCrash", ...)`.

**DI timing constraint:** Platform exception handlers are registered during startup, before the DI container is fully built. `IDiagnosticLog` must be resolved lazily inside the handler body (not captured at registration time), with a fallback to `Console.Error.WriteLine` if the service is not yet available:
```csharp
var log = IPlatformApplication.Current?.Services?.GetService<IDiagnosticLog>();
if (log != null) log.Log("UnobservedTaskException", e.Exception);
else Console.Error.WriteLine(msg);
```

**Files:**
- Modify: `PrayerApp/Platforms/iOS/AppDelegate.cs`
- Modify: `PrayerApp/Platforms/Android/MainApplication.cs`

---

## H-6: PrayerTag.Color MaxLength(7) Too Short

**Problem:** `PrayerTag.Color` has `[MaxLength(7)]` which fits `#RRGGBB` (7 chars) but not `#AARRGGBB` (9 chars) from `Color.ToArgbHex()`. sqlite-net silently truncates strings exceeding `MaxLength` on write.

**Fix:** Change `[MaxLength(7)]` to `[MaxLength(9)]` in `Models/PrayerTag.cs`.

No database migration is needed — SQLite doesn't enforce `MaxLength` at the engine level. It's a sqlite-net ORM annotation that controls truncation behavior in the C# layer.

**Files:**
- Modify: `PrayerApp/Models/PrayerTag.cs`

---

## H-1: Fire-and-Forget Error Handling + Diagnostic Log

**Problem:** 19 `_ = SomeAsync()` calls across ViewModels and Settings silently swallow exceptions. The only crash logging (`Preferences.Set("LastCrash", ...)`) is iOS-only, stores only the last error, and has no UI for users to access or share it.

### New: IDiagnosticLog / DiagnosticLog Service

Singleton service providing append-only file-based logging with user-initiated sharing.

```csharp
public interface IDiagnosticLog
{
    void Log(string category, Exception ex);
    string GetLogPath();
    void Trim();
}
```

**Implementation details:**
- Log file: `FileSystem.AppDataDirectory/diagnostics.log`
- Format: `[2026-03-20 14:30:05] [category] ExceptionType: Message\nStackTrace\n---`
- `Trim()`: Called at app startup via `App.InitTask` (alongside `SeedAsync`) — must not block `CreateMauiApp()`. Keeps the last 100 entries. Reads the file, counts `---` delimiters, drops oldest entries beyond 100.
- Thread safety: `lock` around file writes (multiple fire-and-forget tasks could fail concurrently).
- Registered as singleton in `MauiProgram.cs`.

**Testability:** `IDiagnosticLog` interface allows test substitution. `DiagnosticLog` constructor accepts a `string logDirectory` parameter (defaults to `FileSystem.AppDataDirectory` in production, injectable in tests).

### New: TaskExtensions.SafeFireAndForget

Static extension method on `Task` in `Helpers/TaskExtensions.cs`.

```csharp
public static async void SafeFireAndForget(this Task task)
{
    try
    {
        await task;
    }
    catch (Exception ex)
    {
        var log = IPlatformApplication.Current?.Services?.GetService<IDiagnosticLog>();
        log?.Log("SafeFireAndForget", ex);
    }
}
```

**Why `async void`:** This is the canonical correct use of `async void` — fire-and-forget error boundaries where no caller awaits the result. The method name makes intent explicit.

### Replacement Pattern

All 19 call sites change from `_ = SomeAsync()` to `SomeAsync().SafeFireAndForget()`:

| File | Line | Call |
|------|------|------|
| `PrayerCardsViewModel.cs` | 78 | `AddNewCardAsync(...)` |
| `PrayerCardsViewModel.cs` | 89 | `matched.AddOrUpdatePrayerAsync(...)` |
| `PrayerCardsViewModel.cs` | 203 | `LoadAsync()` |
| `PrayerCardViewModel.cs` | 203 | `LoadPrayerCardAsync(...)` |
| `PrayerCardViewModel.cs` | 232 | `LoadPrayerCardAsync(...)` |
| `PrayerRequestDetailViewModel.cs` | 334 | `LoadTagsAsync()` |
| `PrayerRequestDetailViewModel.cs` | 357 | `LoadPrayerAsync(...)` |
| `PrayerRequestDetailViewModel.cs` | 394 | `LoadPrayerAsync(...)` |
| `PrayerListViewModel.cs` | 153 | `HandleSavedAsync(...)` |
| `PrayerListViewModel.cs` | 317 | `LoadPrayersAsync()` |
| `PrayerTimeScopeViewModel.cs` | 36 | `LoadTagsAsync()` |
| `PrayerTimeViewModel.cs` | 167 | `LoadEntriesAsync(...)` |
| `PrayerTimeViewModel.cs` | 369 | `NextAsync()` |
| `QuickAddViewModel.cs` | 40 | `LoadCardsAsync()` |
| `TagDetailViewModel.cs` | 59 | `LoadSwatchesAsync()` |
| `TagDetailViewModel.cs` | 67 | `LoadAsync(...)` |
| `Settings.cs` | 47 | `UpdateAllowNotificationsAsync(...)` |
| `Settings.cs` | 78 | `_notificationService.RequestPermissionAsync()` |
| `PrayerTagSelectionViewModel.cs` | 99 | `ToggleTagAsync(...)` |

### Platform Exception Handler Updates

Both iOS `AppDelegate.cs` handlers (`UnhandledException`, `UnobservedTaskException`) and the new Android handler switch from `Preferences.Set("LastCrash", ...)` to `IDiagnosticLog.Log(...)`.

### Settings UI: "Send Diagnostic Info"

New button in `Settings.xaml` (code-behind pattern, consistent with existing backup buttons):
- On tap: `Share.RequestAsync(new ShareFileRequest { File = new ShareFile(logPath) })`
- Uses OS share sheet — user picks email, text, etc.
- Only visible when log file exists and is non-empty.

**Files:**
- Create: `PrayerApp/Services/IDiagnosticLog.cs`
- Create: `PrayerApp/Services/DiagnosticLog.cs`
- Create: `PrayerApp/Helpers/TaskExtensions.cs`
- Modify: `PrayerApp/MauiProgram.cs` (register service, call `Trim()` at startup)
- Modify: `PrayerApp/Platforms/iOS/AppDelegate.cs` (use `IDiagnosticLog`, add `SetObserved()`)
- Modify: `PrayerApp/Platforms/Android/MainApplication.cs` (add handlers using `IDiagnosticLog`)
- Modify: all 13 ViewModel/Service files listed above (replace `_ =` with `.SafeFireAndForget()`)
- Modify: `PrayerApp/Views/Settings.xaml` + `Settings.xaml.cs` (add share button)

**Test files:**
- Create: `PrayerApp.Tests/Services/DiagnosticLogTests.cs` (use temp directory, clean up via `IDisposable`)
- Create: `PrayerApp.Tests/Helpers/TaskExtensionsTests.cs`
- Add `<Compile Include>` entries in `PrayerApp.Tests.csproj`:
  - `../PrayerApp/Helpers/TaskExtensions.cs`
  - `../PrayerApp/Services/IDiagnosticLog.cs`
  - `../PrayerApp/Services/DiagnosticLog.cs`

**TaskExtensions testability:** Add an overload `SafeFireAndForget(this Task task, IDiagnosticLog? log)` for direct injection in tests. The parameterless version resolves from the service locator for production use.

---

## H-3: PrayerCardViewModel.Reload() Race Condition

**Problem:** `PrayerCardViewModel.Reload()` (line 230-234) and `ApplyQueryAttributes` (line 196-208) both call `RefreshProperties()` synchronously after firing `LoadPrayerCardAsync` as fire-and-forget. This reads stale `_prayerCard` data. `LoadPrayerCardAsync` already calls `RefreshProperties()` in its `finally` block (line 226).

**Origin:** Both redundant calls originated in the initial ViewModel scaffold (`993b187 — AI refactor`). They were not added to fix a stale-data problem — they shipped with the first version.

**Fix:** Remove the redundant synchronous `RefreshProperties()` calls from:
1. `Reload()` line 233
2. `ApplyQueryAttributes` line 206

The async path's `finally` block (line 226) is the single correct notification point.

**Note:** After H-1 fix, both `_ = LoadPrayerCardAsync(...)` calls in these methods will become `LoadPrayerCardAsync(...).SafeFireAndForget()`.

**Files:**
- Modify: `PrayerApp/ViewModels/PrayerCardViewModel.cs`

---

## H-4: Null Checks on LoadAsync Return Values

**Problem:** 6 call sites use `LoadAsync` return values without null checks. If the record was deleted between navigation (e.g., user deletes on one tab, taps a stale link on another), the app crashes with `NullReferenceException`.

**Fix pattern:** Guard each call site. For page-load methods, navigate back. For in-list operations, return silently.

| File | Line | Current Code | Fix |
|------|------|-------------|-----|
| `PrayerCardViewModel.cs` | 283 | `prayer.PrayerCardId` | `if (prayer is null) return;` |
| `PrayerCardsViewModel.cs` | 112 | `new PrayerCardViewModel(card)` | `if (card is null) return;` |
| `PrayerListViewModel.cs` | 264 | `BuildViewModel(p)` | `if (p is null) return;` |
| `PrayerRequestDetailViewModel.cs` | 379 | `_prayer = await Prayer.LoadAsync(id)` | `if (result is null) { await Shell.Current.GoToAsync(".."); return; }` — guard must precede the `try/finally` block (see note below) |
| `TagDetailViewModel.cs` | 85 | `_tag = await PrayerTag.LoadAsync(id)` | `if (result is null) { await Shell.Current.GoToAsync(".."); return; }` |
| `TagService.cs` | 138 | `tag.DeleteAsync()` | `if (tag is null) return;` |

For page-load methods (`PrayerRequestDetailViewModel.LoadPrayerAsync`, `TagDetailViewModel.LoadAsync`), navigating back is the correct UX — the record no longer exists. The `IDiagnosticLog` should also log these as warnings (not errors) for diagnostic visibility.

**`PrayerRequestDetailViewModel.LoadPrayerAsync` structure note:** The existing `finally` block (line 387-389) calls `RefreshProperties()` and `LoadTagsAsync()` unconditionally, which would touch `_prayer` fields after a null return. The null guard must be placed before the `try/finally` so it short-circuits cleanly — restructure from `try { load + assign } finally { refresh }` to: load → null check with early return → assign → refresh.

**Files:**
- Modify: `PrayerApp/ViewModels/PrayerCardViewModel.cs`
- Modify: `PrayerApp/ViewModels/PrayerCardsViewModel.cs`
- Modify: `PrayerApp/ViewModels/PrayerListViewModel.cs`
- Modify: `PrayerApp/ViewModels/PrayerRequestDetailViewModel.cs`
- Modify: `PrayerApp/ViewModels/TagDetailViewModel.cs`
- Modify: `PrayerApp/Services/TagService.cs`

---

## Implementation Order

1. **H-1 first** — creates `IDiagnosticLog` and `SafeFireAndForget` that H-2, H-3, H-4 depend on
2. **H-2** — uses `IDiagnosticLog` in platform handlers
3. **H-6** — independent one-liner
4. **H-3** — uses `SafeFireAndForget` in the cleaned-up `Reload()`
5. **H-4** — uses `IDiagnosticLog` for warning logs on null returns

---

## Verification

1. **Unit tests:** DiagnosticLog service tests (log, trim, file creation). All 77 existing tests still pass.
2. **H-2:** Force an unobserved task exception in debug → verify it's logged, not crashed.
3. **H-6:** Create a tag with a color that includes alpha → verify full `#AARRGGBB` string persists.
4. **H-1:** Temporarily throw in a fire-and-forget path → verify it appears in `diagnostics.log`. Tap "Send Diagnostic Info" in Settings → share sheet opens with the log file.
5. **H-3:** Edit a card → save → return to card list → verify card shows updated data (no stale flash).
6. **H-4:** Manually delete a prayer row from the DB while app is running → navigate to it → verify app navigates back gracefully instead of crashing.

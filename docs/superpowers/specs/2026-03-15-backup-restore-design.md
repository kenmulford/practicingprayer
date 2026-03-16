# Backup & Restore — Design Spec

**Feature:** F-11 — iCloud / Google Drive Backup
**Date:** 2026-03-15
**Status:** Approved for implementation

---

## Context

Users need to transfer their prayer data when switching devices. The app stores all data in a local SQLite database (`prayer_app.db`). There is no cloud sync. This feature adds a manual, user-initiated backup/restore flow that writes to and reads from a proprietary `.pcrd` file (a ZIP archive containing the database file) via the OS-native file picker — no authentication, no cloud SDK integration.

---

## Goals

- Let users export a `.pcrd` backup to any location their OS supports (iCloud Drive, Google Drive, local storage, etc.)
- Let users restore from a `.pcrd` file on a new or existing device
- Survive any point of failure including force-close mid-restore
- Keep the DB connection valid under all circumstances

## Non-Goals

- Automatic / scheduled backup
- Cloud provider selection UI (deferred to OS)
- Merge-based restore (replace-only)
- Encryption of backup contents
- iOS open-in-place file association (UTType / `CFBundleDocumentTypes` plist wiring) — the OS file picker is sufficient without it

---

## File Format

**Extension:** `.pcrd` (Prayer Cards Data)
**Contents:** A ZIP archive containing a single entry: `prayer_app.db`
**Filename on export:** `prayer_cards_YYYY-MM-DD.pcrd`

Validation on import: confirm the file is a valid ZIP and contains `prayer_app.db` before proceeding.

---

## Architecture

### New: `IBackupService` / `BackupService`

Singleton service registered in `MauiProgram.cs`. Owns all backup/restore logic. Depends on `IDBService`.

```
IBackupService
  Task<bool> ExportAsync()
  Task<bool> ImportAsync()
```

### Modified: `IDBService` / `DBService`

**`_db` field must change from `readonly` to mutable:**

```csharp
// Before
private readonly SQLiteAsyncConnection _db;

// After
private SQLiteAsyncConnection _db;
```

This is required so `ReinitializeAsync` can replace the connection object. All dependent services (`CardService`, `PrayerService`, etc.) depend on `IDBService`, not the raw connection — so they automatically use the new connection on their next call after reinitialize.

Two new methods:

```
Task CloseAsync()      — issues PRAGMA wal_checkpoint(TRUNCATE), calls _db.CloseAsync(), sets _db = null
Task ReinitializeAsync(string path)  — creates new SQLiteAsyncConnection, calls UpdateSchema()
```

**Null-guard requirement:** All `DBService` methods that access `_db` must check for null and throw `InvalidOperationException("Database is not available.")` if `_db` is null. This protects against any stray call arriving during the millisecond close/reinitialize window (e.g. from a background notification handler). The guard is a simple `if (_db == null) throw` at the top of each public method — no lock or semaphore is required given the narrow window and absence of background DB workers.

The `DBService` constructor also gains a **synchronous startup recovery check** (see Atomic Restore section). This is consistent with the existing constructor pattern — the constructor already calls `.Wait()` on async table creation. The recovery check uses synchronous `File` operations (rename, delete) and must complete before the connection is opened.

The existing startup sequence in `MauiProgram.cs` does not change:
1. Factory lambda constructs `DBService(dbPath)` on first resolution → recovery check runs → connection opens → tables created
2. `myDBService.UpdateSchema()` called explicitly afterward

### Settings page — code-behind pattern (no ViewModel)

`SettingsPage.xaml.cs` currently handles all logic directly in code-behind with no ViewModel. The backup commands follow the same pattern — `BackupCommand` and `RestoreCommand` are wired as button click handlers in `Settings.xaml.cs`, not as ViewModel commands. No ViewModel is created; no DI registration for the page changes.

---

## Export Flow

1. User taps "Back Up Now" → button disables, spinner shows
2. Call `IDBService.CloseAsync()` — this issues `PRAGMA wal_checkpoint(TRUNCATE)` before closing, ensuring all WAL pages are flushed to the main DB file and companion `-wal` / `-shm` files are cleared
3. Read `prayer_app.db` from `FileSystem.AppDataDirectory`
4. Call `IDBService.ReinitializeAsync(dbPath)` to reopen the connection immediately after reading
5. Create a ZIP archive in `FileSystem.CacheDirectory` named `prayer_cards_YYYY-MM-DD.pcrd` containing only `prayer_app.db` (the `-wal` and `-shm` companion files are not included — they are empty/absent after a successful checkpoint)
6. Call `IFileSaver.SaveAsync("prayer_cards_YYYY-MM-DD.pcrd", stream, CancellationToken)` → OS save dialog appears
7. User picks destination (cloud folder, local, etc.)
8. Delete temp ZIP from cache directory
9. Button re-enables
10. On success: toast "Backup saved"
11. On failure / cancellation: toast "Backup cancelled" (no data was changed)

> **Why close before copy:** `sqlite-net-pcl` may operate in WAL mode. Reading the raw `.db` file while the connection is open risks missing recently committed transactions residing in the `-wal` companion file. `PRAGMA wal_checkpoint(TRUNCATE)` followed by `CloseAsync()` ensures the main DB file is complete and the WAL is empty before the file is read. The connection is reopened immediately after, so the window of unavailability is milliseconds.

`IFileSaver` is from `CommunityToolkit.Maui.Storage`. Registration in `MauiProgram.cs`: `builder.Services.AddSingleton<IFileSaver>(FileSaver.Default)`.

---

## Restore Flow

### UI Lock

Before any file operations begin, push a full-screen non-dismissable modal using `Navigation.PushModalAsync(new RestoreProgressPage())`. `RestoreProgressPage` is a simple display-only page (spinner + text) — it takes no injected dependencies and is instantiated with `new`. Back gesture and hardware back button are disabled for the duration. The modal is dismissed only after `ReinitializeAsync()` completes or an unrecoverable error is shown. The page shows:

> **"Restoring your data…"**
> **"Do not close the app."**

### Steps

> **Ordering note:** The OS file picker must be invoked *before* pushing the modal. On iOS, presenting a UIDocumentPicker while a modal is already on screen causes a presentation conflict. Pick and validate first; push the blocking modal only once a valid file is in hand.

1. User taps "Restore from Backup"
2. Confirmation dialog (via `DisplayAlertAsync`): **"This will permanently replace all your current prayer data. This cannot be undone. Continue?"** — Cancel / Restore
3. User confirms → `FilePicker.PickAsync()` → OS file picker
4. User selects file
5. Validate: is it a valid ZIP? Does it contain `prayer_app.db`? If not → alert "Invalid backup file", abort (no modal was shown)
6. Push `RestoreProgressPage` modal: `await Navigation.PushModalAsync(new RestoreProgressPage())`
7. Enter atomic swap (see below)
8. Call `IDBService.ReinitializeAsync(dbPath)`
9. Navigate to app root: `Shell.Current.GoToAsync("//MainPage")`
10. Pop modal via `Navigation.PopModalAsync()`
11. Toast: "Restore complete"

---

## Atomic Restore

All restore file operations use three named paths (all in `FileSystem.AppDataDirectory`):

| Path | Role |
|---|---|
| `prayer_app.db` | Live database |
| `prayer_app_restore.db` | Incoming restore (written before swap) |
| `prayer_app_backup.tmp` | Snapshot of original (held during swap) |

### Swap Sequence

```
Phase 1 — Write (original DB untouched)
  Extract prayer_app.db from .pcrd → prayer_app_restore.db

Phase 2 — Swap
  CloseAsync()                                    ← DB connection closed
  Rename prayer_app.db → prayer_app_backup.tmp    ← original safe
  Rename prayer_app_restore.db → prayer_app.db    ← new DB goes live

Phase 3 — Cleanup
  Delete prayer_app_backup.tmp
```

**Force-close safety:** at every point, the DB can be recovered on next startup.

### Startup Recovery Check

Runs **synchronously** in the `DBService` constructor before any connection is opened. Uses `File.Exists`, `File.Move`, `File.Delete`.

| Files present at startup | Action |
|---|---|
| `prayer_app_restore.db` exists, `prayer_app.db` exists | Delete `prayer_app_restore.db` (write interrupted, original intact) |
| `prayer_app_restore.db` exists, `prayer_app.db` missing | Rename restore → live (swap interrupted mid-flight) |
| `prayer_app_backup.tmp` exists, `prayer_app.db` exists | Delete `prayer_app_backup.tmp` (swap completed, stale backup) |
| `prayer_app_backup.tmp` exists, `prayer_app.db` missing | Rename backup → live (catastrophic swap failure, roll back to original) |
| Both `prayer_app_restore.db` and `prayer_app_backup.tmp` exist | Impossible by construction — the second rename (restore→live) completes before cleanup begins, so both can never be present with `prayer_app.db` absent. Treat as: delete restore, delete backup.tmp (both are stale). |
| None of the above | Normal startup |

After recovery check completes, open connection and call `UpdateSchema()` as normal.

---

## Error Handling

| Failure point | Behaviour |
|---|---|
| File picker cancelled | Silent abort, no state change |
| Invalid / corrupt `.pcrd` | Alert: "This file doesn't appear to be a valid Prayer Cards backup." Abort. (No modal was shown — validation occurs before modal is pushed.) |
| Disk full during write | Alert: "Not enough storage to complete the backup." Cleanup temp file. |
| Swap phase failure | Next startup recovery check restores a valid DB |
| `ReinitializeAsync()` fails | Alert: "Restore failed. Please restart the app." (DB is in a valid state due to atomic swap) |

---

## Files Changed

| File | Change |
|---|---|
| `PrayerApp/Services/IBackupService.cs` | **New** — interface |
| `PrayerApp/Services/BackupService.cs` | **New** — implementation |
| `PrayerApp/Services/IDBService.cs` | Add `CloseAsync()`, `ReinitializeAsync(string path)` |
| `PrayerApp/Services/DBService.cs` | Change `_db` to mutable; implement new methods; add startup recovery check |
| `PrayerApp/MauiProgram.cs` | Register `IBackupService`, `IFileSaver` |
| `PrayerApp/Views/Settings/SettingsPage.xaml` | Add "Online Backup" section (header, description, two buttons) |
| `PrayerApp/Views/Settings/SettingsPage.xaml.cs` | Add backup/restore button handlers (code-behind pattern, no ViewModel) |
| `PrayerApp/Views/Settings/RestoreProgressPage.xaml` | **New** — full-screen blocking modal (spinner + message) |
| `PrayerApp/Views/Settings/RestoreProgressPage.xaml.cs` | **New** — code-behind; disables back navigation |

---

## Verification

1. **Export happy path**: Tap "Back Up Now" → OS picker appears → save to iCloud Drive or local → `.pcrd` file appears at destination → unzipping the file reveals a valid `prayer_app.db`
2. **Import happy path**: On a fresh install (or after clearing data), tap "Restore from Backup" → confirm → pick `.pcrd` → data appears correctly on all pages after navigate-to-root
3. **Invalid file**: Pick a non-`.pcrd` file or a corrupt archive → error alert, app state unchanged
4. **Force-close during write (Phase 1)**: Kill app after `prayer_app_restore.db` is partially written → relaunch → original DB intact, app works normally
5. **Force-close during swap (Phase 2)**: Kill app after `prayer_app_backup.tmp` rename but before second rename → relaunch → recovery check completes swap → new DB is live
6. **Force-close during cleanup (Phase 3)**: Kill app before `prayer_app_backup.tmp` is deleted → relaunch → stale file deleted, app works normally
7. **Export WAL safety**: Add data, immediately tap "Back Up Now" → restore the resulting `.pcrd` on a clean install → all data present (confirms WAL was flushed before copy)
8. **DB connection continuity**: Export, then immediately restore, then export again → no connection errors, all operations complete cleanly

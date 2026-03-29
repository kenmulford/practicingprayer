# iOS Crash: Unhandled Exception During Tag Save/Delete Navigation

**Date:** 2026-03-27
**Source:** iOS UITest suite on iPad (A16) simulator, iOS 26.4
**Build:** v1.0.6 (32), Release config, iossimulator-arm64

---

## Summary

The app crashes with an unhandled .NET exception (SIGABRT via `xamarin_unhandled_exception_handler`) during tag create/delete flows. The crash occurs on the **main thread** and is reproducible — it happened twice in the same test run, both times during `RecreateDriver` recovery (meaning the app crashed, tests recreated the session, the app crashed *again* on the fresh launch).

## Steps to Reproduce

### Crash 1: During `Tags_CreateTag_AppearsInList`
1. Navigate to Tags tab
2. Tap "Add" toolbar item
3. Enter tag name "UITest Tag"
4. Tap "Save" toolbar item
5. Save triggers `GoToAsync("..")` to navigate back to tag list
6. **App crashes during or immediately after the navigation back**

### Crash 2: During `Tags_DeleteTag` (fresh app launch after crash 1)
1. App relaunched by Appium (`RecreateDriver`)
2. App crashes **12 seconds after launch** (07:06:14 → 07:06:26)
3. This suggests a startup-time crash, possibly triggered by corrupted state from crash 1

## Crash Details

- **Exception Type:** `EXC_CRASH (SIGABRT)`
- **Termination:** `Abort trap: 6` — self-terminated by PrayerApp process
- **Handler:** `xamarin_unhandled_exception_handler` → `mono_invoke_unhandled_exception_hook` → `mono_jit_exec`
- **Thread:** Main thread (`com.apple.main-thread`)
- **No managed stack trace available** in the crash report (Release build strips symbols)

## Key Observations

1. **Navigation-related:** Both crashes occur around Shell navigation (`GoToAsync("..")` after save, or app startup which triggers Shell initialization)
2. **Tag-specific:** Other save flows (prayer cards, prayers) don't crash — only tag save/delete
3. **Intermittent:** The same tests passed in 6 prior rounds without crashing. This is a race condition.
4. **Second crash on startup:** Suggests the first crash may leave SQLite or app state in a bad condition that causes the next launch to fail
5. **Recovery works:** Despite both crashes, `RecreateDriver` (3-attempt retry) eventually succeeded and both tests passed

## Relevant Code Paths to Investigate

- `TagDetailViewModel.SaveAsync()` — the save command that triggers `GoToAsync("..")`
- `TagsViewModel` — the list page that receives navigation back
- `AppShell` / Shell route registration for Tags
- `DBService` / SQLite connection lifecycle — could a write be in-flight when navigation triggers disposal?
- App startup path — `MauiProgram.cs`, `App.xaml.cs` — what happens if SQLite state is inconsistent?

## Crash Report Timestamps

| Event | Time | Delta |
|-------|------|-------|
| Crash 1: App launched | 06:53:37 | — |
| Crash 1: App crashed | 07:05:19 | +11m 42s into test run |
| Crash 2: App relaunched | 07:06:14 | +55s after crash 1 |
| Crash 2: App crashed | 07:06:26 | +12s after relaunch |

## Suggested Investigation

1. **Add `try/catch` with logging around `GoToAsync` in `TagDetailViewModel.SaveAsync()`** to capture the managed exception
2. **Run a Debug build** on the simulator and reproduce — the managed stack trace will appear in Xcode console
3. **Check for `async void` event handlers** in tag-related ViewModels that could swallow/rethrow exceptions on the UI thread
4. **Check SQLite thread safety** — is `SaveAsync()` completing before `GoToAsync()` navigates away and the page is disposed?
5. **Check if `LoadAllAsync()` in `TagsViewModel` is called during `OnNavigatedTo` and could throw if the DB is mid-write**

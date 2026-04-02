# INV-4 — Android In-App Update Research

**Branch:** `feature/inv4-android-update`
**Status:** Shelved — NuGet compatibility blocker as of 2026-03-17
**Resume when:** MAUI 10 AndroidX package pins are relaxed, or a compatible Play Core binding is available

---

## What We're Building

Android-only in-app update notification using the Play Core **flexible update** flow:
- Queries Google Play directly — no server, no JSON file, no maintenance
- Shows the Play Store update overlay (non-blocking, user can dismiss)
- When download finishes, shows a `DisplayAlertAsync` prompt offering "Restart Now / Later"
- iOS gets a no-op stub — App Store updates handled by OS

Beta/internal testers see the nudge **immediately** after Google finishes build processing
(not during review). All users see it post-review.

---

## Architecture (Ready to Implement)

All code is in this branch:

| File | Status |
|------|--------|
| `PrayerApp/Services/IUpdateService.cs` | ✅ Written — clean interface |
| `PrayerApp/Platforms/Android/UpdateService.cs` | ✅ Written — uses Play Core listener pattern |
| `PrayerApp/Platforms/iOS/UpdateService.cs` | ✅ Written — no-op stub |
| `PrayerApp/MauiProgram.cs` | ✅ DI registration added (`#if ANDROID / #elif IOS`) |
| `PrayerApp/Platforms/Android/MainActivity.cs` | ✅ `OnResume` hook added |

The implementation is complete code-wise. The blocker is purely the NuGet package.

---

## NuGet Compatibility Blocker

### What was tried

**Option 1: `Xamarin.Google.Android.Play.Core` 1.10.3**
- Restores cleanly, no dependency conflicts
- Only provides TFM assets for `monoandroid12.0` and `net6.0-android31.0`
- Types from `Com.Google.Android.Play.Core.*` are not resolved under `net10.0-android`
- Workaround tried: `AssetTargetFallback` set to `net6.0-android31.0` — restores successfully
- **Still need to verify at build time** whether the fallback actually provides the types

**Option 2: `Xamarin.Google.Android.Play.App.Update` 2.1.0.18** (split package)
- Targets `net10.0-android36.0` ✅ and `net9.0-android35.0`
- **Hard dependency conflict:** requires `Xamarin.AndroidX.Lifecycle.LiveData.Core >= 2.10.0.2`
  via its transitive chain: `GooglePlayServices.Basement 118.10.0.1` → `AndroidX.Fragment 1.8.9.2` → `LiveData.Core >= 2.10.0.2`
- MAUI 10.0.41 pins `Xamarin.AndroidX.Lifecycle.LiveData 2.9.2.1` which requires
  `LiveData.Core >= 2.9.2.1 && < 2.9.3` — **mutually exclusive**
- Adding `LiveData.Core 2.10.0.2` directly breaks the MAUI constraint
- All 18 minor versions of `Xamarin.Google.Android.Play.App.Update` were published as a batch;
  older ones likely have the same transitive chain

### Root cause

MAUI 10.0.41 pins AndroidX Lifecycle to `2.9.x`. The latest Play Core binding requires
`2.10.x`. These are mutually exclusive until either:
- Microsoft ships a MAUI update bumping its AndroidX pins
- A new Play Core binding ships with a lower AndroidX floor
- The project upgrades to a future MAUI version that aligns

### Next step when resuming

1. Check if MAUI version > 10.0.41 has relaxed the `Xamarin.AndroidX.Lifecycle.LiveData` pin
2. If yes: try `Xamarin.Google.Android.Play.App.Update` (latest version) — everything else is ready
3. If no: try the `AssetTargetFallback` approach with `Xamarin.Google.Android.Play.Core 1.10.3`
   and run a full Android debug build to see if types resolve at compile time

---

## Play Core API Reference

```csharp
// Namespaces (Xamarin.Google.Android.Play.Core / .App.Update)
using Com.Google.Android.Play.Core.Appupdate;       // IAppUpdateManager, AppUpdateManagerFactory, AppUpdateInfo
using Com.Google.Android.Play.Core.Install;         // IInstallStateUpdatedListener, InstallState
using Com.Google.Android.Play.Core.Install.Model;   // UpdateAvailability, AppUpdateType, InstallStatus
using Com.Google.Android.Play.Core.Tasks;           // IOnSuccessListener, IOnFailureListener

// Key values
UpdateAvailability.UpdateAvailable                  // == 2
AppUpdateType.Flexible                              // == 0  (non-blocking, background download)
InstallStatus.Downloaded                            // == 11 (ready to apply)

// Flow
var manager = AppUpdateManagerFactory.Create(activity);
manager.AppUpdateInfo.AddOnSuccessListener(listener);   // Java Task, not System.Task
manager.StartUpdateFlowForResult(info, AppUpdateType.Flexible, activity, requestCode);
manager.RegisterListener(installStateListener);         // fires when download status changes
manager.CompleteUpdate();                               // triggers app restart to apply update
```

Listener helper classes must inherit `Java.Lang.Object` and implement the Java interface.
See `UpdateService.cs` in this branch for the full `SuccessListener<T>` and
`InstallStateListener` implementations.

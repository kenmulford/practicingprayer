---
name: maui-android-emulator
description: >-
  Build, deploy, run, UI-test, and screenshot a .NET MAUI app on an Android
  emulator the reliable way. Use this skill WHENEVER the task involves a MAUI
  Android build/deploy/run, installing or launching the app on an emulator or
  device, running Appium/UiAutomator2 UI tests on Android, capturing Android
  screenshots, or debugging adb / emulator / `INSTALL_FAILED` / FastDev /
  `run-as` / signature-mismatch problems — even if the user just says "get the
  app running on the emulator", "rebuild and install", "run the UI tests", or
  "grab some screenshots". It encodes the canonical `dotnet build` targets plus
  the field-proven gotchas (Git Bash adb path mangling, no-op incremental
  builds, Release-breaks-seeding, never-uninstall-FastDev) that otherwise cost
  an hour of wheel-spinning each time.
---

# MAUI on Android Emulator — build, deploy, run, test, screenshot

A reusable runbook for getting a .NET MAUI app onto an Android emulator and
keeping it there reliably. It is **project-agnostic** — substitute the
placeholders below for your project. The concrete values for *this* repo are in
the last section.

| Placeholder | Meaning | This repo |
|---|---|---|
| `<CSPROJ>` | the app project file | `PrayerApp/PrayerApp.csproj` |
| `<TFM>` | Android target framework | `net10.0-android` |
| `<APP_ID>` | application id / package | `com.multithreadedllc.prayercards` |
| `<MAIN_ACTIVITY>` | launch activity | `crc6425c6d21f3599989c.MainActivity` |
| `<AVD_NAME>` | emulator AVD | `pixel_9_-_api_36_0` (override via `ANDROID_AVD`) |
| `<UITESTS>` | UI test project dir | `PrayerApp.UITests/` |

## When to use

Reach for this before: a first emulator run on a machine, "rebuild and
deploy", an install that fails with a signature/`INSTALL_FAILED` error, a
build that "succeeds" but the app doesn't change, running the Appium suite, or
capturing store/PR screenshots. The single most common failure mode is
fighting `adb` or picking the wrong build config — both are covered here.

## The one decision that matters: which command?

MAUI ships MSBuild *targets* that build, sign, deploy, and (optionally) launch
in one step. **Prefer them over hand-rolled `adb install`** — they keep the
on-device signature consistent and handle Fast Deployment for you, which is
exactly what hand-rolled installs get wrong.

```powershell
# Build + deploy + LAUNCH on the running emulator (the everyday "run it" command)
dotnet build <CSPROJ> -t:Run -f <TFM>

# Build + deploy, do NOT launch (e.g. before an Appium run that attaches)
dotnet build <CSPROJ> -t:Install -f <TFM>
```

Reach for a **standalone APK + `adb install`** only when you genuinely need the
APK as an artifact (CI, sharing, multi-device screenshot runs):

```powershell
dotnet build <CSPROJ> -f <TFM> -c Debug -p:EmbedAssembliesIntoApk=true
# APK: <CSPROJ_DIR>/bin/Debug/<TFM>/<APP_ID>-Signed.apk
adb install -r <path-to>/<APP_ID>-Signed.apk
```

### Debug vs Release vs EmbedAssembliesIntoApk

| Setting | Use it when | Trap it avoids / causes |
|---|---|---|
| `-c Debug` (default) | dev iteration; **any harness that seeds via `adb shell run-as`** | `run-as` works only on a *debuggable* app — Release fails DB seeding before tests even start |
| `-c Release` | store builds, perf passes, screenshot polish | breaks `run-as` seeding; signed with a different key than your Debug install (see signature trap) |
| `-p:EmbedAssembliesIntoApk=true` | any **standalone** APK you `adb install` and launch without `dotnet` | a plain FastDev Debug APK won't launch standalone — it needs the dev host; symptom is an immediate crash |

## Prerequisites (once per machine)

- **.NET SDK** matching `<TFM>` + MAUI Android workload: `dotnet workload install maui-android`
- **Android SDK** with `platform-tools` (for `adb`) **on PATH**; a created **AVD**
- **JDK 17+** (required by Appium's UiAutomator2 driver)
- **For UI tests:** Node 18+, `npm i -g appium`, `appium driver install uiautomator2`
- `adb` must be on PATH; the **emulator binary usually is not** — launch via the full path:
  `& "$env:ANDROID_HOME\emulator\emulator.exe" -list-avds` then `... -avd <AVD_NAME>`

## Workflow A — run / iterate the app

```powershell
# 1. Emulator up? (start it if `adb devices` shows nothing)
adb devices
& "$env:ANDROID_HOME\emulator\emulator.exe" -avd <AVD_NAME>   # if needed; wait for boot

# 2. Build + deploy + launch
dotnet build <CSPROJ> -t:Run -f <TFM>
```

After a code change, re-run the same `-t:Run` command. If the app didn't
change, re-read the **no-op build** gotcha below — a 1-second "Build succeeded"
means nothing recompiled.

## Workflow B — run the Appium UI tests

The test project attaches to an **already-installed** app (`noReset: true`),
so deploy is a *separate* step from the test run. If the harness seeds the DB
via `run-as`, the install **must be Debug**.

```powershell
# 1. Emulator running + Appium server running (separate terminal: `appium`)
# 2. Deploy a debuggable build
dotnet build <CSPROJ> -t:Install -f <TFM> -c Debug
# 3. Run the suite (filter to keep runs short)
dotnet test <UITESTS> --filter "Section=9-Archive"
```

For a **full** suite (often >5 min): don't run it inline and watch — hand the
operator the one-liner and poll the result file, or run a tight `--filter`.
Inline long runs flood the transcript and you can't react mid-run.

## Workflow C — capture screenshots

Capturing specific UI states + cropping for store/PR has its own moving parts
(seed data, dark mode, device matrix, base64 token limits). See
**`references/screenshots.md`** for the full runbook. The two load-bearing
rules: **capture with `adb exec-out screencap -p > file.png`** (Appium's
screenshot returns base64 that blows tool-result token limits) and **navigate
with Appium** (`adb shell input tap` coordinates are unreliable).

If your project already has a capture *test* pattern (a `[Fact]` that drives to
a state and calls a screenshot helper), prefer adding one there over ad-hoc
driving — it's repeatable and lives with the suite.

## Field-proven gotchas (this is the part that saves the hour)

These are failure modes that have actually bitten, with the fix. Read them
before improvising.

1. **Use PowerShell for `adb`, not Git Bash.** MSYS rewrites device-side POSIX
   paths — `adb push x /data/local/tmp/y` or `adb shell run-as <id> cp …
   files/…` gets its `/data/...` argument mangled into a Windows path like
   `C:/Program Files/Git/...`, so the push/copy silently hits the wrong place
   or errors. Run `adb` from PowerShell, or disable translation per-command
   (`MSYS_NO_PATHCONV=1 adb shell …`, or double the leading slash:
   `//data/local/tmp/…`). This is the #1 cause of "the deploy/seed is wedged."

2. **Don't drive emulator/build/deploy/test from a *background* agent.**
   Background subagents auto-deny any tool call that would otherwise prompt for
   permission. A non-allowlisted `dotnet build` / `adb` / `dotnet test` then
   fails outright with no interactive recovery, and the agent spins on retries.
   Run these interactively on the main thread, or hand the operator the
   one-liner. (A *synchronous* subagent is fine — prompts surface to the user.)

3. **A 1–2 second "Build succeeded" is a no-op.** Incremental MSBuild skipped
   everything. If you changed source and the resulting APK's mtime did **not**
   advance, you did not actually rebuild — and you'll install/screenshot stale
   bits. Verify the APK timestamp before trusting a build; force a real rebuild
   with a touched source file or by clearing `bin/`+`obj/` for the TFM.

4. **Build Debug for `run-as`-based DB seeding.** `adb shell run-as <APP_ID> …`
   only works on a **debuggable** app. A Release build fails the seed step
   (`run-as … cp … failed`) before a single test runs. If a project script
   builds `-c Release` and also seeds via `run-as`, that script is a trap —
   build `-c Debug` for the test path.

5. **`EmbedAssembliesIntoApk=true` for any standalone APK.** Fast Deployment
   keeps your assemblies on the dev host, not in the APK. An `adb install`ed
   FastDev Debug APK launches only while `dotnet` is hosting it; standalone it
   crashes. Add the flag for APKs you install and launch on their own.

6. **Never `adb uninstall` a FastDev (non-embedded) Debug MAUI app.** Fast
   Deployment stores override assemblies on-device *separately* from the APK;
   uninstall wipes them and the reinstalled APK `SIGABRT`s on launch. To
   redeploy cleanly use `dotnet build -t:Install`. Only `adb uninstall` builds
   made with `EmbedAssembliesIntoApk=true`.

7. **`INSTALL_FAILED_UPDATE_INCOMPATIBLE` = signature mismatch.** The device
   holds one install per package, signed with that build's key. Installing a
   *different config* over it (classic: `adb install -r` a **Release** APK on
   top of a **Debug** install) fails. Keep the config consistent, redeploy via
   `dotnet build -t:Install`, or (for embedded-APK builds only) uninstall
   first.

8. **Absolute paths for `adb push` targets and screenshot outputs.** Relative
   paths fail silently — you get exit 0 and no file where you expected it.

9. **Re-chown after `adb push` into the app's data dir.** Ownership can flip to
   `root:root`, after which the app can't read its own file. Read the right
   owner from an existing file (`adb shell stat -c '%U:%G'
   /data/data/<APP_ID>/files/<something>`) and `adb shell chown` the pushed
   file back to it.

10. **Don't open Appium Inspector during a test run.** A second session kills
    the UiAutomator2 instrumentation; every remaining test fails with
    "instrumentation process is not running". Close Inspector before running.

## Troubleshooting (fast lookup)

| Symptom | Cause → Fix |
|---|---|
| `adb: command not found` | platform-tools not on PATH → add `$ANDROID_HOME\platform-tools` |
| `emulator: command not found` | emulator not on PATH → use `& "$env:ANDROID_HOME\emulator\emulator.exe"` |
| Build "succeeds" in ~1s, app unchanged | incremental no-op → verify APK mtime; clear `bin/`+`obj/` and rebuild |
| `INSTALL_FAILED_UPDATE_INCOMPATIBLE` | config/signature mismatch → `dotnet build -t:Install`, or uninstall (embedded APKs only) |
| App `SIGABRT`s right after an `adb install` | FastDev APK installed standalone, or uninstalled+reinstalled a FastDev app → rebuild with `-p:EmbedAssembliesIntoApk=true`, or `-t:Install` |
| `run-as … cp … failed` during seeding | Release build → rebuild `-c Debug` |
| `adb push`/`run-as` path looks Windows-y / wrong | Git Bash path mangling → run from PowerShell or `MSYS_NO_PATHCONV=1` |
| `Couldn't find app` (Appium) | app not installed → `dotnet build -t:Install` first |
| Pushed seed/file but app can't read it | ownership flipped to root → `adb shell chown` to app UID:GID |
| Whole deploy/seed "hangs" with no output | likely a background agent auto-denying a prompt → run interactively |

## This repo's concrete values

- App project: `PrayerApp/PrayerApp.csproj` · TFM `net10.0-android` · package
  `com.multithreadedllc.prayercards` · activity
  `crc6425c6d21f3599989c.MainActivity`
- AVD default `pixel_9_-_api_36_0` (override `ANDROID_AVD`); Appium at
  `http://127.0.0.1:4723` (override `APPIUM_SERVER_URL`)
- UI tests in `PrayerApp.UITests/` seed the DB via `run-as` (so **build
  Debug**); they attach to the installed app (`noReset: true`). Sections and
  helpers are documented in the project's `prayer-app-ui-testing` skill and
  `docs/RUNNING_UITESTS.md`.
- `run-uitests.ps1` builds `-c Release` — convenient for a clean APK but a trap
  for the `run-as` seed path; for seeded test runs prefer `-t:Install -c Debug`.
- Android screenshot runbook with seed data: `docs/plans/UX-22-android-screenshots.md`.

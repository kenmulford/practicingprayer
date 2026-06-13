# Android screenshot capture — reference runbook

Capturing specific UI states on an Android emulator for store listings or PR
visual review. Project-agnostic; substitute the `<...>` placeholders from
`SKILL.md`. Adapt device/seed specifics to your project.

## Load-bearing rules (get these wrong and you waste a run)

- **Capture with `adb exec-out screencap -p > <abs-path>.png`.** Appium's
  screenshot API returns base64 that overflows tool-result token limits — use
  it for *navigation* only, not capture.
- **Navigate with Appium** (`FindByAutomationId` + tap), not `adb shell input
  tap` — coordinate taps are unreliable across densities/orientations.
- **Absolute output paths.** `... > screenshots/x.png` can fail silently; use a
  full path.
- **Run `adb` from PowerShell on Windows** — Git Bash mangles the device-side
  `/sdcard/...` and `/data/...` paths (see `SKILL.md` gotcha #1).
- **Build a runnable APK:** `-p:EmbedAssembliesIntoApk=true` (a FastDev build
  won't launch standalone for capture).

## Two ways to drive to a state

1. **Capture test (preferred when a suite exists).** Add a `[Fact]` that resets
   state, navigates to the target screen using the project's UI-test helpers,
   and calls the project's screenshot helper (many suites already have one —
   e.g. a `CaptureDiagnostics(reason)` that writes a PNG to a temp dir and
   returns the path; mirror an existing capture test). Repeatable, lives with
   the suite, survives redesigns. Run it like any filtered test
   (`dotnet test <UITESTS> --filter "<your capture test>"`), then collect the
   PNG paths it prints.
2. **Manual drive (one-offs / store matrix).** Appium session for navigation +
   `adb exec-out screencap` for capture, per the steps below.

## Manual capture steps

```powershell
# 1. Build a standalone, runnable APK (Debug is fine; Release for store polish)
dotnet build <CSPROJ> -f <TFM> -c Release -p:EmbedAssembliesIntoApk=true
$APK = "<CSPROJ_DIR>/bin/Release/<TFM>/<APP_ID>-Signed.apk"

# 2. Boot device(s); note serials
& "$env:ANDROID_HOME\emulator\emulator.exe" -avd <AVD_NAME>     # wait for boot
adb devices                                                    # e.g. emulator-5554
$DEV = "emulator-5554"

# 3. Install (light mode to start)
adb -s $DEV install -r $APK
adb -s $DEV shell cmd uimode night no

# 4. (If seeding) terminate app, push seed DB, re-chown — see below

# 5. Launch, navigate (Appium), capture
adb -s $DEV shell am start -n <APP_ID>/<MAIN_ACTIVITY>
adb -s $DEV exec-out screencap -p > C:/abs/path/screenshots/02-cards.png

# 6. Dark mode: flip and retake the high-impact screens
adb -s $DEV shell cmd uimode night yes
```

## Seeding deterministic data (optional but recommended for store shots)

Realistic, stable data makes screenshots read well. Two ways to get a seed DB
onto the device:

- **`run-as` (debuggable app only):** push to a world-writable staging dir,
  then `run-as` copy into the app's private dir.
  ```powershell
  adb -s $DEV shell am force-stop <APP_ID>
  adb -s $DEV push <seed.db> /data/local/tmp/seed.db
  adb -s $DEV shell run-as <APP_ID> cp /data/local/tmp/seed.db files/<app.db>
  adb -s $DEV shell run-as <APP_ID> rm -f files/<app.db>-wal files/<app.db>-shm
  adb -s $DEV shell rm -f /data/local/tmp/seed.db
  ```
  Remove the SQLite `-wal`/`-shm` sidecars or they replay over your fresh seed.
- **Direct push + chown (emulator with root-ish adb):**
  ```powershell
  adb -s $DEV shell am force-stop <APP_ID>
  adb -s $DEV push <seed.db> /data/data/<APP_ID>/files/<app.db>
  # ownership may flip to root:root — set it back to the app UID:GID:
  $own = adb -s $DEV shell stat -c '%U:%G' /data/data/<APP_ID>/files
  adb -s $DEV shell chown $own /data/data/<APP_ID>/files/<app.db>
  ```

Onboarding / first-run flags often live in `shared_prefs` XML (Android), not in
the DB — push a prefs override to
`/data/data/<APP_ID>/shared_prefs/<APP_ID>_preferences.xml` and re-chown it too.
**Capture the onboarding/first-run screen before seeding** — once the
"completed" flag is set you can't return without an uninstall.

## Store-size cropping

Modern phones exceed Play Store's 2:1 max aspect (e.g. Pixel 9 is 1080×2424).
Crop to an allowed ratio at the end, in one batch (so you can re-shoot without
losing originals):

```powershell
# ImageMagick — centered crop to 1080×1920
magick <in.png> -gravity center -crop 1080x1920+0+0 +repage <out.png>
```

## Orientation (e.g. a landscape screen)

`adb -s $DEV shell settings put system user_rotation 1` (landscape) or Appium
`mobile: setOrientation`; reset to `0` afterward.

## Efficiency

- Do one device fully (light + dark) before switching — boot time dominates.
- Keep emulators booted across the whole session.
- Capture onboarding first (pre-seed); crop last (batch).
- Re-chown after *every* push into the app data dir.

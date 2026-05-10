# UX-22: Android Play Store Screenshots — Plan & Runbook

> Refreshed 2026-05-10 to reach parity with `app-store-screenshots.md` (iOS) after #33 UAT polish.

---

## Goal

Capture polished Play Store screenshots on Android phone and tablet emulators with realistic prayer data, in both light and dark mode. Same 10-screen catalog as iOS plus the new Confirm Import screen (phone-only, light + dark) — visual parity is the goal so that store listings read consistently across platforms.

---

## Cross-machine setup (prerequisites)

This plan runs on a machine with a working Android toolchain. The maintainer's primary Mac does not have one — captures happen on the secondary machine.

| Requirement | Notes |
|---|---|
| Android SDK + `platform-tools` (`adb`) | Recent SDK; ensure `adb` is on `PATH` |
| Phone AVD | Pixel 9 API 36 recommended (`pixel_9_-_api_36_0`) — 1080×2424 native |
| Tablet AVD | Pixel Tablet profile, 2560×1600 — name the AVD `screenshot_tablet` so the runbook's commands match |
| .NET 10 + MAUI Android workloads | `dotnet workload install maui-android` |
| ImageMagick **or** Python PIL | For the crop step (1080×2424 → 1080×1920) |
| Appium 3.x server | Recommended for navigation; coordinate taps via `adb shell input tap` are unreliable |

If `screenshot_tablet` AVD does not yet exist, create it from Android Studio's AVD Manager (Pixel Tablet device profile, latest stable system image with Google APIs).

---

## Play Store Requirements (2026)

| Form Factor | Dimensions | Aspect Ratio | Format | Quantity |
|---|---|---|---|---|
| Phone | Min 1080×1920, max 3840px per side | 16:9 or 9:16 | PNG (no alpha) or JPEG, <8MB | 2–8 (4+ for promo eligibility) |
| 7" Tablet | Same as phone | 16:9 or 9:16 | Same | Up to 8 |
| 10" Tablet | Min 1080px per side, max 7680px | 16:9 or 9:16 | Same | Up to 8 |

**Note:** Modern Android phones (Pixel 9: 1080×2424) exceed the 2:1 max aspect ratio. Screenshots are taken on the Pixel 9 emulator and **cropped to 1080×1920** (centered crop, trimming status bar overflow and nav bar).

---

## Screenshot List

### Per Device (Phone + Tablet)

| # | Screen | Filename | Notes |
|---|--------|----------|-------|
| 1 | Welcome / Onboarding | `01-welcome-onboarding.png` | Requires fresh install (uninstall + reinstall) |
| 2 | Prayer Cards | `02-prayer-cards.png` | Cards organized into collection sections (Personal, Ministry). Expand one to show nested requests. |
| 3 | Prayer Detail | `03-prayer-detail.png` | Tap "Wisdom for the job transition" — shows title, details, tags, Share/Mark Answered |
| 4 | Prayer List (Active) | `04-prayer-list.png` | Prayers tab with Active filter, tag chips, all requests listed |
| 5 | Answered Prayers | `05-answered-prayers.png` | Prayers tab with Answered filter — strikethrough text + date |
| 6 | Tags List | `06-tags-list.png` | Tags tab showing colored dots |
| 7 | Tag Detail | `07-tag-detail.png` | Tap "Family" tag — expand row, then tap Edit to reach the tag edit page with color picker swatches |
| 8 | Prayer Time | `08-prayer-time.png` | **Phone: landscape**, **tablet: portrait** |
| 9 | Quick Add | `09-quick-add.png` | Home → Quick Add button — modal with prayer title field |
| 10 | Manage Collections | `10-manage-collections.png` | Cards tab → Collections toolbar — user + system collections with card counts |
| 11 | Confirm Import | `11-confirm-import.png` | **Phone only — light + dark.** Triggered via Android share intent or opening a `.json` import payload from another app. Confirm Import page shows parsed prayer items + Import/Cancel buttons. **No tablet capture.** |

### Dark Mode (high-impact only, both devices)

| # | Screen | Filename | Notes |
|---|--------|----------|-------|
| 2 | Prayer Cards (expanded) | `dark/02-prayer-cards.png` | |
| 4 | Prayer List (Active) | `dark/04-prayer-list.png` | |
| 8 | Prayer Time | `dark/08-prayer-time.png` | |
| 10 | Manage Collections | `dark/10-manage-collections.png` | |
| 11 | Confirm Import | `dark/11-confirm-import.png` | Phone only |

### Android-only additions

| # | Screen | Filename | Notes |
|---|--------|----------|-------|
| 12 | Home Dashboard | `12-home-dashboard.png` | **Android only — phone + tablet, light only.** Metrics grid showing active cards, unanswered prayers, last prayed, overdue count. Android Play Store users land on Home by default; this is the highest-impact "first impression" tile. iOS users land on Cards, so this screen is omitted from the iOS pass. |

---

## Seed Data

All data is inserted directly into the SQLite database while the app is terminated. Timestamps use .NET ticks (100-nanosecond intervals since Jan 1, 0001). Values mirror iOS verbatim — same schema, same content, so screens read consistently across platforms.

**Android DB path:** `/data/data/com.multithreadedllc.prayercards/files/prayer_app.db`

The seed artifacts live in `screenshots/android/`:

- `prayer_app_seed.db` — fully seeded SQLite DB (push via `adb push`)
- `prayer_app_template.db` — empty schema-only template (regenerate from when schema changes)
- `prefs_override.xml` — shared_prefs override that flips onboarding/banner flags

### Timestamp Reference

```
NOW           = 639102550000000000  # ~March 28, 2026
WEEK_AGO      = 639101945200000000
TWO_WEEKS     = 639101340400000000
MONTH_AGO     = 639100130800000000
THREE_MONTHS  = 639092370000000000
ANSWERED_AT   = 639101500000000000
```

### Card Boxes (Collections)

| Id | Name | IsSystem | SystemKey | SortOrder |
|----|------|----------|-----------|-----------|
| 1 | System | 1 | system | 900 |
| 2 | Archived | 1 | archived | 999 |
| 3 | Personal | 0 | (null) | 0 |
| 4 | Ministry | 0 | (null) | 0 |

### Prayer Cards

| Id | Title | IsFavorite | IsSystem | BoxId | Created |
|----|-------|-----------|----------|-------|---------|
| 1 | Quick Add | 0 | 1 | 1 | (auto-created by app) |
| 2 | Family & Health | 1 | 0 | 3 | THREE_MONTHS |
| 3 | Career & Purpose | 0 | 0 | 4 | TWO_WEEKS |
| 4 | Gratitude | 0 | 0 | 3 | MONTH_AGO |

### Prayer Requests

| Id | Card | Title | Details | IsAnswered |
|----|------|-------|---------|-----------|
| 1 | Family & Health | Mom's recovery from knee surgery | She had surgery last Tuesday. Praying for a smooth recovery and that physical therapy goes well. The doctor says 6-8 weeks for full mobility. | No |
| 2 | Family & Health | Peace for Sarah during finals | She's been so stressed with exams coming up. Praying she finds rest and clarity, and remembers how capable she is. | No |
| 3 | Family & Health | Strength for Dad during treatment | Grateful the prognosis is good. Praying for endurance through the remaining rounds and for the whole family's peace. | **Yes** |
| 4 | Career & Purpose | Wisdom for the job transition | The new role starts in April. Praying for confidence in the learning curve and discernment about which opportunities to pursue. | No |
| 5 | Career & Purpose | Direction for the ministry opportunity | The youth group asked me to lead a small group this fall. Praying about whether this is the right season to say yes. | No |
| 6 | Gratitude | Thankful for our new church community | We've felt so welcomed since joining. Grateful for the connections and friendships forming. Praying we can give back and serve. | No |
| 7 | Quick Add | Safe travels for the mission trip team | *(empty details)* | No |

### Tags

| Id | Name | Color (light hex) |
|----|------|-------------------|
| 1 | Recently Notified | #505050 (system) |
| 2 | Family | #B84040 (Red) |
| 3 | Health | #1E7870 (Teal) |
| 4 | Work | #2E5A9A (Blue) |
| 5 | Guidance | #663C8C (Purple) |
| 6 | Gratitude | #B35A20 (Orange) |

### Tag Assignments (PrayerCardTag)

| Card | Tag | Request |
|------|-----|---------|
| Family & Health | Family | Mom's recovery |
| Family & Health | Health | Mom's recovery |
| Family & Health | Family | Peace for Sarah |
| Family & Health | Health | Strength for Dad |
| Family & Health | Family | Strength for Dad |
| Career & Purpose | Work | Wisdom for job |
| Career & Purpose | Guidance | Wisdom for job |
| Career & Purpose | Guidance | Direction for ministry |
| Gratitude | Gratitude | Thankful for community |

### Prayer Interactions

Interactions populate the "Last prayed" timestamps.

| Request | Type | When |
|---------|------|------|
| Mom's recovery | Prayed | WEEK_AGO |
| Peace for Sarah | Prayed | WEEK_AGO |
| Wisdom for job | Prayed | NOW |
| Direction for ministry | Prayed | NOW |
| Thankful for community | Prayed | TWO_WEEKS |
| Safe travels | Prayed | WEEK_AGO |

---

## Step-by-Step Runbook

### 1. Build for Android

```bash
dotnet build PrayerApp/PrayerApp.csproj -c Release -f net10.0-android \
  -p:EmbedAssembliesIntoApk=true
```

Output APK: `PrayerApp/bin/Release/net10.0-android/com.multithreadedllc.prayercards-Signed.apk` (path may vary by signing config).

### 2. Boot Emulators

Either via Android Studio AVD Manager, or:

```bash
emulator -avd pixel_9_-_api_36_0 &
emulator -avd screenshot_tablet &
```

Wait for both to boot, then list device serials:

```bash
adb devices
# emulator-5554  device  (phone)
# emulator-5556  device  (tablet)
PHONE=emulator-5554
TABLET=emulator-5556
APK=PrayerApp/bin/Release/net10.0-android/com.multithreadedllc.prayercards-Signed.apk
```

### 3. Install

```bash
adb -s $PHONE install -r $APK
adb -s $TABLET install -r $APK
adb -s $PHONE shell cmd uimode night no
adb -s $TABLET shell cmd uimode night no
```

### 4. Capture Onboarding (fresh install only)

Launch each device's app cold. The "Welcome to Practicing Prayer" popup appears on first launch.

```bash
adb -s $PHONE shell am start -n com.multithreadedllc.prayercards/crc6425c6d21f3599989c.MainActivity
sleep 3
adb -s $PHONE exec-out screencap -p > screenshots/android/phone/light/01-welcome-onboarding.png
```

Repeat for tablet. Then dismiss the popup via Appium or terminate + push the prefs override.

### 5. Push Seed DB + Preferences

Terminate the app, push the pre-built seed artifacts:

```bash
adb -s $PHONE shell am force-stop com.multithreadedllc.prayercards
adb -s $PHONE push screenshots/android/prayer_app_seed.db \
  /data/data/com.multithreadedllc.prayercards/files/prayer_app.db
adb -s $PHONE push screenshots/android/prefs_override.xml \
  /data/data/com.multithreadedllc.prayercards/shared_prefs/com.multithreadedllc.prayercards_preferences.xml
adb -s $PHONE shell chown -R $(adb -s $PHONE shell stat -c '%U:%G' /data/data/com.multithreadedllc.prayercards) \
  /data/data/com.multithreadedllc.prayercards/files \
  /data/data/com.multithreadedllc.prayercards/shared_prefs
```

`prefs_override.xml` flips `OnboardingComplete`, `QuickAddTipDismissed`, and `CollectionsBannerDismissed` to `true`. Repeat for tablet.

> If the schema has drifted since `prayer_app_seed.db` was last regenerated, rebuild it from `prayer_app_template.db` and the INSERT statements implied by the Seed Data section above. The schema is shared with iOS — no Android-specific columns.

### 6. Take Screenshots

Use Appium for navigation, `adb exec-out screencap` for capture:

```bash
adb -s $PHONE exec-out screencap -p > screenshots/android/phone/light/02-prayer-cards.png
```

For phone Prayer Time (landscape): rotate the emulator first via Appium (`mobile: setOrientation`) or `adb shell settings put system user_rotation 1`, then capture.

### 7. Switch to Dark Mode

```bash
adb -s $PHONE shell cmd uimode night yes
```

Retake the 4 dark-mode screens (`02`, `04`, `08`, `10`) on each device, plus `11` on phone.

### 8. Crop Phone Screenshots to 1080×1920

ImageMagick:

```bash
for f in screenshots/android/phone/{light,dark}/*.png; do
  magick "$f" -gravity center -crop 1080x1920+0+0 +repage "$f"
done
```

Python PIL alternative:

```python
from PIL import Image
import glob
for path in glob.glob('screenshots/android/phone/**/*.png', recursive=True):
    img = Image.open(path)
    if img.size == (1080, 2424):
        top = (2424 - 1920) // 2
        img.crop((0, top, 1080, top + 1920)).save(path)
```

Tablet screenshots from `screenshot_tablet` AVD (2560×1600 or 1600×2560) need no crop if already within Play Store ratio limits — verify per-image.

---

## Output Structure

```
screenshots/
  android/
    phone/
      light/   01 through 12
      dark/    02, 04, 08, 10, 11
    tablet/
      light/   01 through 10, plus 12 (no 11 — phone-only)
      dark/    02, 04, 08, 10
```

---

## Gotchas

| Issue | Workaround |
|-------|-----------|
| Pixel 9 native is 1080×2424, exceeds Play Store 2:1 max | Crop to 1080×1920 (centered) via ImageMagick or PIL — see runbook step 8 |
| `adb shell input tap` is unreliable for fine-grained nav | Use Appium with `FindByAutomationId`; reserve `input tap` for top-level fallback only |
| Tablet emulator AVD `screenshot_tablet` may not exist on a fresh machine | Create from Android Studio AVD Manager (Pixel Tablet profile) before starting |
| Onboarding/banner flags live in shared_prefs XML, not NSUserDefaults | Push `screenshots/android/prefs_override.xml` (see **Prefs override file** subsection below for full key list) to `/data/data/com.multithreadedllc.prayercards/shared_prefs/com.multithreadedllc.prayercards_preferences.xml`; chown to the app's UID after push. |
| File ownership after `adb push` may flip to `root:root` and prevent app reads | `adb shell chown` back to the app's UID:GID (use `stat -c '%U:%G'` on an existing file in the app's data dir) |
| Tag Detail requires two taps | Tap the tag row to expand (Edit/Delete inline buttons), then tap Edit to reach the tag edit page with color picker swatches |
| Keyboard on Quick Add | Quick Add modal auto-focuses the title field; soft keyboard shows. Either accept it in the screenshot or `adb shell input keyevent 111` (KEYCODE_ESCAPE) to dismiss. |
| Rotating the phone for Prayer Time | `adb shell settings put system user_rotation 1` (landscape) or use Appium `mobile: setOrientation`. Reset to `0` afterward. |
| App must be built with `EmbedAssembliesIntoApk=true` | FastDev builds won't run standalone on the emulator without the dev host |
| **No Android toolchain on maintainer's primary Mac** | Per `feedback_no_android_toolchain_local.md`: this entire plan runs on the secondary machine that has Android SDK + emulator. Don't attempt local execution from the primary Mac. |
| Confirm Import (#11) requires a real share intent | Either build a tiny "send to PrayerApp" companion app, or use `adb shell am start -a android.intent.action.SEND -t application/json --es android.intent.extra.STREAM file:///sdcard/Download/sample-import.json -n com.multithreadedllc.prayercards/...` once the receiving activity is wired |

### Prefs override file

The contents of `screenshots/android/prefs_override.xml` were opened and verified empirically (not assumed). The file holds 11 keys total — 6 are screenshot-relevant, 5 are app defaults included to keep the seeded state stable.

**Source:** `screenshots/android/prefs_override.xml`
**Push destination:** `/data/data/com.multithreadedllc.prayercards/shared_prefs/com.multithreadedllc.prayercards_preferences.xml`

**Screenshot-relevant keys:**

| Key | Value | Why it matters for capture |
|-----|-------|----------------------------|
| `AllowNotifications` | `false` | Suppresses the Android 13+ runtime notification-permission prompt that would otherwise surface a system dialog mid-capture |
| `PrayerTimeLandscape` | `true` | Forces the Prayer Time carousel into landscape; required for screen #08 |
| `OnboardingComplete` | `true` | Short-circuits the welcome flow on app launch |
| `OnboardingStep` | `"Complete"` | String paired with the bool above; some legacy code paths read this instead |
| `QuickAddTipDismissed` | `true` | Hides the Quick Add coachmark on Home |
| `CollectionsBannerDismissed` | `true` | Hides the Collections-feature banner |

**App defaults (not screenshot-specific, included for state stability):** `FirstRun=false`, `DefaultNotifyHour=9`, `DefaultNotifyMinute=0`, `ArchivedFolderId=2`, `OverdueDayThreshold=30`.

After pushing, re-chown the file to the app's UID:GID — see the chown command in runbook step 5.

---

## Efficiency Tips

1. **Do one device fully (light + dark) before moving to the next** — reduces emulator-state churn
2. **Use `adb exec-out screencap -p > path.png` for captures, Appium for navigation only** — Appium screenshots return base64 that exceeds tool result token limits
3. **Absolute paths for `adb push` targets and screenshot outputs** — relative paths fail silently
4. **Keep both emulators booted across the whole session** — boot time per emulator is the dominant overhead
5. **Capture Onboarding before any seed push** — once `OnboardingComplete=true` is in shared_prefs, you can't go back without an uninstall
6. **Re-chown after every `adb push` into the app's data dir** — Android namespacing will reject reads otherwise
7. **Crop phone screenshots in one batch at the end** — don't crop incrementally; lets you re-shoot without losing originals

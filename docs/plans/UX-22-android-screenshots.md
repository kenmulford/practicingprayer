# UX-22: Android Play Store Screenshots

## Overview

Automated screenshot capture for Google Play Store listing. Matches the iOS App Store screenshot approach: seed a SQLite database with realistic content, navigate to key screens, capture screenshots in both light and dark mode.

## Play Store Requirements (2026)

| Form Factor | Dimensions | Aspect Ratio | Format | Quantity |
|---|---|---|---|---|
| Phone | Min 1080x1920, max 3840px per side | 16:9 or 9:16 | PNG (no alpha) or JPEG, <8MB | 2-8 (4+ for promo eligibility) |
| 7" Tablet | Same as phone | 16:9 or 9:16 | Same | Up to 8 |
| 10" Tablet | Min 1080px per side, max 7680px | 16:9 or 9:16 | Same | Up to 8 |

**Note:** Modern Android phones (Pixel 9: 1080x2424) exceed the 2:1 max aspect ratio. Screenshots are taken on the Pixel 9 emulator and **cropped to 1080x1920** (centered crop, trimming status bar overflow and nav bar).

## Target Screens (4 screens × 2 modes = 8 per form factor)

1. **Home Dashboard** — metrics grid showing active cards, unanswered prayers, last prayed, overdue count
2. **Prayer Cards** — cards organized into collection sections (Personal, Ministry), one expanded to show nested requests
3. **Prayer List** — active filter selected, tag chips visible, multiple prayers with card labels
4. **Prayer Time** — landscape carousel showing prayer title + details, progress bar, nav buttons
5. **Manage Collections** — Cards tab → Collections toolbar — user + system collections with card counts

## Approach

### 1. Seed Database via SQLite

Push a pre-built `prayer_app.db` directly to the emulator's app data directory. This avoids UI interaction for data setup.

**Android DB path:** `/data/data/com.multithreadedllc.prayercards/files/prayer_app.db`

**Seed data (matching iOS screenshots):**

Card Boxes (Collections):
- Id=1: "System" (IsSystem=true, SystemKey="system", SortOrder=900)
- Id=2: "Archived" (IsSystem=true, SystemKey="archived", SortOrder=999)
- Id=3: "Personal" (user collection)
- Id=4: "Ministry" (user collection)

Cards (with BoxId assignments):
- "Quick Add" (system card, BoxId=1)
- "Family & Health" (favorited, 2 active prayers, BoxId=3 Personal)
- "Career & Purpose" (expanded in screenshot, 2 prayers, BoxId=4 Ministry)
- "Gratitude" (1 prayer, BoxId=3 Personal)

Prayers (PrayerRequest table):
- "Mom's recovery from knee surgery" (Family & Health, has details)
- "Peace for Sarah during finals" (Family & Health)
- "Direction for the ministry opportunity" (Career & Purpose)
- "Wisdom for the job transition" (Career & Purpose)
- "Thankful for our new church community" (Gratitude)
- "Safe travels for the mission trip team" (Quick Add)

Tags:
- "Family", "Gratitude", "Guidance", "Health", "Recently Notified"

PrayerInteractions:
- At least one interaction per prayer (so "last prayed" metric shows a date)

**Preferences to set via `adb shell`:**
```bash
# Mark onboarding complete
adb shell "run-as com.multithreadedllc.prayercards cat /dev/null > /data/data/com.multithreadedllc.prayercards/shared_prefs/com.multithreadedllc.prayercards_preferences.xml"
# Or use am broadcast to set preferences
```

Actually simpler: launch the app once after seeding, let it create preferences, then set OnboardingComplete=true via the app's Preferences API by pushing an XML file.

### 2. Screenshot Capture Script

```bash
# For each screen:
# 1. Navigate via adb shell am start or UI automation
# 2. Wait for render
# 3. Capture: adb exec-out screencap -p > screenshot.png
# 4. Crop to 1080x1920 (ImageMagick or Python PIL)
```

### 3. Dark Mode Toggle

```bash
# Enable dark mode
adb shell cmd uimode night yes
# Disable dark mode
adb shell cmd uimode night no
```

### 4. Output Directory Structure

```
screenshots/
  android/
    phone/
      light/
        01-home-dashboard.png
        02-prayer-cards.png
        03-prayer-list.png
        04-prayer-time.png
      dark/
        01-home-dashboard.png
        02-prayer-cards.png
        03-prayer-list.png
        04-prayer-time.png
    tablet/
      light/
        (same 4 screens)
      dark/
        (same 4 screens)
```

## Emulator Setup

- **Phone:** Pixel 9 API 36 (`pixel_9_-_api_36_0`) — 1080x2424, cropped to 1080x1920
- **Tablet:** `screenshot_tablet` AVD (Pixel Tablet profile, 2560x1600) — crops to 16:9 or 9:16

## Prerequisites

- Android emulator running
- App installed with `EmbedAssembliesIntoApk=true` (no FastDev)
- `adb` available
- ImageMagick or Python PIL for cropping (or manual crop)
- Appium server running (if using Appium for navigation)

## Navigation Strategy

Use `adb shell` commands to navigate:
```bash
# Force stop and relaunch to Home
adb shell am force-stop com.multithreadedllc.prayercards
adb shell am start -n com.multithreadedllc.prayercards/crc6425c6d21f3599989c.MainActivity

# Tab navigation via Shell routes (if possible) or UI taps via input tap coordinates
```

Or use Appium for reliable navigation with `FindByAutomationId` — more robust than coordinate taps.

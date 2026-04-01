# App Store Screenshots — Plan & Runbook

> Created 2026-03-28 after completing the first iOS screenshot session.

---

## Goal

Capture polished App Store screenshots on iPhone and iPad simulators with realistic prayer data, in both light and dark mode.

---

## Device & Dimension Matrix

Apple's docs and App Store Connect validation **disagree**. These are the validated sizes that actually upload successfully:

| Category | Simulator | Native Resolution | Accepted Size | Action |
|----------|-----------|-------------------|---------------|--------|
| iPhone 6.5" portrait | iPhone 17 Pro Max | 1320 × 2868 | **1284 × 2778** | Resize with `sips -z 2778 1284` |
| iPhone 6.5" landscape | iPhone 17 Pro Max | 2868 × 1320 | **2778 × 1284** | Resize with `sips -z 1284 2778` |
| iPad 13" portrait | iPad Pro 13" (M5) | 2064 × 2752 | **2064 × 2752** | No resize needed |

> The 6.9" category (1320×2868) that Apple's docs list as the primary required size is **rejected** by App Store Connect validation as of March 2026. Use the 6.5" category instead.

---

## Screenshot List

### Per Device (iPhone + iPad)

| # | Screen | Filename | Notes |
|---|--------|----------|-------|
| 1 | Welcome / Onboarding | `01-welcome-onboarding.png` | Requires fresh install (uninstall + reinstall) |
| 2 | Prayer Cards | `02-prayer-cards.png` | Expand "Career & Purpose" to show nested requests; other cards show badge counters |
| 3 | Prayer Detail | `03-prayer-detail.png` | Tap "Wisdom for the job transition" — shows title, details, tags, Share/Mark Answered |
| 4 | Prayer List (Active) | `04-prayer-list.png` | Prayers tab with Active filter, tag chips, all requests listed |
| 5 | Answered Prayers | `05-answered-prayers.png` | Prayers tab with Answered filter — strikethrough text + date |
| 6 | Tags List | `06-tags-list.png` | Tags tab showing colored dots |
| 7 | Tag Detail | `07-tag-detail.png` | Tap "Family" tag — shows name, color picker swatches |
| 8 | Prayer Time | `08-prayer-time.png` | **iPhone: landscape** (rotate image with `sips -r 270`), **iPad: portrait** |
| 9 | Quick Add | `09-quick-add.png` | Home → Quick Add button — shows modal with prayer title field |

### Dark Mode (high-impact only, both devices)

| # | Screen | Filename |
|---|--------|----------|
| 1 | Prayer Cards (expanded) | `dark/02-prayer-cards.png` |
| 2 | Prayer List (Active) | `dark/04-prayer-list.png` |
| 3 | Prayer Time | `dark/08-prayer-time.png` |

---

## Seed Data

All data is inserted directly into the SQLite database while the app is terminated. Timestamps use .NET ticks (100-nanosecond intervals since Jan 1, 0001).

### Timestamp Reference

```
NOW           = 639102550000000000  # ~March 28, 2026
WEEK_AGO      = 639101945200000000
TWO_WEEKS     = 639101340400000000
MONTH_AGO     = 639100130800000000
THREE_MONTHS  = 639092370000000000
ANSWERED_AT   = 639101500000000000
```

### Prayer Cards

| Id | Title | IsFavorite | IsSystem | Created |
|----|-------|-----------|----------|---------|
| 1 | Quick Add | 0 | 1 | (auto-created by app) |
| 2 | Family & Health | 1 | 0 | THREE_MONTHS |
| 3 | Career & Purpose | 0 | 0 | TWO_WEEKS |
| 4 | Gratitude | 0 | 0 | MONTH_AGO |

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

### 1. Build for Simulator

```bash
dotnet build PrayerApp/PrayerApp.csproj -c Release -f net10.0-ios -r iossimulator-arm64
```

Output: `PrayerApp/bin/Release/net10.0-ios/iossimulator-arm64/PrayerApp.app`

### 2. Boot & Install

```bash
IPHONE=AD03FB0C-F5F3-49C3-AA7B-E06014F19594  # iPhone 17 Pro Max
IPAD=9D882FAF-5ACF-474D-BC6C-5DCC96A74CF4    # iPad Pro 13" (M5)
APP=PrayerApp/bin/Release/net10.0-ios/iossimulator-arm64/PrayerApp.app

xcrun simctl boot $IPHONE
xcrun simctl boot $IPAD
xcrun simctl install $IPHONE $APP
xcrun simctl install $IPAD $APP
xcrun simctl ui $IPHONE appearance light
xcrun simctl ui $IPAD appearance light
```

### 3. Capture Onboarding (fresh install only)

Launch the app. The "Welcome to Practicing Prayer" popup appears on first launch.

```bash
xcrun simctl launch $IPHONE com.multithreadedllc.prayercards
sleep 3
xcrun simctl io $IPHONE screenshot screenshots/iphone/light/01-welcome-onboarding.png
```

Repeat for iPad. Then dismiss the popup via Appium or terminate + mark onboarding complete.

### 4. Populate Seed Data

Terminate the app, find the DB, insert data:

```bash
xcrun simctl terminate $IPHONE com.multithreadedllc.prayercards
DB=$(find ~/Library/Developer/CoreSimulator/Devices/$IPHONE/data/Containers/Data -name "prayer_app.db")
# Run the INSERT statements from the Seed Data section above
# Mark onboarding complete:
xcrun simctl spawn $IPHONE defaults write com.multithreadedllc.prayercards OnboardingComplete -bool true
```

### 5. Take Screenshots

Use Appium MCP for navigation, `xcrun simctl io` for capture:

```bash
xcrun simctl io $IPHONE screenshot <output-path>.png
```

For iPhone Prayer Time (landscape): capture in portrait, then rotate:
```bash
sips -r 270 input.png --out output.png
```

### 6. Switch to Dark Mode

```bash
xcrun simctl ui $IPHONE appearance dark
```

Retake the 3 key screenshots (prayer cards, prayer list, prayer time).

### 7. Batch Resize iPhone Screenshots

```bash
for f in screenshots/iphone/{light,dark}/*.png; do
  w=$(sips -g pixelWidth "$f" | awk '/pixelWidth/{print $2}')
  h=$(sips -g pixelHeight "$f" | awk '/pixelHeight/{print $2}')
  if [ "$w" = "1320" ] && [ "$h" = "2868" ]; then
    sips -z 2778 1284 "$f"
  elif [ "$w" = "2868" ] && [ "$h" = "1320" ]; then
    sips -z 1284 2778 "$f"
  fi
done
```

iPad screenshots (2064×2752) need no resize.

---

## Output Structure

```
screenshots/
  iphone/
    light/   01 through 09
    dark/    02, 04, 08
  ipad/
    light/   01 through 09
    dark/    02, 04, 08
```

---

## Gotchas

| Issue | Workaround |
|-------|-----------|
| App Store Connect rejects 1320×2868 (6.9") | Resize to 1284×2778 (6.5") with `sips` |
| `xcrun simctl orientation` doesn't exist | Capture portrait, rotate with `sips -r 270` |
| Appium sessions drop frequently | Recreate session before each navigation burst |
| Onboarding popup only shows on fresh install | Uninstall + reinstall to retrigger |
| Prayer Time auto-rotates to landscape on iPhone | App setting "Landscape Mode" controls this |
| iPad screenshots accepted as-is | 2064×2752 matches the 13" category exactly |
| Seed data timestamps | Must be .NET ticks format, not Unix epoch |

# iOS UITest Setup & Adaptation Guide

> This doc provides everything needed to adapt and run the Appium UITest suite on iOS.
> Pull `dev` on the Mac, then use this as the reference for the iOS session.

---

## Quick Start

```bash
# 1. Pull latest code
cd ~/repos/PrayerApp
git checkout dev && git pull

# 2. Build the iOS app (Release)
dotnet build PrayerApp/PrayerApp.csproj -f net10.0-ios -c Release

# 3. Start Appium
appium --port 4723 &

# 4. Run tests (skip Android-only tests)
dotnet test PrayerApp.UITests/PrayerApp.UITests.csproj --filter "Platform!=Android"
```

---

## Configuration

The test framework auto-detects macOS and uses iOS capabilities. Override defaults via environment variables:

| Variable | Default | Description |
|----------|---------|-------------|
| `APPIUM_SERVER_URL` | `http://127.0.0.1:4723` | Appium server address |
| `IOS_SIMULATOR` | `iPhone 17` | Simulator device name |
| `IOS_VERSION` | `26.0` | iOS version |

Example:
```bash
IOS_SIMULATOR="iPhone 16 Pro" IOS_VERSION="26.4" dotnet test PrayerApp.UITests/ --filter "Platform!=Android"
```

---

## Project Structure

```
PrayerApp.UITests/
  Infrastructure/
    AppiumSetup.cs          — IAsyncLifetime fixture, shared driver, session recovery
    AppiumCollection.cs     — xUnit [Collection("Appium")] definition
    TestConfig.cs           — Platform-aware config (Windows=Android, macOS=iOS)
  Helpers/
    AppExtensions.cs        — NavigateToTab, FindByAutomationId, WaitForElement, etc.
  Tests/
    HomeTests.cs            — 1 test
    QuickAddTests.cs        — 4 tests
    OnboardingTests.cs      — 2 tests
    PrayerCardTests.cs      — 10 tests
    PrayerListTests.cs      — 9 tests
    PrayerTimeTests.cs      — 5 tests
    ReminderTests.cs        — 3 tests
    TagTests.cs             — 5 tests
    SettingsTests.cs        — 7 tests
    UnsavedChangesTests.cs  — 4 tests
    EdgeCaseTests.cs        — 5 tests
    AndroidTests.cs         — 3 tests (Android-only, skip on iOS)
```

**Total: 58 tests** (55 cross-platform + 3 Android-only)

---

## Architecture Decisions

- **Single shared driver** via xUnit collection fixture (`[Collection("Appium")]`) — one app launch for entire test run
- **Session recovery**: `EnsureSessionAlive()` in `AppiumSetup.cs` checks `PageSource`, recreates driver on crash. Called by `EnsureOnTab()` before every test's navigation.
- **Platform detection**: `TestConfig.IsAndroid` / `TestConfig.IsIOS` via `RuntimeInformation.IsOSPlatform`
- **AutomationId mapping**: Android uses XPath checking both `resource-id` and `content-desc`; iOS uses `MobileBy.AccessibilityId` directly
- **Tab navigation**: `MobileBy.AccessibilityId(tabTitle)` for Shell tabs — works on both platforms

---

## What Already Works Cross-Platform

These helpers in `AppExtensions.cs` use `AutomationIdLocator` which already branches by platform — no changes needed:

- `FindByAutomationId`, `WaitForElement`, `WaitForElementGone`
- `Tap`, `WaitAndTap`, `EnterText`, `GetText`
- `IsDisplayed`
- `EnsureOnTab`, `NavigateToNewPrayer`, `DismissOnboardingIfPresent`
- `GoBack` — `driver.Navigate().Back()` works on both

---

## iOS-Specific Adaptations Needed

These helpers in `AppExtensions.cs` use Android-specific XPath attributes and need iOS branches:

### Text-based element finding
| Helper | Android XPath | iOS equivalent |
|--------|--------------|----------------|
| `NavigateToTab` | `@text`, `@content-desc` | `@name` or `@label` |
| `TapToolbarItem` | `@text`, `@content-desc` | `@name` or `@label` |
| `IsTextDisplayed` | `@text` | `@name` or `@label` |
| `TapByText` | `@text` | `@name` or `@label` |
| `FindByText` | `@text` | `@name` or `@label` |

### Alert handling
| Helper | Android | iOS |
|--------|---------|-----|
| `IsAlertPresent` | `android:id/message` resource-id | Accessibility-based alert detection |
| `DismissAlertIfPresent` | `android:id/button1` / `button2` | iOS alert button locators |
| `TapAlertButton` | Same Android resource-ids | iOS locators |

### Gestures
| Helper | Android | iOS |
|--------|---------|-----|
| `ScrollDownTo` | `mobile: swipeGesture` | `mobile: scroll` |
| `SwipeElement` | `mobile: swipeGesture` | `mobile: swipe` or equivalent |

### App lifecycle
| Helper | Android | iOS |
|--------|---------|-----|
| `ActivateApp` | `TestConfig.AndroidPackage` | `TestConfig.IOSBundleId` |

---

## Android Test Results (baseline from Windows)

Use these as a reference for what to expect. Failures marked below are test logic issues, not infrastructure.

| Test Class | Passing | Notes |
|------------|---------|-------|
| EdgeCaseTests | 5/5 | All pass |
| HomeTests | 1/1 | All pass |
| OnboardingTests | 2/2 | All pass |
| PrayerTimeTests | 5/5 | All pass |
| QuickAddTests | 4/4 | All pass |
| AndroidTests | 2/3 | Hardware back + dirty dialog timing |
| PrayerCardTests | 5/10 | Swipe gestures not revealing actions |
| PrayerListTests | 4/9 | Data dependencies, toolbar timing |
| SettingsTests | 5/7 | Partial failures |
| ReminderTests | 0/3 | All failing — timing/data issues |
| TagTests | ~3/5 | Partial |
| UnsavedChangesTests | ~2/4 | Dialog detection timing |

**Common failure causes:**
- Swipe gestures don't reliably reveal SwipeView actions
- Tests depend on specific data ("UI Test Prayer") not existing
- `DisplayAlertAsync` dialog detection has timing issues
- `TapToolbarItem("Add")` timeout when already on detail page

---

## Recommended Approach

1. **Refactor text-based helpers** in `AppExtensions.cs` to branch on `TestConfig.IsIOS` for XPath attributes (`@name`/`@label` instead of `@text`)
2. **Refactor alert helpers** to use iOS-native alert detection patterns
3. **Run existing suite** on iOS simulator — most tests should pass with helper adaptations
4. **Add `IOSTests.cs`** mirroring `AndroidTests.cs` for iOS-specific behaviors (if any)
5. Fix the ~25 Android failures where the test logic applies to both platforms

---

## Troubleshooting

- **WebDriverAgent build fails**: Open `~/.appium/node_modules/appium-xcuitest-driver/node_modules/appium-webdriveragent/WebDriverAgent.xcodeproj` in Xcode and fix signing
- **Simulator not found**: `xcrun simctl list devices` to see available simulators, then set `IOS_SIMULATOR` env var
- **AccessibilityId not found**: MAUI `AutomationId` maps to `accessibilityIdentifier` on iOS — verify in Accessibility Inspector
- **Tests use `AccessibilityId`** which maps to MAUI `AutomationId` on iOS
- **Appium XCUITest driver** communicates with iOS simulators via WebDriverAgent

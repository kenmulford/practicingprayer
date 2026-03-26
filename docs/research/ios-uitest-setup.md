# iOS UITest Setup (Mac)

**Prerequisites (already installed):**
- Appium with XCUITest driver
- Xcode with iOS simulators
- .NET 10 SDK

---

## Running iOS UI Tests

```bash
# 1. Pull latest code
cd ~/repos/PrayerApp   # or wherever the repo is cloned
git pull

# 2. Build the iOS app (Release)
dotnet build PrayerApp/PrayerApp.csproj -f net10.0-ios -c Release

# 3. Start Appium
appium --port 4723 &

# 4. Run tests (iOS only)
dotnet test PrayerApp.UITests/PrayerApp.UITests.csproj --filter "Platform=iOS"
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
IOS_SIMULATOR="iPhone 16 Pro" IOS_VERSION="26.4" dotnet test PrayerApp.UITests/ --filter "Platform=iOS"
```

---

## Notes

- Tests use `AccessibilityId` which maps to MAUI `AutomationId` on iOS
- iOS tests share the same test files as Android — platform detection is automatic
- Appium XCUITest driver communicates with iOS simulators via WebDriverAgent
- If WebDriverAgent build fails, open `~/.appium/node_modules/appium-xcuitest-driver/node_modules/appium-webdriveragent/WebDriverAgent.xcodeproj` in Xcode and fix signing

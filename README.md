# Practicing Prayer

A privacy-first prayer journal for iOS and Android. Prayer cards, prayer requests, and a focused "Prayer Time" session — all stored locally on your device. No accounts, no cloud sync, no analytics, no backend.

<p align="center">
  <img src="screenshots/android/phone/light/01-home-dashboard.png" alt="Home dashboard, light mode" width="300">
  <img src="screenshots/android/phone/dark/01-home-dashboard.png" alt="Home dashboard, dark mode" width="300">
</p>

## What it is

Practicing Prayer organizes prayer around three primitives:

- **Prayer cards** — a topic, person, or theme you return to over time
- **Prayer requests** — specific items inside a card, with answered/unanswered state and a date when answered
- **Prayer time** — a focused session that walks through your cards and requests

Everything lives in a local SQLite database. There are no user accounts, no telemetry, and nothing leaves the device unless you explicitly export or share. Tags, colors, and collections exist for personal organization, but the three primitives above are the core loop.

## Tech stack

- **.NET MAUI 10** targeting `net10.0-android` and `net10.0-ios`
- **CommunityToolkit.Mvvm** — `ObservableObject` + explicit `SetProperty` / `RelayCommand`
- **CommunityToolkit.Maui** — popups and platform helpers
- **sqlite-net-pcl** — Active Record-style model layer
- **Plugin.LocalNotification** — local prayer reminders
- **xUnit + NSubstitute** — unit tests

## Build and run

You'll need the .NET 10 SDK with the MAUI workloads installed:

```bash
dotnet workload install maui
```

Then, from the repo root:

```bash
# Android (emulator or USB-debugging device)
dotnet build PrayerApp/PrayerApp.csproj -t:Run -f net10.0-android

# iOS (macOS + Xcode required)
dotnet build PrayerApp/PrayerApp.csproj -t:Run -f net10.0-ios
```

Release builds for Android are signed via environment variables — see the `AndroidSigningStorePass` / `AndroidSigningKeyPass` block in `PrayerApp/PrayerApp.csproj`.

## Project layout

```
PrayerApp/              Main app (MAUI)
  Models/               Active Record models (SQLite-backed)
  Services/             Singletons: DBService, PrayerService, etc.
  ViewModels/           MVVM layer (CommunityToolkit.Mvvm)
  Views/                XAML pages + code-behind
  Resources/Styles/     Color tokens and named styles
  Platforms/            Android- and iOS-specific code
PrayerApp.Tests/        xUnit unit tests (links source files from main project)
PrayerApp.UITests/      Appium-based UI tests
docs/                   Plans, research notes, screenshot runbooks
screenshots/            App store screenshots (phone + tablet, light + dark)
```

Architecture, conventions, and known gotchas are documented in [ARCHITECTURE.md](ARCHITECTURE.md).

## Contributing

This started as (and primarily remains) a personal project. Issues and pull requests are welcome, but I make no commitments on response time or scope. If you fork it for your own use, that's exactly what the GPL-3.0 license is for.

## License

[GPL-3.0](LICENSE.txt) © Multithreaded LLC

# run-uitests.ps1 — Build, launch emulator, start Appium, run UI tests
# Usage: .\run-uitests.ps1 [-SkipBuild] [-SkipEmulator] [-SkipAppium]

param(
    [switch]$SkipBuild,
    [switch]$SkipEmulator,
    [switch]$SkipAppium
)

$ErrorActionPreference = "Stop"
$androidSdk = "$env:LOCALAPPDATA\Android\Sdk"
$emulatorExe = "$androidSdk\emulator\emulator.exe"
$avdName = if ($env:ANDROID_AVD) { $env:ANDROID_AVD } else { "pixel_9_-_api_36_0" }

# Step 1: Build APK
if (-not $SkipBuild) {
    Write-Host "`n=== Building Android APK (Release) ===" -ForegroundColor Cyan
    dotnet build PrayerApp/PrayerApp.csproj -f net10.0-android -c Release
    if ($LASTEXITCODE -ne 0) { throw "APK build failed" }
}

# Step 2: Start emulator (if not already running)
if (-not $SkipEmulator) {
    $devices = & adb devices 2>&1
    if ($devices -notmatch "emulator|device") {
        Write-Host "`n=== Starting Android Emulator: $avdName ===" -ForegroundColor Cyan
        Start-Process $emulatorExe -ArgumentList "-avd", $avdName, "-no-snapshot-load" -WindowStyle Hidden
        Write-Host "Waiting for emulator to boot..."
        & adb wait-for-device
        Start-Sleep -Seconds 10  # Extra settle time
    } else {
        Write-Host "`n=== Emulator already running ===" -ForegroundColor Green
    }
}

# Step 3: Start Appium (if not already running)
if (-not $SkipAppium) {
    $appiumRunning = Get-Process -Name "node" -ErrorAction SilentlyContinue | Where-Object {
        $_.MainWindowTitle -match "appium" -or $true  # Heuristic — node processes may be Appium
    }

    try {
        $response = Invoke-WebRequest -Uri "http://127.0.0.1:4723/status" -TimeoutSec 2 -ErrorAction Stop
        Write-Host "`n=== Appium already running ===" -ForegroundColor Green
    } catch {
        Write-Host "`n=== Starting Appium server ===" -ForegroundColor Cyan
        Start-Process appium -ArgumentList "--port", "4723", "--log-level", "warn" -WindowStyle Hidden
        Start-Sleep -Seconds 3
    }
}

# Step 4: Run UI tests
Write-Host "`n=== Running UI Tests ===" -ForegroundColor Cyan
dotnet test PrayerApp.UITests/PrayerApp.UITests.csproj --logger "console;verbosity=detailed"

Write-Host "`n=== Done ===" -ForegroundColor Green

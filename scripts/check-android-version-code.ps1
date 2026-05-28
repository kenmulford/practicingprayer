<#
  Regression guard for the #8 Android version-stamp gap.

  #8 centralized version stamping in Directory.Build.props but registered the
  _StampApplicationVersion hook for iOS only, and the Android SDK pre-sets
  ApplicationVersion=1 (DefaultProperties.targets:168) which defeated the
  "== ''" gate — so every Android build shipped android:versionCode="1".
  The iOS-only smoke (check-version-increments.sh) could not catch this.

  This guard asserts the generated Android manifest carries:
    android:versionCode == resolved ApplicationVersion (git commit count by
                            default, or -Expected for the override paths)
    android:versionName == <ApplicationDisplayVersion> from Directory.Build.props

  Reads the LAST net10.0-android build's merged manifest. Pass -Build to build
  first. Exits non-zero on mismatch so it can gate CI / pre-release.
#>
param(
    [string]$Expected = '',
    [ValidateSet('Debug', 'Release')][string]$Config = 'Debug',
    [switch]$Build
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrEmpty($repo) -or -not (Test-Path (Join-Path $repo '.git'))) {
    Write-Host "FAIL: could not resolve a valid git repo root from `$PSScriptRoot ('$PSScriptRoot'). Run this script as a file (not dot-sourced) from within the repo."; exit 1
}
Set-Location $repo

if (-not $Expected) {
    $shallowCheck = (git rev-parse --is-shallow-repository).Trim()
    if ($shallowCheck -eq 'true') {
        Write-Host "FAIL: shallow clone detected — git rev-list --count would return a partial commit count. Pass -Expected NNN explicitly."; exit 1
    }
    $Expected = (git rev-list --count HEAD).Trim()
}

$propsMatch = Select-String -Path 'Directory.Build.props' `
    -Pattern '<ApplicationDisplayVersion>([^<]+)</ApplicationDisplayVersion>'
if (-not $propsMatch) { Write-Host 'FAIL: <ApplicationDisplayVersion> not found in Directory.Build.props'; exit 1 }
$expectedName = $propsMatch.Matches[0].Groups[1].Value

if ($Build) {
    dotnet build PrayerApp/PrayerApp.csproj -f net10.0-android -c $Config
    if ($LASTEXITCODE -ne 0) { Write-Host "FAIL: build returned $LASTEXITCODE"; exit 1 }
}

$manifest = "PrayerApp/obj/$Config/net10.0-android/android/AndroidManifest.xml"
if (-not (Test-Path $manifest)) {
    Write-Host "FAIL: manifest not found at $manifest -- run a net10.0-android $Config build first, or pass -Build"
    exit 1
}

$raw = Get-Content $manifest -Raw
if ($raw -notmatch 'android:versionCode="([^"]*)"') { Write-Host 'FAIL: no android:versionCode in manifest'; exit 1 }
$actualCode = $Matches[1]
$actualName = if ($raw -match 'android:versionName="([^"]*)"') { $Matches[1] } else { '<none>' }

$ok = $true
if ($actualCode -ne $Expected) { Write-Host "FAIL: versionCode expected '$Expected', got '$actualCode'"; $ok = $false }
else { Write-Host "PASS: versionCode=$actualCode" }
if ($actualName -ne $expectedName) { Write-Host "FAIL: versionName expected '$expectedName', got '$actualName'"; $ok = $false }
else { Write-Host "PASS: versionName=$actualName" }

if (-not $ok) { Write-Host 'ANDROID VERSION CHECK FAILED'; exit 1 }
Write-Host "ALL ANDROID VERSION CHECKS PASS (versionCode=$actualCode, versionName=$actualName)"

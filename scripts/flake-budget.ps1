<#
  T8.10 flake budget for PR 8 / #14 (ContextMenuImportTests on Android).

  Runs the UITest suite N times against the PRE-INSTALLED app, tallies failures
  across all invocations, and reports PASS/REVIEW against the <=10% flake gate.

  Appium MUST be running with the insecure flag (the Spannable test uses
  `mobile: shell`):
      appium --allow-insecure=uiautomator2:adb_shell

  Builds the test assembly once, then runs each iteration with --no-build.

  Usage:
      pwsh -File scripts/flake-budget.ps1
      pwsh -File scripts/flake-budget.ps1 -Runs 20
      pwsh -File scripts/flake-budget.ps1 -Filter "FullyQualifiedName~ContextMenu_RichTextSpannablePayload_StagesPlainText"
#>
[CmdletBinding()]
param(
    [int]$Runs = 50,
    [string]$Filter = "FullyQualifiedName~ContextMenuImportTests",
    [ValidateSet('Debug', 'Release')][string]$Config = "Debug",
    [switch]$SkipBuild
)

$PSNativeCommandUseErrorActionPreference = $false   # a failing run must not abort the loop
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo

$log = Join-Path $env:TEMP "flake-budget-1.4.4.log"
Remove-Item $log -ErrorAction SilentlyContinue

# Appium reachability. Cannot verify the --allow-insecure flag from here; run 1
# will surface it as an adb_shell error if the flag is missing.
try { $code = (Invoke-WebRequest -Uri http://127.0.0.1:4723/status -TimeoutSec 3 -UseBasicParsing).StatusCode }
catch { $code = 0 }
if ($code -ne 200) {
    Write-Host "Appium not reachable on 4723. Start it first (separate terminal):" -ForegroundColor Yellow
    Write-Host "  appium --allow-insecure=uiautomator2:adb_shell"
    return
}

if (-not $SkipBuild) {
    dotnet build PrayerApp.UITests -c $Config
    if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED - aborting" -ForegroundColor Red; return }
}

$sw = [Diagnostics.Stopwatch]::StartNew()
for ($i = 1; $i -le $Runs; $i++) {
    "=== Run $i / $Runs  ($(Get-Date -Format HH:mm:ss)) ===" | Tee-Object -FilePath $log -Append
    dotnet test PrayerApp.UITests --no-build -c $Config `
        --filter $Filter --logger "console;verbosity=normal" 2>&1 |
        Tee-Object -FilePath $log -Append
}
$sw.Stop()

# Tally one summary per invocation. `dotnet test ... --logger "console;..."` routes
# through the classic xUnit VSTest adapter, which prints "Total tests: N" + a
# "Failed: N" line (only when not fully green) - NOT the Microsoft.Testing.Platform
# "Test summary: total: N, failed: N" line. Parse VSTest first, fall back to MTP, so
# the gate is correct regardless of which runner dotnet test selects.
$vsTot = Select-String -Path $log -Pattern '^\s*Total tests:\s+(\d+)'
$mtp   = Select-String -Path $log -Pattern 'Test summary:\s+total:\s+(\d+),\s+failed:\s+(\d+)'
if ($vsTot) {
    $done = $vsTot.Count
    $tot  = ($vsTot | ForEach-Object { [int]$_.Matches[0].Groups[1].Value } | Measure-Object -Sum).Sum
    $fail = [int]((Select-String -Path $log -Pattern '^\s*Failed:\s+(\d+)' |
             ForEach-Object { [int]$_.Matches[0].Groups[1].Value } | Measure-Object -Sum).Sum)
}
elseif ($mtp) {
    $done = $mtp.Count
    $tot  = ($mtp | ForEach-Object { [int]$_.Matches[0].Groups[1].Value } | Measure-Object -Sum).Sum
    $fail = ($mtp | ForEach-Object { [int]$_.Matches[0].Groups[2].Value } | Measure-Object -Sum).Sum
}
else {
    Write-Host "No test summary found (neither VSTest 'Total tests:' nor MTP 'Test summary:') across $Runs runs in $log - every invocation likely crashed before reporting; inspect the log." -ForegroundColor Red
    return
}
$thresh = [math]::Floor($tot * 0.10)

""
"================ FLAKE BUDGET RESULT ================"
"Runs summarized : $done / $Runs   (wall clock $([int]$sw.Elapsed.TotalMinutes) min)"
"Invocations     : $tot    Failures: $fail    Gate: <= $thresh (10%)"
"Log             : $log"
if ($done -lt $Runs) { "WARN: $($Runs - $done) run(s) produced no summary - likely crashed; investigate" }
if ($fail -le $thresh -and $done -eq $Runs) { "VERDICT: PASS -> G8 green" }
else { "VERDICT: REVIEW -> circuit-breaker per handoff S3" }

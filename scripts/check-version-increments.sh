#!/usr/bin/env bash
# Build-system smoke for #8: ApplicationVersion / ApplicationDisplayVersion stamping.
#
# Verifies the END STATE in the packaged app bundle's plists, not an intermediate
# MSBuild log line. The host and extension Info.plist files inside the built
# PrayerApp.app must both carry:
#   - CFBundleVersion           = stamped ApplicationVersion (commit count or override)
#   - CFBundleShortVersionString = ApplicationDisplayVersion from Directory.Build.props
#
# Precedence (highest first) for ApplicationVersion:
#   1. Env var APPLICATION_VERSION_OVERRIDE
#   2. -p:ApplicationVersionOverride=NNN
#   3. git rev-list --count HEAD
#
# This script does NOT mutate git state (no stash, no empty commit). The
# commit-count auto-bump property is guaranteed by `git rev-list --count`
# semantics; this script proves that the value (whatever it is) reaches the
# packaged plists, and that overrides flow end-to-end.
#
# Cost note: iOS build is slow (~30s-3min cold). This is a manual local smoke,
# not a per-commit gate. Run from repo root or scripts/.
set -euo pipefail

cd "$(dirname "$0")/.."

# ----- Inputs -----
PROJ="PrayerApp/PrayerApp.csproj"
TFM="net10.0-ios"
CONFIG="Debug"
# Explicit simulator RID: aligns the host iOS build with the HostShortcuts
# Xcode-managed static library SDK (see build/HostShortcuts.targets). Without
# a RID, dotnet build defaults can split host (simulator) from HostShortcuts
# (device), producing a clang++ link error. Apple Silicon arm64 simulator.
RID="iossimulator-arm64"
BIN_GLOB="PrayerApp/bin/${CONFIG}/${TFM}/${RID}"
APP_BUNDLE_NAME="PrayerApp.app"
EXPECTED_PLIST_COUNT=2   # host + 1 extension; bump if a new extension target ships

# Read display version dynamically from Directory.Build.props so this script
# tracks bumps automatically.
EXPECTED_DISPLAY_VERSION=$(
  grep -oE '<ApplicationDisplayVersion>[^<]+</ApplicationDisplayVersion>' Directory.Build.props \
    | sed -E 's#</?ApplicationDisplayVersion>##g'
)
if [[ -z "${EXPECTED_DISPLAY_VERSION}" ]]; then
  echo "FAIL: could not read <ApplicationDisplayVersion> from Directory.Build.props" >&2
  exit 1
fi

EXPECTED_COMMIT_COUNT=$(git rev-list --count HEAD)

# ----- Helpers -----
fail() { echo "FAIL: $*" >&2; exit 1; }
pass() { echo "PASS: $*"; }

find_plists() {
  # Dynamic enumeration: host Info.plist + every PlugIns/*.appex/Info.plist
  # inside the built .app. Returns absolute paths, one per line.
  local app_root
  app_root=$(find "${BIN_GLOB}" -type d -name "${APP_BUNDLE_NAME}" -print -quit 2>/dev/null || true)
  if [[ -z "${app_root}" ]]; then
    return 1
  fi
  {
    [[ -f "${app_root}/Info.plist" ]] && echo "${app_root}/Info.plist"
    find "${app_root}/PlugIns" -mindepth 2 -maxdepth 2 -name 'Info.plist' 2>/dev/null || true
  }
}

assert_plist_value() {
  # $1=plist path, $2=key, $3=expected value, $4=human label
  local plist="$1" key="$2" expected="$3" label="$4"
  local actual
  actual=$(/usr/libexec/PlistBuddy -c "Print :${key}" "${plist}" 2>/dev/null || echo "<MISSING>")
  if [[ "${actual}" != "${expected}" ]]; then
    fail "${label}: ${plist}  ${key}: expected '${expected}', got '${actual}'"
  fi
  pass "${label}: ${plist##*PrayerApp/bin/}  ${key}=${actual}"
}

verify_bundle() {
  # $1 = expected CFBundleVersion, $2 = expected CFBundleShortVersionString, $3 = label
  local expected_bv="$1" expected_sv="$2" label="$3"
  local plists count
  mapfile -t plists < <(find_plists || true)
  count=${#plists[@]}
  if [[ "${count}" -ne "${EXPECTED_PLIST_COUNT}" ]]; then
    echo "FOUND PLISTS (${count}):"
    printf '  %s\n' "${plists[@]:-<none>}"
    fail "${label}: expected exactly ${EXPECTED_PLIST_COUNT} Info.plist files in app bundle, found ${count}"
  fi
  pass "${label}: found ${count} Info.plist files (host + extensions)"

  local p
  for p in "${plists[@]}"; do
    assert_plist_value "$p" "CFBundleVersion"            "${expected_bv}" "${label}"
    assert_plist_value "$p" "CFBundleShortVersionString" "${expected_sv}" "${label}"
  done

  # Parity check: every plist already matched expected_bv, so by transitivity
  # host == extension. Explicit log for human readers.
  pass "${label}: host == extension parity holds (all ${count} plists matched)"
}

run_build() {
  # $1 = label, remaining args = extra dotnet build switches
  local label="$1"; shift
  echo
  echo "=== ${label} ==="
  echo "Building iOS ${CONFIG} / ${RID} ($*)..."
  # Cold cache acceptable. Suppress noisy package restore output but keep errors.
  if ! dotnet build "${PROJ}" -f "${TFM}" -c "${CONFIG}" -p:RuntimeIdentifier="${RID}" \
      -nologo --verbosity:quiet "$@"; then
    fail "${label}: dotnet build returned non-zero exit"
  fi
}

# ----- Test 1: default path (git commit count + props display version) -----
run_build "Default (git rev-list count)"
verify_bundle "${EXPECTED_COMMIT_COUNT}" "${EXPECTED_DISPLAY_VERSION}" "default"

# ----- Test 2: -p:ApplicationVersionOverride wins over git -----
run_build "Stamping with -p:ApplicationVersionOverride=88888" -p:ApplicationVersionOverride=88888
verify_bundle "88888" "${EXPECTED_DISPLAY_VERSION}" "-p override"

# ----- Test 3: env var beats -p switch -----
echo
echo "=== Env override (APPLICATION_VERSION_OVERRIDE=99999) beats -p:88888 ==="
echo "Building iOS ${CONFIG} / ${RID} (env override + -p:88888)..."
if ! APPLICATION_VERSION_OVERRIDE=99999 dotnet build "${PROJ}" -f "${TFM}" -c "${CONFIG}" \
    -p:RuntimeIdentifier="${RID}" -nologo --verbosity:quiet -p:ApplicationVersionOverride=88888; then
  fail "env override: dotnet build returned non-zero exit"
fi
verify_bundle "99999" "${EXPECTED_DISPLAY_VERSION}" "env override"

echo
echo "=========================================="
echo "ALL SMOKE CHECKS PASS"
echo "  display version: ${EXPECTED_DISPLAY_VERSION}"
echo "  commit count:    ${EXPECTED_COMMIT_COUNT}"
echo "=========================================="

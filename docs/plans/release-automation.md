# Release automation — Android + iOS, fastlane → free CI

Consolidate the manual release process (Android built on Windows + uploaded via Play
web UI; iOS built on a MacBook Air + uploaded via Transporter) onto the Mac, then into
free GitHub Actions CI. fastlane is the through-line: the same lanes run locally now and
in CI later. CI is free because the repo is **public** (standard GitHub-hosted runners,
including macOS, are unlimited at $0 — only `-large`/`-xlarge` labels bill).

## Decisions locked

- **Staged rollout:** Mac-local fastlane first, lift the *same* lanes into CI second.
- **Secrets in GitHub Actions** approved for the CI phase — but see the auth decision in
  Phase 2; "approved" does not mean "downloaded long-lived keys everywhere by default."
- **Submit gate stays manual:** iOS → **TestFlight**; Android → **production track as a draft**
  (`release_status: "draft"`) — fastlane creates the full release + uploads release notes, you
  click "Send for review" in Play Console. Google review still gates going live.
- **Branching:** work on `feature/release-automation` off `dev`, per CONTRIBUTING.md.

## Phase 0 — Foundations (portal/credential work — Ken)

- [ ] Confirm **Play App Signing** enrolled (Play Console → Release → Setup → App Integrity).
      If yes, the local keystore is a *resettable upload key*, not the irreplaceable one.
- [ ] Copy the upload keystore (alias `prayerapp`) from Windows → Mac securely; passwords sent separately.
- [ ] **App Store Connect API key** — create a **Team** key; download the `.p8` (one-time);
      record Issuer ID + Key ID; back up the `.p8` immediately (never re-downloadable).
- [x] **Google Play service account** created (Create-credentials wizard → Application data);
      **JSON key downloaded** and stored outside the repo.
- [ ] Play Console → Users & permissions → invite the service-account email → grant release
      access to the app → save. Wait up to 24–48h to propagate.
- [ ] Verify: `bundle exec fastlane run validate_play_store_json_key json_key:<path>`

## Phase 1 — Mac-local fastlane (scaffolded on this branch)

Done:
- [x] `fastlane/Appfile`, `fastlane/Fastfile` (android + ios `release` lanes: dotnet build → upload).
- [x] `Gemfile` (pins fastlane); `.gitignore` ignores `fastlane/.env`, `*.keystore`, `*.p8`.

Remaining:
- [ ] Install a current Ruby (system Ruby is 2.6.10, Apple's deprecated default — fastlane needs newer).
      `brew install ruby` or rbenv; confirm against fastlane's current minimum.
- [ ] `bundle install`.
- [ ] Create `fastlane/.env` from the template; fill in real paths/passwords/key IDs.
- [ ] Edit `fastlane/metadata/android/en-US/changelogs/default.txt` with this version's notes.
- [ ] `bundle exec fastlane android release` → confirm the draft production release appears in Play Console (then submit it manually).
- [ ] `bundle exec fastlane ios release` → confirm IPA lands in TestFlight
      (distribution cert + provisioning profile must be in the Mac keychain — manual signing,
      matching `PrayerApp.csproj`).

## Phase 2 — GitHub Actions CI (tag-triggered)

- [ ] Add `global.json` pinning the .NET 10 SDK + MAUI workload (repo has none — top CI-flakiness source).
- [ ] Add `.github/workflows/release.yml`: trigger on `v*` tag → Android job (Linux/Windows) +
      iOS job (pin `macos-26` + pin Xcode via `setup-xcode`) → each calls the Phase 1 fastlane lane.
      Keep separate from `ci.yml` (the Unit Tests check) so PR CI stays fast.
- [ ] Tag a throwaway build → watch it ship to internal/TestFlight.

### SECURITY DECISION — CI auth (resolve here, do not default)

Do **not** reflexively paste long-lived keys into secrets. Decide per platform:

- **Android — prefer keyless.** GitHub Actions OIDC → GCP **Workload Identity Federation**
  issues short-lived credentials with no downloaded key stored in GitHub.
  - [ ] **Verify fastlane `supply` accepts WIF / Application Default Credentials** before
        committing to this. If supply still requires a service-account JSON, weigh: (a) use
        `google-github-actions/auth` to materialise a short-lived credential supply can read,
        or (b) fall back to the downloaded JSON as an encrypted secret, scoped to *release only*.
  - The downloaded JSON key from Phase 0 is the **fallback**, not the assumed default.
- **iOS — no keyless option exists.** Apple offers no OIDC federation for the App Store Connect
  API. The `.p8` **must** live as an encrypted GitHub secret. Mitigate honestly:
  - [ ] Scope it to a **Team** key with the least role that still permits upload.
  - [ ] Back up the `.p8` out-of-band (un-redownloadable); plan periodic rotation.
- **iOS signing identity:** adopt fastlane **`match`** (encrypted certs/profiles in a private
  store) so CI and Mac share one identity instead of copying keychains.

## Standing security posture (so it isn't quietly eroded)

- Android *can* be keyless in CI (WIF); iOS *cannot* (.p8 in a secret is unavoidable). Don't
  let the iOS constraint normalise downloading long-lived keys for Android too.
- Secrets to back up the instant they're created (single-download): ASC `.p8`, iOS cert `.p12`.
- Keep all signing material out of the repo (`.gitignore` already blocks `*.keystore`/`*.p8`).
- Scope every credential to release-only in its own console (Play Console role, ASC Team key).

## Open caveats to confirm at implementation time

- Current Ruby vs fastlane's minimum.
- Exact Xcode version required by the pinned .NET 10 SDK (pin both; don't rely on `macos-latest`,
  which is migrating to macOS 26 through July 15 2026).
- `fastlane/.env.example` template blocked by the local sensitive-file hook — pending Ken's call
  (approve the template vs document vars in a README).

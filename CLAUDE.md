# PracticingPrayer — guidance for AI coding agents

This file is committed and public. Machine-local, personal instructions (cross-machine handoffs, vault routing) live in `CLAUDE.local.md`, which is gitignored and loaded automatically alongside this file.

## Branching & merge discipline

The full branch model and contribution process are in [CONTRIBUTING.md](CONTRIBUTING.md) — that is the source of truth. Operational essentials for agents:

- **`master` and `dev` are branch-protected.** You cannot push to them directly. Every change reaches them through a pull request that passes the required `Unit Tests` CI check and is **squash-merged** (one commit per PR).
- **Never edit `dev` or `master` directly.** Before any edit, confirm `git branch --show-current` is not `dev`/`master`; if it is, create a branch first.
- **Branch from the right base:**
  - `feature/*`, `fix/*`, `release/*` → branch from `dev`
  - `hotfix/*` → branch from `master`
- **One concern per branch.** If a new concern surfaces mid-branch, branch separately off the appropriate base.
- **PR flow:** push the branch → `gh pr create --base <dev|master> --fill` → wait for CI green → `gh pr merge --squash --delete-branch`.
- **`release/*` and `hotfix/*` merge twice:** into `master` (then tag the release) **and** back into `dev`, so the fix isn't lost in the next release.
- **`release/*` is optional ceremony:** cut one only when `dev` must keep moving while a build is frozen for stabilization / App Store review; otherwise `dev → master` directly is equivalent.
- **Commits are squashed**, so the PR title/description become the landed commit message — write them release-quality.

## Working in this codebase

- Read the file you're changing plus its direct callers/callees before editing.
- New behavior lands with tests; pure refactors are fine under existing green coverage.
- Run `dotnet test PrayerApp.Tests/PrayerApp.Tests.csproj` before opening a PR.
- Architecture and naming conventions: [ARCHITECTURE.md](ARCHITECTURE.md).

## Solving a GitHub issue (milestone-driver)

This repo is a [milestone-driver](https://github.com/kenmulford/milestone-driver) consumer; its profile is `milestone-driver.json` (repo root). Drive one issue with `/solve-issue <n>`, or a whole milestone in dependency order with `/solve-milestone <name>` (order comes from the milestone description's Wave list).

Per issue the orchestrator — never authoring code itself — reads the issue → finds the root cause or **STOPs and comments** → dispatches the `implementer` subagent (TDD red→green, least-code, citations posted on the issue) → runs `dotnet test PrayerApp.Tests/…` → runs the E2E gate (`run-uitests.ps1`) for UI-touching changes → `/code-review` → opens a PR `--base dev` → auto-merges on the `Unit Tests` check green → closes the issue. Architecture is locked at plan-approval time; one-way-door decisions STOP and ask rather than drift.

**Mechanical gates** (active once the plugin is installed and Claude Code is restarted):
- `force-subagent` — main-thread edits to `PrayerApp/**`, `PrayerApp.Tests/**`, `PrayerApp.UITests/**` are blocked; dispatch the implementer instead (escape: `CLAUDE_HOOK_DISABLE_FORCE_SUBAGENT=1`).
- `tests-green` — source commits run the unit suite; red blocks the commit.
- `no-push` / `no-pr-to-protected` — pushing or opening a PR to `master` is blocked locally; GitHub branch protection is the server-side backstop.

Non-negotiables: MAUI .NET 10 + Community Toolkit; iOS 26.5 / Android API 36.

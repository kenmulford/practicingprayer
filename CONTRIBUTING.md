# Contributing to Practicing Prayer

Thanks for your interest. This is primarily a personal project, but bug reports, feature suggestions, and pull requests are welcome. There are no commitments on response time or scope — fork the project under [GPL-3.0](LICENSE.txt) if you need something I can't move on quickly.

## Filing an issue

Pick the template that fits — these prompt for the details I need to act:

| Template | When to use |
|---|---|
| **Bug report** | Something doesn't work right. Repro steps, device, OS version, app version are required. Screenshots are optional but helpful. |
| **Feature request** | A new capability or improvement. Describe the **problem** you're trying to solve, not just the proposed solution. |
| **Tech debt** | Refactors, test infrastructure, build tooling — internal-quality work. |
| **Needs design** | Idea where the **what** is clear but the **how** has open architectural questions. |

Personal questions and prayer stories are best handled privately by email at [practicingprayerapp@gmail.com](mailto:practicingprayerapp@gmail.com), not in a public issue.

### Privacy when attaching screenshots

If a screenshot shows personal prayer content, names, or notes, blur or crop those before posting. The bug-report template only asks for screenshots — diagnostic logs are not requested through the issue tracker. If a developer needs a log to diagnose your bug, they'll ask you to email it.

## Submitting a pull request

This repo's working branch is **`dev`**. Releases land on `master`.

1. Fork the repo on GitHub.
2. Create a branch from `dev` (not `master`).
3. Make your changes; keep commits focused and reviewable.
4. Run the test suite locally — see [Build and run](README.md#build-and-run) and the test layout below.
5. Open the PR against `dev`. Link the issue with `Closes #<n>` if one exists.

I squash-merge PRs into `dev`, so your commit history within the branch is for review readability — final history is one squashed commit per PR.

### What to test before opening a PR

| Surface | Command |
|---|---|
| Unit tests | `dotnet test PrayerApp.Tests/PrayerApp.Tests.csproj` |
| Build | `dotnet build PrayerApp/PrayerApp.csproj` for both `net10.0-android` and `net10.0-ios` (iOS requires macOS + Xcode) |
| UI tests | `PrayerApp.UITests/` — Appium-based, requires emulator/sim and `appium` running locally. Optional for small PRs; required for changes touching navigation, list virtualization, or share/import flows. |

### Code review expectations

- Architecture and naming conventions are documented in [ARCHITECTURE.md](ARCHITECTURE.md). Read the section relevant to your change before writing.
- New behavior should land with tests. Pure refactors with existing green coverage are fine.
- Don't bundle unrelated changes into one PR. If you find unrelated cleanups while working, file a separate `tech-debt` issue for them.

## Labels

| Label | Meaning |
|---|---|
| `bug`, `enhancement`, `tech-debt` | Issue type. Auto-applied by templates. |
| `needs-design`, `needs-investigation` | Open scope decisions or unproven hypothesis — implementation shouldn't start from this state without further discussion. |
| `needs-repro`, `cannot-reproduce` | Bug witnessed but not reproduced in-house. Eyewitness reports help. |
| `accessibility`, `privacy`, `performance`, `infra` | Topic area. |
| `regression-net`, `false-green-risk`, `test-infrastructure` | Test-quality signal. |
| `ios`, `android`, `tablet` | Platform scope. |
| `good first issue` | Smaller scope, suitable for a first PR. |
| `closed-retroactively` | Created post-fix for `git log` archaeology only. Already closed. |

## License

By contributing, you agree your contributions will be licensed under [GPL-3.0](LICENSE.txt).

# A11Y-4: Remaining Accessibility Sprints

> Sprint 1 complete (2026-04-07). Full audit at `docs/research/a11y-4-audit.md`.

---

## Sprint 2: Container Description cleanup + state communication (needs on-device testing)

**Why careful:** Description on layout containers (Grid/Border) can hide children on iOS VoiceOver. Changes to the BUG-60 card header area need Android verification too. Each fix should be tested with TalkBack/VoiceOver before committing.

| ID | Fix | File | Risk |
|----|-----|------|------|
| H2a | MainPage metric Borders (4): add `IsInAccessibleTree="false"` to child Labels since Border has Description | `MainPage.xaml` | Low — Borders already act as single tap targets |
| H2b | PrayerListPage prayer row Grid: hide children or remove Description | `PrayerListPage.xaml` | Medium — verify row tap still works with VoiceOver |
| H2c | TagsPage/BoxesPage row Grids: hide children or remove Description | `TagsPage.xaml`, `BoxesPage.xaml` | Medium — same concern |
| H2d | TagPickerPage suggestion Grid: verify current behavior | `TagPickerPage.xaml` | Low — single-child Grid |
| H7 | Add accessible answered-state to prayer rows inside expanded cards | `PrayerCardsPage.xaml` + `PrayerCardViewModel.cs` | Low — additive Description on row Grid |
| H8 | Tag filter chips: bind Description to include selected/unselected state | `PrayerCardsPage.xaml`, `PrayerListPage.xaml` + chip VMs | Low — additive binding |

## Sprint 3: Missing announcements (C# only, safe)

| ID | Fix | File |
|----|-----|------|
| H9 | Inject IAccessibilityService into HomeViewModel, add loading/error announcements | `HomeViewModel.cs` |
| M5 | PrayerTime auto-mode: announce start/stop/pause/resume/interval changes | `PrayerTimeViewModel.cs` |
| M6 | Debounce PrayerListViewModel filter announcements (copy pattern from PrayerCardsVM) | `PrayerListViewModel.cs` |
| M11 | Announce multi-select count on each toggle | `PrayerCardsViewModel.cs` |
| M12 | Announce favorite toggle | `PrayerCardViewModel.cs` |
| M13 | Announce section expand/collapse | `PrayerCardsPage.xaml.cs` |
| M14 | Announce "Prayer marked as answered" before navigation | `PrayerRequestDetailViewModel.cs` |

## Sprint 4: Touch targets, headings, structural (needs on-device testing)

| ID | Fix | File | Risk |
|----|-----|------|------|
| H1 | Add accessible multi-select entry point (toolbar button or menu) | `PrayerCardsPage.xaml` + VM | Medium — toolbar layout on small phones |
| H10 | Fix 8 undersized touch targets (see audit doc for specifics) | Multiple files | Medium — layout changes, test on device |
| M1 | Verify/add HeadingLevel on all page titles | 9 XAML files | Low — additive |
| M2 | Add Hints to ~30 toolbar items and buttons | Multiple files | Low — additive, high volume |
| M7 | Fix Android NotifyLayoutChanged (currently iOS-only) | `MauiAccessibilityService.cs` | Low — platform-specific code |
| M8 | Dynamic FAQ hints (expanded/collapsed) | `HelpPage.xaml` + `FaqItemViewModel.cs` | Low |
| M9 | Add HeadingLevel to FAQ questions | `HelpPage.xaml` | Low |

---

## Testing approach

- **Sprint 2:** Each file change → deploy to device → test with TalkBack (Android) or VoiceOver (iOS)
- **Sprint 3:** Unit tests verify Announce() calls via NSubstitute mock
- **Sprint 4:** Touch targets → visual + tap testing on device; headings/hints → screen reader verification

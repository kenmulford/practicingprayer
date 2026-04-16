# A11Y-4: Full Accessibility Audit

> Audit date: 2026-04-07. Three parallel agents audited all XAML files, all ViewModels/code-behind, converters, styles, navigation, contrast, and touch targets.

---

## What's Already Done Well

- `IAccessibilityService.Announce()` used extensively (30+ announcement points)
- Composed accessible descriptions (`AccessibleCardHeader`, `AccessibleSummary`) on key list items
- Decorative elements (dividers, chevrons in HelpPage, picker indicators) correctly hidden
- Global Button/Entry/Editor/CheckBox styles set 44pt minimum touch targets
- Heading levels correctly set in Headline (Level1) and SectionHeading (Level2) styles
- ColorPicker provides human-readable color names and auto-focuses hex entry
- Multi-select mode entrance/exit announced programmatically
- Debounced filter announcements on PrayerCardsPage prevent screen reader spam

---

## HIGH PRIORITY — Blocks screen reader workflows or fails WCAG AA

### H1. Long-press for multi-select has no accessible alternative
**File:** `PrayerCardsPage.xaml` + code-behind
**Problem:** Long-press is the only way to enter multi-select mode. Screen reader users can't perform long-press reliably on either platform.
**Fix:** Add a toolbar item or context action that enters multi-select mode (e.g., "Select" toolbar button).

### H2. `SemanticProperties.Description` on layout containers hides children on iOS
**Files:** `MainPage.xaml` (4 metric Borders), `PrayerCardsPage.xaml` (section header Grid), `PrayerListPage.xaml` (prayer row Grid), `TagsPage.xaml` (tag row Grid), `BoxesPage.xaml` (box row Grid), `TagPickerPage.xaml` (suggestion Grid)
**Problem:** Setting Description on a Grid/Border makes iOS VoiceOver treat it as a single element — all children become unreachable.
**Fix:** For tappable rows that should be one focusable unit: keep Description on the container but add `AutomationProperties.IsInAccessibleTree="false"` on all child elements. For containers where children should be individually focusable: remove Description from container.

### H3. `SemanticProperties.Description` on Label causes double-read
**File:** `PrayerCardsPage.xaml` line 243
**Problem:** Card title Label has both `Text="{Binding Title}"` and `Description="{Binding AccessibleCardHeader}"`. VoiceOver reads Description then Text.
**Fix:** Move Description to parent Grid (the tap target), remove from Label.

### H4. `SemanticProperties.Description` on Switch breaks TalkBack
**File:** `PrayerDetailPage.xaml` lines 165, 243
**Problem:** Description on Switch overrides TalkBack's built-in toggle announcements ("on"/"off").
**Fix:** Change to `SemanticProperties.Hint` instead. Let the adjacent Label serve as the accessible name.

### H5. Gray400 on PageLight fails WCAG AA (3.1:1)
**File:** `Colors.xaml` — `Gray400=#919191` on `PageLight=#FAF8F3`
**Problem:** Used as answered-prayer muted text color (via `BoolToMutedColorConverter`) in light mode. Contrast ratio 3.1:1, requires 4.5:1.
**Fix:** Darken to ~`#717171` or darker to hit 4.5:1.

### H6. TabUnselectedDark on PageDark fails WCAG AA (2.4:1)
**File:** `Colors.xaml` — `TabUnselectedDark=#4a4d46` on `PageDark=#0d0e0c`
**Problem:** Used for empty-state text ("No active cards", "No prayers yet") on dark mode MainPage. Contrast 2.4:1.
**Fix:** Lighten to ~`#707368` or use `TextMutedDark` instead.

### H7. Prayer rows inside expanded cards have no accessible answered-state
**File:** `PrayerCardsPage.xaml` lines 437-479
**Problem:** Prayer request rows within cards use only muted color + strikethrough to show answered state. No `SemanticProperties.Description` on the row. Screen readers read the title text but don't communicate answered status.
**Fix:** Add a composed Description on the prayer row Grid (like PrayerListPage's `AccessibleSummary`).

### H8. Tag filter chips don't communicate selected/unselected state
**Files:** `PrayerCardsPage.xaml` lines 44-78, `PrayerListPage.xaml` lines 100-133
**Problem:** Filter chips have `Hint="Double tap to toggle filter"` but no Description that includes current state. Screen reader users can't tell which filters are active.
**Fix:** Bind Description to a composed string: `"{Binding Name}, {Binding SelectedStateLabel}"` where SelectedStateLabel is "selected"/"not selected".

### H9. HomeViewModel has no accessibility instrumentation
**File:** `ViewModels/HomeViewModel.cs`
**Problem:** The most-visited screen has no `IAccessibilityService` injection, no loading announcements, no error announcements. Silent to screen readers during load.
**Fix:** Inject `IAccessibilityService`, add loading/loaded/error announcements matching other ViewModels.

### H10. Touch targets below 44x44pt minimum
| Element | File | Size | Fix |
|---------|------|------|-----|
| Remove tag "x" button | `TagPickerPage.xaml` | 24x24 | Increase to 44x44 |
| Dismiss banner "x" button | `PrayerCardsPage.xaml` | 32x32 | Increase to 44x44 |
| Dismiss tip "x" button | `QuickAddPage.xaml` | 32x32 | Increase to 44x44 |
| Add tag "+" button | `PrayerDetailPage.xaml` | 28x28 | Increase to 44x44 |
| Tag filter chips | `PrayerCardsPage.xaml`, `PrayerListPage.xaml` | ~26pt tall | Add MinimumHeightRequest="36" |
| Action chips | `Styles.xaml` ActionChip | ~30pt tall | Add MinimumHeightRequest="44" |
| Pause button | `PrayerTimePage.xaml` | Padding="4,0" | Add MinimumHeightRequest="44" |
| Cycle interval button | `PrayerTimePage.xaml` | Padding="4,0" | Add MinimumHeightRequest="44" |

---

## MEDIUM PRIORITY — Degrades experience

### M1. Missing HeadingLevel on page titles
**Files:** `MainPage.xaml`, `PrayerCardsPage.xaml`, `PrayerListPage.xaml`, `TagsPage.xaml`, `BoxesPage.xaml`, `HelpPage.xaml`, `SettingsHubPage.xaml`, `TagPickerPage.xaml`, `RestoreProgressPage.xaml`
**Problem:** Page/section titles use Headline or SectionHeading style (which do set HeadingLevel), but some pages override with custom Labels that lack it.
**Fix:** Verify every page title Label uses Headline/SectionHeading style, or add HeadingLevel directly.

### M2. Missing Hints on toolbar items and buttons (~30 elements)
**Files:** Across all pages
**Problem:** ToolbarItems ("Add Card", "Save", "Add") and standalone buttons ("Cancel", "Start", "Back Up Now", "Restore", "Privacy Policy", etc.) lack `SemanticProperties.Hint`.
**Fix:** Add Hint to all ToolbarItems and buttons where the Text alone is ambiguous. Low effort, high volume.

### M3. Action chip icon Images not hidden from tree
**Files:** `PrayerCardsPage.xaml` (4 chips), `TagsPage.xaml` (2 chips), `BoxesPage.xaml` (2 chips)
**Problem:** Icon Images inside action chips are in the accessibility tree as unlabeled images. Parent Border has Description.
**Fix:** Add `AutomationProperties.IsInAccessibleTree="false"` to each icon Image.

### M4. BoolToTriangle Label in section headers not hidden
**File:** `PrayerCardsPage.xaml` line 153
**Problem:** The triangle arrow Label reads as a raw Unicode character ("Black Down-Pointing Triangle").
**Fix:** Add `AutomationProperties.IsInAccessibleTree="false"`.

### M5. Auto-mode in PrayerTime is mostly silent
**File:** `PrayerTimeViewModel.cs`
**Problem:** Start/stop auto-mode, pause/resume, interval cycle — none are announced. Screen reader users can't use auto-mode effectively.
**Fix:** Add `Announce()` calls: "Auto-advance started", "Paused", "Resumed", "Interval set to {label}".

### M6. PrayerListViewModel filter announcements not debounced
**File:** `PrayerListViewModel.cs`
**Problem:** Unlike PrayerCardsViewModel (which debounces at 400ms), PrayerListViewModel announces on every keystroke during search. Creates screen reader spam.
**Fix:** Add the same debounce pattern used in PrayerCardsViewModel.

### M7. NotifyLayoutChanged() is no-op on Android
**File:** `MauiAccessibilityService.cs`
**Problem:** Only posts iOS notification. Android TalkBack misses all layout change notifications.
**Fix:** Add Android equivalent using `AccessibilityEvent.TYPE_WINDOW_CONTENT_CHANGED`.

### M8. FAQ accordion hints are static
**File:** `HelpPage.xaml`
**Problem:** Hint always says "Double tap to expand" even when already expanded.
**Fix:** Bind Hint to a dynamic property: "Double tap to collapse" when expanded.

### M9. FAQ questions lack HeadingLevel
**File:** `HelpPage.xaml`
**Problem:** FAQ question labels are bold navigational elements but have no HeadingLevel.
**Fix:** Add `SemanticProperties.HeadingLevel="Level2"`.

### M10. Primary color on CardLight fails AA for small text
**File:** `Colors.xaml` — `Primary=#6B7D5A` on `CardLight=#FFFFFF`
**Problem:** Contrast 4.3:1. Used at 10pt for "View in Prayers" hint text on MainPage. Requires 4.5:1 for normal text.
**Fix:** Darken slightly or increase font size to 14pt bold (large text threshold).

### M11. Multi-select count not announced per toggle
**File:** `PrayerCardsViewModel.cs`
**Problem:** `ToggleCardSelection()` updates count visually but doesn't announce it.
**Fix:** Add `_accessibilityService.Announce(SelectedCountText)` after toggle.

### M12. Favorite toggle not announced
**File:** `PrayerCardViewModel.cs`
**Problem:** Toggling favorite is silent to screen readers.
**Fix:** Add `Announce(IsFavorite ? "Marked as favorite" : "Removed from favorites")`.

### M13. Section expand/collapse not announced
**File:** `BoxSectionViewModel.cs` + `PrayerCardsPage.xaml.cs`
**Problem:** Box section expand/collapse has no screen reader announcement (card accordion does, but sections don't).
**Fix:** Add announce in `OnSectionHeaderTapped`: `Announce(section.IsExpanded ? $"Expanded {section.Name}" : $"Collapsed {section.Name}")`.

### M14. MarkAnswered not announced from detail view
**File:** `PrayerRequestDetailViewModel.cs`
**Problem:** Marking a prayer as answered navigates back silently.
**Fix:** Add `Announce("Prayer marked as answered")` before navigation.

---

## LOW PRIORITY — Polish

### L1. Entry Placeholder + Hint conflicts on Android
**Files:** `QuickAddPage.xaml`, `PrayerDetailPage.xaml`, `TagPickerPage.xaml`, `ColorPickerPopup.xaml`
**Problem:** Both properties map to the same Android attribute. Low severity — MAUI usually handles it.
**Action:** Keep Hints that add value beyond Placeholder. Remove redundant ones.

### L2. Code-behind uses SemanticScreenReader directly
**Files:** `PrayerCardsPage.xaml.cs`
**Problem:** Bypasses `IAccessibilityService` abstraction, not testable.
**Action:** Route through `IAccessibilityService.Announce()` instead.

### L3. Color swatches described by hex code
**File:** `TagDetailPage.xaml`
**Problem:** `SemanticProperties.Description="{Binding LightHex}"` reads "#B84040" to screen readers.
**Action:** Map hex to human-readable names (ColorPicker already has `GetApproximateColorName`).

### L4. SubHeadline style missing HeadingLevel
**File:** `Styles.xaml`
**Action:** Add `SemanticProperties.HeadingLevel="Level2"`.

### L5. Tag/box selection for actions not announced
**Files:** `TagsViewModel.cs`, `BoxesViewModel.cs`
**Action:** Add selection state announce.

### L6. Scope picker loading/selection not announced
**Files:** `PrayerTimeScopeViewModel.cs`, `PrayerTimeBoxScopeViewModel.cs`
**Action:** Add loading and selection announcements.

### L7. Settings changes not announced
**File:** `AppSettingsPage.xaml.cs`
**Action:** Platform switches provide implicit feedback; custom changes (overdue threshold) could use announce.

### L8. Onboarding banner appearance not announced
**File:** `OnboardingBanner.xaml.cs`
**Action:** Announce headline text when banner becomes visible.

### L9. TagGrayDark chip fails AA contrast (3.6:1)
**File:** `Colors.xaml` — White text on `TagGrayDark=#848484`
**Action:** Darken TagGrayDark or use dark text.

### L10. FormLabel has no LabeledBy relationship
**File:** `Styles.xaml`
**Action:** MAUI doesn't support `LabeledBy` well cross-platform. The label-above-field layout is adequate — screen readers read them sequentially.

---

## Recommended Fix Order

### Sprint 1: Critical fixes (1 session)
1. H5 + H6: Fix contrast failures (Colors.xaml — 2 color changes)
2. H4: Switch Description → Hint (PrayerDetailPage — 2 lines)
3. H3: Move Description off Label to parent (PrayerCardsPage — small refactor)
4. M3 + M4: Hide icon images and triangle from tree (~10 `IsInAccessibleTree` additions)
5. H10: Fix touch targets (8 elements across 5 files)

### Sprint 2: Container Description cleanup (1 session)
6. H2: Fix Description-on-container across 6 files (add `IsInAccessibleTree="false"` to children)
7. H7: Add accessible answered-state to prayer rows in PrayerCardsPage
8. H8: Add selected state to tag filter chips

### Sprint 3: Missing announcements (1 session)
9. H9: Add IAccessibilityService to HomeViewModel
10. M5: PrayerTime auto-mode announcements
11. M6: Debounce PrayerListViewModel filter announcements
12. M11-M14: Toggle/state change announcements

### Sprint 4: Structure + polish (1 session)
13. H1: Add accessible multi-select entry point (toolbar button)
14. M1 + M9: Fix missing HeadingLevels
15. M2: Add Hints to toolbar items and buttons
16. M7: Fix Android NotifyLayoutChanged
17. M8: Dynamic FAQ hints

### Estimated total: 4 sessions

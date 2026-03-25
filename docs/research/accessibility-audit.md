# Accessibility Audit Report

**Date:** 2026-03-24
**Scope:** All XAML views, code-behind files, Styles.xaml, Colors.xaml, AppShell.xaml
**Standard:** WCAG 2.1 AA, platform screen reader compatibility (TalkBack, VoiceOver, Narrator)

---

## Summary

The app has a partial accessibility foundation. SwipeItem actions across the app have `SemanticProperties.Description` set correctly, and some form fields use `SemanticProperties.Hint`. However, there are significant gaps: no `SemanticScreenReader.Announce` calls anywhere in C# code, zero `AutomationId` values for UI testing, no `TabIndex` ordering on any page, missing heading levels on most pages, many tap-gesture-based interactive elements are invisible to screen readers, and several color contrast ratios fall below WCAG AA thresholds.

**Counts:**
- SemanticProperties.Description: 12 usages (all on SwipeItems, Switches, CheckBoxes)
- SemanticProperties.Hint: 8 usages (form fields and buttons)
- SemanticProperties.HeadingLevel: 2 usages (PrayerListPage, TagsPage only)
- SemanticScreenReader.Announce: 0 usages
- SetSemanticFocus: 0 usages
- AutomationId: 0 usages
- TabIndex: 0 usages
- AutomationProperties.IsInAccessibleTree: 0 usages (decorative elements not hidden)

---

## Critical Issues (Must Fix)

### C1. TapGestureRecognizer-based controls are invisible to screen readers

**Affected:** MainPage (overdue list items, overdue header chevron), PrayerCardsPage (card header tap-to-expand, "+ Add prayer" row), PrayerListPage (prayer row taps, tag filter chips), PrayerDetailPage (tag remove "x" label), TagsPage (tag row tap), TagDetailPage (color swatches, add-custom-color button), PrayerTimePage (pause/resume label, countdown cycle label), OnboardingBanner (skip tour label), OnboardingWelcomePopup (skip tour label), Settings (Privacy Policy label).

**Problem:** `TapGestureRecognizer` on `Label` or `Grid` elements creates tap targets that screen readers cannot discover or activate. A VoiceOver/TalkBack user has no way to tap these elements because they are not announced as interactive controls.

**Fix:** Replace tap-gesture Labels with `Button` (styled as needed) or `ImageButton`, or add `SemanticProperties.Description` and `SemanticProperties.Hint="Double tap to activate"` to the tappable element. For layout containers with gesture recognizers, consider wrapping content in a `Button` or using accessibility grouping.

### C2. No dynamic content announcements

**Affected:** Every page with state changes.

**Problem:** Zero calls to `SemanticScreenReader.Announce()` or `SetSemanticFocus()` exist in any code-behind or ViewModel. Key scenarios that need announcements:
- Prayer saved / deleted confirmation
- Filter changed on PrayerListPage (Active/Answered/All toggle results)
- Prayer Time session progress ("Prayer 3 of 10")
- Prayer Time session completed
- Backup/restore status changes
- Onboarding step transitions
- Overdue list loaded on MainPage
- Tag saved/deleted confirmation
- Validation errors on any form

**Fix:** Add `SemanticScreenReader.Announce()` calls in ViewModels or code-behind for all transient status feedback. Use `SetSemanticFocus()` after navigation or when displaying error messages.

### C3. Color contrast failures (WCAG AA)

The following color combinations fail the WCAG AA 4.5:1 contrast ratio for normal text (or 3:1 for large text/UI components):

| Usage | Foreground | Background | Ratio | Required | Verdict |
|-------|-----------|------------|-------|----------|---------|
| MutedText (light) | Gray400 `#919191` | White `#FFFFFF` | ~3.0:1 | 4.5:1 | **FAIL** |
| MutedText (dark) | Gray300 `#ACACAC` | OffBlack `#1F1F1F` | ~4.7:1 | 4.5:1 | PASS (barely) |
| SectionDescription (light) | Gray500 `#6E6E6E` | Secondary `#E8EDE3` | ~3.4:1 | 4.5:1 | **FAIL** |
| FormLabel (light) | Tertiary `#3F4A34` | Secondary `#E8EDE3` | ~4.5:1 | 4.5:1 | Borderline |
| SuccessBadge | SuccessGreen `#4CAF50` | White `#FFFFFF` | ~3.0:1 | 4.5:1 | **FAIL** |
| SuccessBadge | SuccessGreen `#4CAF50` | Secondary `#E8EDE3` | ~2.8:1 | 4.5:1 | **FAIL** |
| OnboardingBanner sub-text | Gray200 `#C8C8C8` | Tertiary `#3F4A34` | ~4.6:1 | 4.5:1 | Borderline PASS |
| OnboardingBanner skip link | Gray300 `#ACACAC` | Tertiary `#3F4A34` | ~3.5:1 | 4.5:1 | **FAIL** |
| WelcomePopup sub-text | Gray500 `#6E6E6E` | Secondary `#E8EDE3` | ~3.4:1 | 4.5:1 | **FAIL** |
| CompletePopup sub-text | Gray500 `#6E6E6E` | Secondary `#E8EDE3` | ~3.4:1 | 4.5:1 | **FAIL** |
| Tag chip text | White `#FFFFFF` | TagGray `#505050` | ~4.2:1 | 4.5:1 | **FAIL** |
| TagsPage chevron (light) | Gray300 `#ACACAC` | Secondary `#E8EDE3` | ~1.9:1 | 3:1 UI | **FAIL** |
| PrayerListPage empty (light) | Gray400 `#919191` (via MutedText) | White | ~3.0:1 | 4.5:1 | **FAIL** |
| TagsPage empty state | Gray400/Gray500 | White/OffBlack | ~3.0:1 / ~2.7:1 | 4.5:1 | **FAIL** |

### C4. ColorPickerPopup is entirely inaccessible

**Affected:** `ColorPickerPopup.xaml`

**Problem:** The GraphicsView-based saturation/value picker and hue slider have zero accessibility support. `GraphicsView` does not expose its content to the accessibility tree. A screen reader user cannot:
- Perceive what the color picker is
- Adjust hue, saturation, or value
- Know what color is currently selected

The hex Entry is the only accessible element. The entire popup lacks any heading or descriptive text.

**Fix:** Add `SemanticProperties.Description` to both GraphicsViews (e.g., "Color saturation and brightness picker", "Hue slider"). Consider adding a readable label showing the current color name or hex value that updates in real-time. For full accessibility, consider an alternative color selection method (e.g., a list of named colors) alongside the visual picker.

---

## Important Issues (Should Fix)

### I1. Missing HeadingLevel on most page titles

**Affected:** MainPage, PrayerCardPage, PrayerDetailPage, QuickAddPage, PrayerTimePage, PrayerTimeScopePage, Settings, TagDetailPage, RestoreProgressPage, all onboarding popups.

**Present on:** PrayerListPage ("Prayer Requests" Level1), TagsPage ("Tags" Level1).

**Problem:** Screen reader users navigate by headings. Without `HeadingLevel` markup, there is no heading-based navigation structure. Section headings in Settings ("Notifications", "Prayer Reminders", "Backup & Restore", "Diagnostics") use `SectionHeading` style but lack `HeadingLevel`.

**Fix:** Add `SemanticProperties.HeadingLevel="Level1"` to every page title. Add `Level2` to section headings within pages (e.g., Settings card titles, PrayerDetailPage mode labels). The `SectionHeading` style in Styles.xaml could include a HeadingLevel setter, but since SemanticProperties are attached properties, they must be set per-element.

### I2. Swipe actions have no discoverability hint

**Affected:** PrayerCardsPage (card-level and prayer-level swipes), TagsPage (tag swipes).

**Problem:** While SwipeItem elements have `SemanticProperties.Description`, there is no indication to screen reader users that swipe gestures are available on list items. Sighted users discover swipe by trying; screen reader users have no such affordance.

**Fix:** Add `SemanticProperties.Hint` on the SwipeView or its content element (e.g., "Swipe left to delete, swipe right to edit and favorite").

### I3. Icon-only and symbol-only elements lack descriptions

**Affected:**
- PrayerCardsPage: Favorite star "★" label (line 108) — decorative when visible but conveys meaning ("this card is favorited")
- PrayerCardsPage: SwipeItem with `IconImageSource="trash_can_solid_full.png"` — has Description, good
- MainPage: Chevron "›" label (line 51) — indicates tappable, no description
- TagsPage: Chevron "›" label (line 101) — decorative nav hint, no description
- PrayerTimePage: Arrow buttons "←" "→" — text-only buttons, screen readers will read the arrow characters but the meaning is unclear
- PrayerDetailPage: Tag remove "×" label (line 216) — interactive but unlabeled

**Fix:** Add `SemanticProperties.Description` (e.g., "Favorited" on the star, "Navigate to overdue prayers" on the chevron, "Previous prayer" / "Next prayer" on arrows, "Remove tag" on ×). Hide purely decorative icons with `AutomationProperties.IsInAccessibleTree="false"`.

### I4. No TabIndex ordering defined anywhere

**Affected:** All pages.

**Problem:** Without explicit `TabIndex` values, the focus order follows the visual layout order, which may not match the logical interaction order. This is especially problematic on:
- PrayerDetailPage: Complex form with view/edit modes
- PrayerTimePage: Non-linear button layout (I'm done, ←, →, Auto)
- Settings: Multiple cards with interrelated controls

**Fix:** Define `TabIndex` on all interactive controls on complex pages. Priority pages: PrayerDetailPage (edit mode), PrayerTimePage, Settings.

### I5. Decorative elements not hidden from accessibility tree

**Affected:**
- BoxView dividers on every page (DividerLine style) — screen readers will encounter these as empty elements
- Gradient fade BoxView on PrayerTimePage (line 116-125)
- Color dot BoxView on TagsPage (line 69-75)
- Selected ring Ellipse on TagDetailPage (line 60-69)
- Badge count Border on PrayerCardsPage (line 86-105) — the number inside is meaningful but the container Border is noise

**Fix:** Add `AutomationProperties.IsInAccessibleTree="false"` to decorative BoxViews, gradient overlays, and purely visual indicators. The DividerLine style in Styles.xaml could include this setter.

### I6. Switch controls lack association with their labels

**Affected:**
- Settings: "Allow Notifications" label + Switch
- PrayerDetailPage: "Reminders" label + Switch, "Answered" label + Switch

**Problem:** The label and switch are adjacent siblings but not programmatically associated. A screen reader may read the switch's `SemanticProperties.Description` (where present) but the association with the adjacent label is not guaranteed across platforms.

**Fix:** The Description on the switch should include the label context (already done for Settings notification switch and PrayerDetailPage switches — good). Verify on device that TalkBack/VoiceOver reads the full context.

### I7. PrayerTimePage pause/resume button is a Label, not a Button

**Affected:** PrayerTimePage line 25-33.

**Problem:** The pause/resume control is a `Label` with a `TapGestureRecognizer`. Screen readers will announce it as static text, not an interactive control. Users will not know they can activate it.

**Fix:** Replace with a `Button` styled to look like the current label, or add `SemanticProperties.Description` and `SemanticProperties.Hint="Double tap to toggle pause"`.

---

## Nice-to-Have Improvements

### N1. Add AutomationId to key elements for UI test automation

**Affected:** All pages — zero `AutomationId` values exist.

**Recommendation:** Add `AutomationId` to key interactive elements to support future UI test automation:
- `AutomationId="BtnQuickAdd"`, `AutomationId="BtnPrayerTime"` on MainPage
- `AutomationId="TitleEntry"` on form Entry fields
- `AutomationId="BtnSave"`, `AutomationId="BtnDelete"` on action buttons
- `AutomationId="FilterActive"`, `AutomationId="FilterAnswered"`, `AutomationId="FilterAll"` on PrayerListPage
- `AutomationId="SearchBar"` on PrayerListPage

### N2. Add screen reader live region behavior for overdue count

**Affected:** MainPage overdue card.

**Recommendation:** When the overdue count changes (e.g., returning to the home tab after marking a prayer as prayed), announce the new count via `SemanticScreenReader.Announce`.

### N3. CarouselView accessibility on PrayerTimePage

**Affected:** PrayerTimePage CarouselView.

**Recommendation:** CarouselView swipe gestures may conflict with screen reader swipe navigation. Consider providing alternative navigation (the ← → buttons help, but ensure they are prominently described). Announce the current prayer when it changes: "Prayer 3 of 10: [title]".

### N4. Activity indicators should announce loading state

**Affected:** PrayerCardsPage, PrayerListPage, TagsPage, PrayerTimePage, RestoreProgressPage.

**Recommendation:** When `ActivityIndicator.IsRunning` becomes true, announce "Loading" via screen reader. When loading completes, announce "Content loaded" or the resulting state.

### N5. Popup dismissal accessibility

**Affected:** OnboardingWelcomePopup, OnboardingCompletePopup, ColorPickerPopup.

**Recommendation:** Ensure focus is trapped within popups when they appear (MAUI Toolkit popups may handle this). When a popup opens, announce its presence or move focus to the first element. When it closes, return focus to the triggering element.

### N6. OnboardingBanner text uses relatively low contrast skip link

**Affected:** OnboardingBanner.xaml SkipLabel.

**Recommendation:** Gray300 `#ACACAC` on Tertiary `#3F4A34` is approximately 3.5:1. While the skip link is secondary, bumping to Gray200 `#C8C8C8` (already used for SubLabel) would improve readability.

### N7. Touch target sizes on tag chips and prayer list rows

**Affected:** PrayerListPage tag filter chips (Padding="10,4"), PrayerDetailPage tag chips (Padding="6,4" or "8,4"), TagDetailPage color swatches (36px circle in 48px grid).

**Recommendation:** Tag filter chips with `Padding="10,4"` may result in vertical height below 44dp depending on font size. Verify rendered height meets 44x44 minimum. Color swatches at 48x48 grid are fine; the 36px visual circle inside is adequate since the tap target is the full 48px grid.

### N8. RestoreProgressPage has no accessible status updates

**Affected:** RestoreProgressPage.

**Recommendation:** The page shows a spinner and static text. If restore progress is available, announce percentage or completion status. At minimum, announce when restore completes.

---

## Per-Page Breakdown

### MainPage.xaml (Home tab)
- [x] SemanticProperties.Hint on Quick Add and Prayer Time buttons
- [ ] No HeadingLevel on any element (page has no visible heading label)
- [ ] Overdue card header tap area (Grid with TapGestureRecognizer) not accessible
- [ ] Suggested prayer rows (TapGestureRecognizer on Grid) not accessible
- [ ] Chevron "›" has no Description and should be hidden or described
- [ ] No dynamic announcements for overdue count changes
- [ ] No AutomationId values

### PrayerCardsPage.xaml (Cards tab)
- [x] SwipeItem Description on all swipe actions (favorite, edit, delete)
- [ ] No HeadingLevel (page title comes from Shell, no in-page heading)
- [ ] Card header tap-to-expand (TapGestureRecognizer on Grid) not accessible
- [ ] "+ Add prayer" row (TapGestureRecognizer on Grid) not accessible
- [ ] Favorite star "★" label lacks Description
- [ ] Badge count Border not labeled for screen readers
- [ ] No swipe discoverability hint on list items
- [ ] No announcement when card expands/collapses
- [ ] No AutomationId values

### PrayerCardPage.xaml (Card edit)
- [x] SemanticProperties.Hint on Title Entry
- [ ] No HeadingLevel on page (title "Prayer Card" in nav bar only)
- [ ] No AutomationId values

### PrayerListPage.xaml (Prayers tab)
- [x] HeadingLevel="Level1" on "Prayer Requests" heading
- [ ] Status filter buttons (Active/Answered/All) lack Description for selected state
- [ ] Tag filter chips (TapGestureRecognizer on Border) not accessible as toggle buttons
- [ ] Prayer row taps (TapGestureRecognizer on Grid) not accessible
- [ ] No announcement when filter changes or search results update
- [ ] No AutomationId values

### PrayerDetailPage.xaml (Prayer view/edit)
- [x] SemanticProperties.Hint on Picker, Entry, Editor in edit mode
- [x] SemanticProperties.Description on Reminders Switch and Answered Switch
- [x] SemanticProperties.Hint on Frequency Picker
- [ ] No HeadingLevel on prayer title (view mode) or section labels
- [ ] Tag remove "×" label with TapGestureRecognizer is not accessible
- [ ] Tag suggestion CollectionView selection may not announce correctly
- [ ] Day of Week and Day of Month Pickers lack Hint
- [ ] TimePicker lacks Description or Hint
- [ ] No announcement on save/delete confirmation
- [ ] No AutomationId values

### TagsPage.xaml (Tags tab)
- [x] HeadingLevel="Level1" on "Tags" heading
- [x] SwipeItem Description on edit and delete
- [ ] Tag row tap (TapGestureRecognizer on Grid) not accessible
- [ ] Color dot BoxView is decorative but not hidden from accessibility tree
- [ ] Chevron "›" is decorative but not hidden
- [ ] System badge not announced in context with tag name
- [ ] No AutomationId values

### TagDetailPage.xaml (Tag edit)
- [ ] No HeadingLevel on page
- [ ] Color swatches (TapGestureRecognizer on Grid) not accessible — no Description
- [ ] Add-custom-color button (TapGestureRecognizer on Grid) not accessible — no Description
- [ ] Selected ring visual state not communicated to screen readers
- [ ] No announcement on save confirmation
- [ ] No AutomationId values

### ColorPickerPopup.xaml
- [ ] GraphicsView elements completely inaccessible to screen readers
- [ ] No heading or title label for the popup
- [ ] Hex Entry lacks Hint or Description
- [ ] Preview BoxView color not announced
- [ ] No AutomationId values

### QuickAddPage.xaml (Quick add modal)
- [x] SemanticProperties.Hint on card Picker
- [ ] No HeadingLevel on page
- [ ] Title Entry lacks Hint
- [ ] No announcement on successful add
- [ ] No AutomationId values

### PrayerTimePage.xaml (Prayer time)
- [ ] No HeadingLevel on any element
- [ ] Pause/resume label with TapGestureRecognizer not accessible
- [ ] Countdown/interval label with TapGestureRecognizer not accessible
- [ ] Arrow buttons "←" "→" lack descriptive text for screen readers
- [ ] "I'm done" and "Auto" buttons lack Hint
- [ ] CarouselView may conflict with screen reader swipe gestures
- [ ] No announcement on prayer card transitions
- [ ] No announcement on session completion
- [ ] Gradient fade BoxView should be hidden from accessibility tree
- [ ] No AutomationId values

### PrayerTimeScopePage.xaml (Scope selector)
- [x] SemanticProperties.Description on CheckBox (bound to tag name)
- [ ] No HeadingLevel on "Filter by Tags" label
- [ ] No AutomationId values

### Settings.xaml
- [x] SemanticProperties.Description on notification Switch and TimePicker
- [ ] No HeadingLevel on section headings ("Notifications", "Prayer Reminders", "Backup & Restore", "Diagnostics")
- [ ] Privacy Policy label (TapGestureRecognizer) not accessible as a link
- [ ] Overdue threshold Entry lacks Hint
- [ ] No announcement on backup/restore completion
- [ ] No AutomationId values

### OnboardingBanner.xaml
- [ ] Skip tour label (TapGestureRecognizer) not accessible as a button
- [ ] Banner content not announced when it appears
- [ ] No AutomationId values

### OnboardingWelcomePopup.xaml
- [ ] No HeadingLevel on "Welcome to Practicing Prayer"
- [ ] Skip tour label (TapGestureRecognizer) not accessible as a button
- [ ] Sub-text contrast is borderline (Gray500 on Secondary)
- [ ] No AutomationId values

### OnboardingCompletePopup.xaml
- [ ] No HeadingLevel on "You're all set!"
- [ ] Sub-text contrast is borderline (Gray500 on Secondary)
- [ ] No AutomationId values

### RestoreProgressPage.xaml
- [ ] No HeadingLevel on "Restoring your data..."
- [ ] No dynamic progress announcements
- [ ] ActivityIndicator not described for screen readers
- [ ] No AutomationId values

### AppShell.xaml
- [x] Tab items have Title and Icon — screen readers will read tab names
- [ ] Tab icons lack explicit accessibility descriptions (platform may use Title as fallback)

### Styles.xaml
- [x] MinimumHeightRequest=44 and MinimumWidthRequest=44 on Button, CheckBox, DatePicker, Editor, Entry, ImageButton, Picker, RadioButton, SearchBar, TimePicker — good touch target baseline
- [ ] DividerLine style should include `AutomationProperties.IsInAccessibleTree="false"` to hide decorative dividers from screen readers
- [ ] No accessibility-related setters in any named style (e.g., HeadingLevel on SectionHeading)

### Colors.xaml
- See contrast analysis in Critical Issue C3 above
- Tag color palette designed for White text on colored backgrounds — most pass except TagGray
- Light/dark mode color pairs are defined but some muted/secondary text colors are too low contrast in light mode

---

## Priority Remediation Order

1. **C3 — Color contrast failures** — affects all users with low vision; fix MutedText, SuccessBadge, SectionDescription colors
2. **C1 — TapGestureRecognizer accessibility** — affects all screen reader users; start with most-used pages (MainPage, PrayerCardsPage, PrayerListPage)
3. **C2 — Dynamic announcements** — add SemanticScreenReader.Announce for save/delete/filter/navigation actions
4. **C4 — ColorPickerPopup** — provide alternative accessible color selection
5. **I1 — HeadingLevel** — quick win, add to all page titles and section headings
6. **I3 — Icon descriptions** — quick win for symbol-only elements
7. **I5 — Hide decorative elements** — quick win via IsInAccessibleTree
8. **I2, I4, I6, I7** — medium effort, improve navigation and control associations
9. **N1-N8** — nice-to-have improvements for polish

# A11Y-1: Accessibility Remediation Plan

**Status:** Ready to implement
**Source:** Accessibility audit (2026-03-24) at `docs/research/accessibility-audit.md`
**Skill:** `maui-skills:maui-accessibility`
**Standard:** WCAG 2.1 AA, VoiceOver (iOS), TalkBack (Android)

---

## Overview

The app has partial accessibility (SwipeItem descriptions, some form hints, 44dp touch targets). This plan addresses 4 critical issues, 7 important issues, and select nice-to-haves across 3 phases. Each phase is independently shippable.

---

## Phase 1: Color Contrast + Quick Semantic Wins (1 session)

Low-effort, high-impact fixes that don't change layout or behavior.

### 1A. Fix color contrast failures (C3)

Update `Colors.xaml` color tokens to meet WCAG AA 4.5:1 minimum:

| Token | Current | Target | Change |
|-------|---------|--------|--------|
| MutedText light fg | Gray400 `#919191` | Gray500 `#6E6E6E` or darker | Bump to ≥4.5:1 on White |
| SectionDescription light fg | Gray500 `#6E6E6E` on Secondary `#E8EDE3` | Darker gray or adjust Secondary | Currently 3.4:1 |
| ~~SuccessGreen / SuccessBadge~~ | ~~`#4CAF50` on White~~ | ~~Darken to `#2E7D32`~~ | **Done** — darkened to `#2E7D32` in style refresh (build 27) |
| OnboardingBanner skip link | Gray300 on Tertiary | Gray200 `#C8C8C8` or White | Currently 3.5:1 |
| TagGray chip text | White on `#505050` | Darken tag bg to `#404040` or lighten text | Currently 4.2:1 |
| WelcomePopup/CompletePopup sub-text | Gray500 on Secondary | Same fix as SectionDescription | Currently 3.4:1 |
| TagsPage chevron | Gray300 on Secondary | Darken chevron to Gray500+ | Currently 1.9:1 |

**Validation:** Use a contrast checker tool to verify each pair ≥ 4.5:1.

### 1B. Add HeadingLevel to all pages (I1)

Quick pass — add `SemanticProperties.HeadingLevel="Level1"` to every page title. Add `Level2` to section headings within pages.

**Pages needing Level1:** MainPage, PrayerCardPage, PrayerDetailPage, QuickAddPage, PrayerTimePage, PrayerTimeScopePage, Settings, TagDetailPage, RestoreProgressPage, OnboardingWelcomePopup, OnboardingCompletePopup.

**Already done:** PrayerListPage, TagsPage.

**Section headings (Level2):** Settings card titles ("Notifications", "Prayer Reminders", "Backup & Restore", "Diagnostics").

**Skill note:** Do NOT set `SemanticProperties.Description` on Label elements — let `Text` speak for itself. Only add `HeadingLevel`.

### 1C. Hide decorative elements (I5)

Add `AutomationProperties.IsInAccessibleTree="false"` to:
- All `BoxView` using `DividerLine` style (add to the style itself in `Styles.xaml`)
- Gradient fade BoxView on PrayerTimePage
- Color dot BoxView on TagsPage
- Selected ring Ellipse on TagDetailPage
- Badge count outer Border on PrayerCardsPage (keep inner Label accessible)

### 1D. Add icon/symbol descriptions (I3)

- Favorite star "★" on PrayerCardsPage → `SemanticProperties.Description="Favorited"`
- Chevron "›" on MainPage and TagsPage → `AutomationProperties.IsInAccessibleTree="false"` (decorative)
- Arrow buttons "←" "→" on PrayerTimePage → `SemanticProperties.Description="Previous prayer"` / `"Next prayer"`
- Tag remove "×" on PrayerDetailPage → `SemanticProperties.Description="Remove tag"`

**Testing:** VoiceOver rotor scan on iOS, TalkBack explore-by-touch on Android. Verify headings appear in heading navigation, decorative items are skipped, icons are described.

---

## Phase 2: Interactive Element Accessibility (1–2 sessions)

Replace inaccessible tap-gesture patterns and add dynamic announcements.

### 2A. Fix TapGestureRecognizer accessibility (C1)

This is the most pervasive issue. Strategy per element type:

**Pattern A — Replace Label+TapGesture with Button:**
- OnboardingBanner "Skip tour" → `Button` with `BackgroundColor="Transparent"`
- OnboardingWelcomePopup "Skip tour" → same
- Settings "Privacy Policy" → `Button` styled as link
- PrayerTimePage pause/resume → `Button` (I7)
- PrayerTimePage countdown cycle → `Button`

**Pattern B — Add semantic properties to tappable containers:**
For Grid/Border rows with TapGestureRecognizer that represent list items or cards, add `SemanticProperties.Description` (describing the action) and `SemanticProperties.Hint="Double tap to activate"` to the outermost tappable element.

- MainPage overdue list items (Grid rows)
- MainPage overdue header chevron (tap to navigate)
- PrayerCardsPage card header tap-to-expand
- PrayerCardsPage "+ Add prayer" row
- PrayerListPage prayer row taps
- PrayerListPage tag filter chips (also need selected state in Description)
- TagsPage tag row taps
- TagDetailPage color swatches
- TagDetailPage add-custom-color button

**Skill note — iOS gotcha:** Do NOT set `SemanticProperties.Description` on layout containers (Grid, StackLayout) — it makes children unreachable on iOS/VoiceOver. Instead, set it on the **primary content element** within the container, or restructure as a Button.

**Skill note — Android gotcha:** Do NOT set `SemanticProperties.Description` on Entry/Editor — it breaks TalkBack "double tap to edit". Use `Placeholder` instead.

### 2B. Add dynamic announcements (C2)

Add `SemanticScreenReader.Announce()` calls in ViewModels/code-behind for:

**High priority (user-initiated actions):**
- Prayer saved → `"Prayer saved"`
- Prayer deleted → `"Prayer deleted"`
- Card saved/deleted → `"Card saved"` / `"Card deleted"`
- Tag saved/deleted → `"Tag saved"` / `"Tag deleted"`
- Filter changed on PrayerListPage → `"Showing {count} {filter} prayers"`
- Prayer Time session completed → `"Prayer session complete"`

**Medium priority:**
- Prayer Time card transition → `"Prayer {n} of {total}: {title}"`
- Backup completed → `"Backup saved successfully"`
- Restore completed → `"Restore complete"`
- Validation errors → `SetSemanticFocus()` on error label

### 2C. Swipe discoverability hints (I2)

Add `SemanticProperties.Hint` to SwipeView content elements:
- PrayerCardsPage card items → `"Swipe left to delete, swipe right to edit or favorite"`
- PrayerCardsPage prayer items → `"Swipe left to delete, swipe right to edit"`
- TagsPage tag items → `"Swipe left to delete, swipe right to edit"`

### 2D. Switch label association (I6)

Verify on-device that screen readers read switch descriptions in context with their labels. If not, update `SemanticProperties.Description` to include label text (e.g., `"Allow Notifications toggle, currently on"`).

### 2E. TabIndex on complex pages (I4)

Define explicit `TabIndex` on interactive controls for:
- PrayerDetailPage (edit mode) — Title → Card picker → Details → Reminders toggle → Frequency → Time → Day fields → Tags → Save/Delete
- PrayerTimePage — Previous → CarouselView → Next → Auto → I'm Done → Pause
- Settings — logical top-to-bottom order within each card

**Testing:** Full VoiceOver walkthrough on iOS, TalkBack walkthrough on Android for each affected page. Verify:
- All interactive elements are reachable
- Actions are announced correctly
- Tab order is logical
- Swipe hints are read on first focus

---

## Phase 3: Advanced Accessibility + Polish (1 session)

### 3A. ColorPickerPopup alternative (C4)

The GraphicsView-based picker is inherently inaccessible. Add:
1. A descriptive label at the top of the popup: "Choose a custom color"
2. `SemanticProperties.Description` on both GraphicsViews (informational only)
3. A live-updating Label showing the current color name/hex: `SemanticProperties.Description` bound to hex value
4. Consider: add a "Preset colors" section with named color buttons (e.g., "Red", "Blue", "Forest Green") as an accessible alternative to the visual picker

### 3B. AutomationId for UI testing (N1)

Add `AutomationId` to all key interactive elements. Convention: `{PagePrefix}_{ElementType}_{Name}`.

Examples:
- `AutomationId="Home_Btn_QuickAdd"`
- `AutomationId="Detail_Entry_Title"`
- `AutomationId="List_Filter_Active"`

### 3C. Loading state announcements (N4)

When `IsLoading` transitions:
- `true` → `SemanticScreenReader.Announce("Loading")`
- `false` → `SemanticScreenReader.Announce("Content loaded")`

Affected: PrayerCardsPage, PrayerListPage, TagsPage, PrayerTimePage, RestoreProgressPage.

### 3D. CarouselView accessibility (N3)

- Announce current prayer on card change: `"Prayer {n} of {total}"`
- Ensure ← → buttons are prominent alternatives to swipe (already exist)

### 3E. Popup focus management (N5)

- On popup open: `SetSemanticFocus()` to first interactive element
- On popup close: return focus to triggering element (may need to track in code-behind)

---

## Files Modified Per Phase

### Phase 1
- `Resources/Styles/Colors.xaml` — contrast fixes
- `Resources/Styles/Styles.xaml` — DividerLine a11y, SectionHeading HeadingLevel
- All 16 XAML views — HeadingLevel on titles
- PrayerTimePage.xaml — hide gradient, describe arrows
- PrayerCardsPage.xaml — hide badge border, describe star
- TagsPage.xaml — hide dot/chevron
- TagDetailPage.xaml — hide ring

### Phase 2
- 11+ XAML views — replace TapGesture patterns or add semantics
- 6+ ViewModels — add SemanticScreenReader.Announce calls
- PrayerCardsPage.xaml, TagsPage.xaml — swipe hints
- PrayerDetailPage.xaml, PrayerTimePage.xaml, Settings.xaml — TabIndex
- OnboardingBanner.xaml, OnboardingWelcomePopup.xaml — Button replacements

### Phase 3
- ColorPickerPopup.xaml — accessible alternative
- All XAML views — AutomationId
- 5+ ViewModels — loading announcements
- PrayerTimePage code-behind — carousel announcements
- Popup code-behinds — focus management

---

## Testing Strategy

Each phase ends with a manual screen reader walkthrough:
1. **iOS VoiceOver:** Settings → Accessibility → VoiceOver. Navigate every page with swipe-right (next element). Verify reading order, headings in rotor, all interactive elements reachable.
2. **Android TalkBack:** Settings → Accessibility → TalkBack. Explore by touch on every page. Verify same criteria.
3. **Contrast check:** Use browser-based contrast checker (WebAIM) for all updated color pairs.

---

## Estimated Effort

| Phase | Sessions | Impact |
|-------|----------|--------|
| Phase 1 | 1 | Fixes contrast for all low-vision users, enables heading navigation, removes screen reader noise |
| Phase 2 | 1–2 | Makes all interactive elements reachable, adds dynamic feedback — app becomes usable by screen reader users |
| Phase 3 | 1 | Polish and UI test foundation — professional-grade accessibility |
| **Total** | **3–4** | **Full WCAG 2.1 AA compliance** |

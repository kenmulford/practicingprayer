# Design system

<!--
Project doc (.project/). Cite as `.project/design-system.md#<section>`. Machine-readable
design tokens normally live in `tokens.json` alongside this file; this project keeps
none — the live XAML resource dictionaries (`Resources/Styles/Colors.xaml` +
`Styles.xaml`) are the token set. Absent or all-[TBD] →
no design-lens grounding (design-reviewer / coherence-reviewer / wireframing
skip it). Skip this file entirely for repos with no UI surface. Keep ## headings
stable — they are citation anchors.
-->

## Design tokens
Canonical color, type, spacing, and radius scales. Source of truth is `tokens.json`; describe intent and usage here.
> Source of truth is the live XAML resource dictionaries `PrayerApp/Resources/Styles/Colors.xaml` (color tokens) and `Styles.xaml` (named styles) — there is no separate `tokens.json`; the XAML *is* the token set. Warm Journal aesthetic. Full light + dark via `AppThemeBinding` — never hardcode hex in XAML or C#; reference `StaticResource` tokens. Brand primary `#6B7D5A` for primary actions only; semantic surfaces (`PageLight`/`PageDark`, `CardLight`/`CardDark`); an 8-hue tag palette (light + dark variants, white text).

## Component inventory
The canonical components and where they live. New UI reuses these before introducing a one-off.

| Component | Location | Use for |
|---|---|---|
| Card-wrapping Border | `PrayerApp/Resources/Styles/Styles.xaml` (`PrayerCardBorder`) | every card-wrapping `Border` |
| Color tokens | `PrayerApp/Resources/Styles/Colors.xaml` | all colors (`StaticResource` + `AppThemeBinding`) |
| Form label | `Styles.xaml` (`FormLabel`) | label-above-field forms |
| Reusable named styles | `PrayerApp/Resources/Styles/Styles.xaml` | buttons, chips, text styles |

## Layout & responsive rules
Grid, breakpoints, spacing rhythm, density.
> Forms: label above field, both full-width (`VerticalStackLayout` with `FormLabel` above the input) — never side-by-side. Cards wrapped in `PrayerCardBorder`. Light / dark via `AppThemeBinding`. Prayer Time supports landscape. `SafeAreaEdges` for edge-to-edge layout. Spacing follows existing per-page rhythm (no formal token scale).

## Required states
Every interactive surface must handle these explicitly.
- **Empty:** show an empty-state prompt (e.g. an "Add Prayer" affordance) — never a blank list.
- **Loading:** an `ActivityIndicator` over the loading scrim (`LoadingScrim` tokens); `IsBusy` save-indicator on writes.
- **Error:** surface via `DisplayAlertAsync`; never silently swallow a data error.
- **Disabled:** dim / disable the control (e.g. save while `IsBusy`) — reflect state, don't hide it.

## Accessibility baseline
The standard you hold, plus contrast, focus, target size, and semantics expectations.
> Screen-reader support via `SemanticProperties` (TalkBack / VoiceOver); heading levels; `AutomationId` on testable and interactive elements; suppress-on-load announcement guards; dynamic `Description` bindings. Honor light / dark contrast (Warm Journal tokens chosen for legibility). Touch targets sized for thumb reach. See the `prayer-app-accessibility` skill.

## Voice & microcopy
Tone for labels, errors, and empty states.
> Warm, calm, and personal — a prayer-journaling companion, not a productivity app. Plain, gentle labels; no gamification or streak-pressure language. Empty states invite ("Add your first prayer"); errors reassure. No social or broadcast framing (OS share sheets are a convenience, not a feature).

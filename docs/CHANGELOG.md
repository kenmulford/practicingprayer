# Changelog

> App store release notes, maintained continuously. Reset when a new version/build tag is created.
>
> **Current baseline:** `1.0.4b18`

---

## What's New (since 1.0.4 build 18)

### New Features

- **Prayer Time from notifications** — Tap a prayer reminder notification to jump straight into a focused Prayer Time session with your recently notified prayers.
- **Move prayers between cards** — Editing a prayer request now lets you reassign it to a different prayer card. I'm sure I'm not the only one who created a request on the wrong card. Right?
- **Custom color management** — Long-press any custom color swatch to remove it from your palette. Default colors are protected.

### Improvements

- **Smarter "Recently Notified" tagging** — The tag now reflects prayers that were actually notified recently, not just prayers with notifications enabled. Recalculated each time you open the app.
- **Configurable overdue threshold** — Choose how many days without prayer before a request shows up as overdue. Adjustable in Settings (default: 30 days).
- **Overdue guidance** — When you're all caught up, the home page explains what the overdue section is for and how to adjust it.
- **Swipe through Prayer Time** — Swipe left and right to navigate between prayers with smooth card-to-card transitions.
- **Mark Answered updates everywhere** — Marking a prayer as answered now immediately reflects on both the prayer list and card views.
- **System tags** — The "Recently Notified" tag is clearly labeled as system-managed. You can personalize its color but not rename or delete it. It always appears at the top of your tag list.
- **Scroll indicator on long prayers** — A subtle fade at the bottom of prayer details lets you know there's more to read.
- **Cleaner input fields** — Refined styling on form fields across the app.
- **Removed non-rendering icons** — Replaced placeholder emoji glyphs that weren't displaying on iOS.

### Bug Fixes

- Fixed color picker saving the wrong color on iOS.
- Fixed Prayer Time swipe gestures being blocked by scrollable content.
- Removed unused location permission prompts from iOS.
- Cleaned up dead code and unused event handlers.

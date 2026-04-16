# Onboarding Review & Refresh Plan

> Planning doc only -- no code changes.
> Created: 2026-03-27

---

## 1. Current State Assessment

### Flow

The onboarding is a linear 3-step guided tour that walks a first-time user through the core loop: create a prayer card, add a prayer request, start prayer time.

**Steps (enum `OnboardingStep`):**

| Step              | Where it appears           | What the user does                                    |
|-------------------|----------------------------|-------------------------------------------------------|
| `Welcome`         | Popup on Home (MainPage)   | "Get Started" navigates to Cards tab; "Skip tour" ends early |
| `CreateCard`      | Banner on PrayerCardsPage  | Tap "Add Card" in toolbar                             |
| `NameCard`        | Banner on PrayerCardPage   | Type a card title, tap Save                           |
| `AddRequest`      | Banner on PrayerCardsPage  | Expand the new card, tap "+ Add prayer"               |
| `NameRequest`     | Banner on PrayerDetailPage | Enter a prayer title, tap Save                        |
| `PrayerTime`      | Banner on MainPage (Home)  | Tap "Prayer Time" button, choose "All Requests"       |
| `PrayerTimeActive`| Banner on PrayerTimePage   | Swipe through cards, tap "I'm done"                   |
| `Complete`        | Popup (from AppShell)      | "You're all set!" -- tap Done                         |

### Mechanics

- **OnboardingService** persists the current step to `Preferences` and exposes a `StepChanged` event.
- **OnboardingBanner** is a reusable `ContentView` with `ExpectedStep`, `HeadlineText`, `SubText` bindable properties. It self-shows/hides by subscribing to `StepChanged`.
- Banners display a step counter ("STEP 1 OF 3") mapped from the enum value.
- Every banner includes a "Skip tour" button.
- The welcome popup blocks interaction (`CanBeDismissedByTappingOutsideOfPopup="False"`).
- The completion popup is shown from `AppShell.StepChanged` handler, with a guard that silently completes if the user is mid-edit (`IEditGuard.IsDirty`).
- `Reset()` exists on the service but is not currently exposed in any UI (no "Replay Tour" button in Settings).

### What it covers

Only the core loop: Cards -> Requests -> Prayer Time. Nothing else.

---

## 2. What's Changed Since Onboarding Was Built

Features and pages that now exist but are **not mentioned or introduced** by onboarding:

| Feature/Page | Description |
|---|---|
| **Quick Add** (Home) | One-tap prayer creation from Home -- goes to a "Quick Add" card. Users may not understand where these prayers end up. |
| **Tags tab** | Full tag management (create, color-pick, assign to prayers). A dedicated tab that new users won't discover from onboarding. |
| **Tag assignment on prayers** | PrayerDetailPage now has tag search/chip UI in edit mode. |
| **Settings Hub** | New top-level tab with 4 sub-pages (App Settings, Backup & Restore, About, Help). |
| **Backup & Restore** | Export/import prayer data. Critical for data safety -- users should know it exists. |
| **Help / FAQ** | Expandable FAQ page under Settings. Could reduce support requests if users find it. |
| **Prayer frequency & reminders** | Per-prayer scheduling (daily/weekly/monthly) with notification time/day pickers. |
| **Overdue prayers** (Home) | Home page now shows overdue prayer suggestions with configurable threshold. |
| **Prayer Time scoping** | "By Tags" option on the Prayer Time action sheet -- filter prayer sessions by tag. |
| **Prayer Time auto-advance** | Timer-based auto-advance with pause/resume, interval cycling. |
| **Landscape mode** | Prayer Time supports landscape orientation (configurable in App Settings). |
| **Swipe gestures** on cards | Left-swipe for delete, right-swipe for edit/favorite on PrayerCardsPage. Not discoverable. |
| **Share button** | Prayer detail view has a Share action. |
| **"Mark Answered"** | Prayers can be marked answered with a date badge. |
| **Favorites** | Star/unstar cards via swipe gesture. |
| **Search** | PrayerCardsPage has a search bar. |

---

## 3. Gap Analysis

### High-priority gaps (features users need to discover)

1. **Quick Add confusion** -- UX-15 in the backlog already flags this. Onboarding doesn't mention Quick Add at all. Users who tap it first won't understand where the prayer went or how it relates to cards.

2. **Tags are invisible** -- The Tags tab is a top-level nav item, but onboarding never points users there. Tags are powerful for organizing prayers and scoping Prayer Time, but easily missed.

3. **Backup & Restore** -- Privacy-first app with no cloud sync means local backup is the user's only data safety net. New users should be nudged toward this early.

4. **Swipe gestures are hidden** -- Edit, delete, and favorite are all behind swipe gestures with no visual hint. Users who don't know to swipe will think cards can't be edited.

### Medium-priority gaps

5. **Prayer frequency/reminders** -- The scheduling UI exists but onboarding doesn't mention it. Users may never discover they can set per-prayer reminders.

6. **Overdue list on Home** -- Users may not understand why certain prayers appear on the home page or how to configure the threshold.

7. **Prayer Time features** -- Auto-advance, tag scoping, and landscape mode are all undiscoverable without exploration.

### Lower-priority gaps

8. **Help/FAQ exists** -- Users with questions may not think to check Settings > Help.

9. **Completion popup is generic** -- "Enjoy the app!" doesn't point users anywhere specific. A missed opportunity to surface next steps.

---

## 4. Refresh Proposals

### Quick wins (text/banner updates, minimal code)

| # | Proposal | Effort |
|---|----------|--------|
| Q1 | **Update completion popup text** -- Replace "Enjoy the app!" with 2-3 bullet points: "Explore Tags to organize your prayers," "Set up Backup in Settings," "Swipe cards to edit or favorite." | ~1 hr |
| Q2 | **Add "Replay tour" to Settings Hub or Help page** -- Wire up `OnboardingService.Reset()` to a button. Lets users re-discover features after initial setup. | ~1 hr |
| Q3 | **Update welcome popup subtitle** -- "Let's set up your first prayer card" is fine, but could add a line: "You can skip this and explore on your own anytime." Reduces pressure. | ~30 min |

### Medium effort (new steps, banners, or one-time hints)

| # | Proposal | Effort |
|---|----------|--------|
| M1 | **Add a "Tips" system for post-onboarding feature discovery** -- After onboarding completes, show one-time contextual tips (not banners) on pages the user visits for the first time. Examples: "Swipe a card to edit or favorite" on PrayerCardsPage, "Add tags to organize and filter prayer time" on PrayerDetailPage. Uses `Preferences` flags like `Tip_SwipeCards_Shown`. Doesn't change the onboarding flow itself. | 4-6 hrs |
| M2 | **Add a Quick Add explanation step** -- Either during onboarding or as a post-onboarding tip on Home, explain what Quick Add does and where those prayers go. Ties into UX-15. | 2-3 hrs |
| M3 | **Expand completion popup into a "What's Next" screen** -- Instead of a simple popup, show a checklist-style screen: "Set up your first tag," "Enable backup," "Try Prayer Time by tags." Items link to relevant pages. Could be a one-time page rather than a popup. | 3-4 hrs |
| M4 | **Add a banner or hint to the Tags tab** -- First-visit hint explaining what tags are for and how they connect to Prayer Time scoping. | 1-2 hrs |

### Larger scope (flow redesign)

| # | Proposal | Effort |
|---|----------|--------|
| L1 | **Restructure onboarding into "Core Tour" + "Feature Discovery"** -- Keep the current 3-step tour for the core loop but add a separate, optional "Discover Features" flow accessible from Settings or the completion screen. Covers tags, backup, reminders, swipe gestures. Each section is self-contained. | 8-12 hrs |
| L2 | **Interactive onboarding with sample data** -- Pre-populate a sample card ("Example: My Family") with sample prayers during onboarding so users see a populated app immediately. Delete sample data when they create their first real card, or let them keep it. | 6-8 hrs |
| L3 | **Animated walkthrough overlays** -- Semi-transparent overlays that highlight specific UI elements (toolbar "Add Card" button, swipe area, etc.) instead of bottom banners. More visually guided but significantly more complex. | 12-16 hrs |

---

## 5. Questions for Ken

1. **Scope of refresh** -- Are you thinking quick wins only (update text, add replay button), or is this a candidate for a medium-effort feature discovery system?

2. **Quick Add in onboarding** -- Should onboarding mention Quick Add at all, or should it stay focused on the "proper" card-first flow? (UX-15 may address this separately.)

3. **Backup nudge timing** -- Should we nudge users about Backup during onboarding, or wait until they have data worth backing up (e.g., after 5+ prayers created)?

4. **"Replay tour" location** -- Settings Hub, Help page, or About page? Or all three?

5. **Tips system vs. expanded onboarding** -- Would you prefer one-time contextual tips that appear on individual pages (M1), or expanding the guided tour itself with more steps? Tips are less intrusive but more scattered; more steps risk making the tour feel long.

6. **Sample data** -- Any interest in the sample data approach (L2), or does that conflict with the "your private prayer space" feel?

7. **Priority relative to backlog** -- Where does this sit against AUD-1 (audit remediation) and UX-15? Quick wins could ship alongside other work; medium/large proposals need their own sprint.

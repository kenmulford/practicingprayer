# Closed Testing Feedback Log

Verbatim tester feedback for reference when responding in Google Play Console.
Format: date · tester · raw quote · mapped bug ID.

---

## Round 2 — 2026-03-17

### Ken (developer)

> "the UX is nice but does not feel very 'iOS' native; especially the entry/editor fields. I know we did the styling for Android, but perhaps we've done too much to detract from the iOS native UX? Worth evaluating."

**Feature:** F-13
**Status:** Open
**Notes:** Entry/Editor field styling was tuned for Android and may over-ride iOS platform defaults in a way that feels off. Need to evaluate whether custom styles should be conditional on platform, or scaled back to let iOS render natively.

---

> "Prayer Time: When I reach the end (you've prayed for them all!), I see the timer in the upper right corner (30s) .. at least text. That def shouldn't be there at that point. Let's evaluate visual states for the timer feature."

**Bug:** BUG-18
**Status:** Open
**Notes:** Timer UI element (30s text / countdown) remains visible on the "all done" end state of Prayer Time. Timer should be hidden or reset when the session completes. Full visual state audit of the timer needed — consider hidden, counting, and done states.

---

### Liz

> "prayer request title shouldn't be prepopulated. It's annoying."

**Bug:** BUG-17
**Status:** Open
**Notes:** Title field on new prayer request form is pre-populated with something (likely card name or placeholder text). Should start blank so user types their own title.

---

> "why is the 'prayer' page there? It seems redundant."
> _(follow-up discussion: goal is to manage prayers without opening/closing cards, but this isn't explained or intuitive. Suggestions: live-filter search bar + tags filter + 3-way toggle for completed/active/all. Open to other ideas.)_

**Feature:** F-12
**Status:** Open
**Notes:** Prayer list page needs clearer purpose and better UX. Proposed approach: add search bar with live filtering, retain tag filter, add active/completed/all 3-way toggle. Consider adding an empty-state explanation of what the page is for.

---

## Round 1 — 2026-03-15

### Tony
> "the color selector on the new tag page won't let me select the 'gray' icon. It's half off the screen and he cannot scroll the colors horizontally."

**Bug:** BUG-7
**Status:** Open

---

### Todd
> "backup failed immediately when tapped"
> _(photo attached — screenshot of failure)_

**Bug:** BUG-8
**Status:** Fixed
**Root cause:** `PRAGMA wal_checkpoint(TRUNCATE)` returns a result set; `ExecuteAsync` calls `ExecuteNonQuery` internally which throws `SQLiteException("not an error")` on receiving `SQLITE_ROW`. Fixed by switching to `ExecuteScalarAsync<int>` which correctly reads the first column and discards the rest.

---

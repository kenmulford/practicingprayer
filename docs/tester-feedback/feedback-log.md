# Closed Testing Feedback Log

Verbatim tester feedback for reference when responding in Google Play Console.
Format: date · tester · raw quote · mapped bug ID.

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
**Status:** Open
**Note:** Investigating whether a missing Android permission declaration or a runtime exception in `BackupService.ExportAsync()` is the root cause.

---

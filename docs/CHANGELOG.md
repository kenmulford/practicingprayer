# Changelog

> App store release notes, maintained continuously. Reset when a new version/build tag is created.
>
> **Current baseline:** `1.0.6b30`

---

## What's New (since 1.0.6 build 30)

### Improvements

- Settings reorganized into four clear sections: App Settings, Backup & Restore, About, and Help
- New Help page with answers to common questions about cards, prayers, tags, reminders, and data privacy
- New About page showing app version, privacy policy, and website link

### Bug Fixes

- Prayer card counts now update reliably when switching between tabs
- Moving a prayer to a different card correctly updates both the old and new card's count
- Restoring a backup now immediately shows restored data (no restart needed)
- Unsaved changes prompt now appears when switching tabs while editing
- Notification time and day changes are now tracked as unsaved changes
- Card delete confirmation now shows accurate prayer count even for collapsed cards
- Overdue filter now refreshes correctly when returning from other tabs
- Notification scheduling errors no longer prevent prayers from saving
- Moving a prayer to a different card now correctly removes it from the original card

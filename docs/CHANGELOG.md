# Changelog

> App store release notes, maintained continuously. Reset when a new version/build tag is created.
>
> **Current baseline:** `1.0.6b30`

---

## What's New (since 1.0.6 build 30)

### New Features

- **Portrait mode for Prayer Time** — New toggle in Settings → App Settings lets you keep Prayer Time in portrait orientation instead of landscape
- **Quick Add tip** — First-time Quick Add users see a helpful note explaining where their prayers are saved and how to reorganize them later

### Improvements

- Improved VoiceOver support: prayer list items and card headers now read meaningful summaries instead of individual labels
- Subtle separator lines between prayers in the list for easier visual scanning
- Settings reorganized into four clear sections: App Settings, Backup & Restore, About, and Help
- New Help page with answers to common questions about cards, prayers, tags, reminders, and data privacy
- New About page showing app version, privacy policy, and website link
- Help page now includes answers about portrait mode and Prayer Time orientation

### Bug Fixes

- Prayer card counts now update reliably when switching between tabs
- Moving a prayer to a different card correctly updates both the old and new card's count
- Restoring a backup now immediately shows restored data (no restart needed)
- Unsaved changes prompt now appears when switching tabs while editing
- Notification time and day changes are now tracked as unsaved changes
- Card delete confirmation now shows accurate prayer count even for collapsed cards
- Fixed a crash on iOS when saving a tag (native gesture cleanup during page teardown)
- Unsaved changes prompt now works correctly with iOS back button and swipe-back gesture (swipe-back disabled on edit pages to prevent data loss)
- Quick Add and Prayer Time tag picker now display as card-style sheets on iPad instead of full-screen
- Prayer Time button no longer allows double-tap during navigation
- Prayer card expansion now displays correctly on iPad (prayers and "Add prayer" button visible)
- Overdue filter now refreshes correctly when returning from other tabs
- Notification scheduling errors no longer prevent prayers from saving
- Moving a prayer to a different card now correctly removes it from the original card

# BUG-75 — PrayerDetailPage Save toolbar + tab bar disappeared after tag select

**Filed:** 2026-04-21 (UAT, tester device)
**Observed on:** iPhone 17 (single occurrence, non-reproducible)
**Platform scope:** NOT confirmed iPad-only — happened on iPhone
**Status:** Root cause unconfirmed; hardening proposed below

---

## Repro flow (as witnessed)

1. New card → Save.
2. New request on the new card → Save.
3. **Second** new request on same card (via back → "+ Add Prayer", so a fresh `PrayerDetailPage` instance).
4. Filled fields including a tag → tapped "+ Add Tag" → `TagPickerPage` opened as modal.
5. Selected tag → Done (modal dismissed).
6. Dismissed keyboard.
7. **Observed:** Save toolbar item missing from the top nav bar AND the bottom tab bar missing.
8. Could not reproduce after close + reopen.

---

## Code paths reviewed

- `PrayerApp/Views/Prayer/PrayerDetailPage.xaml.cs:154` — `UpdateToolbarItems` (Clear+Add rebuild)
- `PrayerApp/ViewModels/PrayerRequestDetailViewModel.cs:682` — `OpenTagPickerAsync` / `SyncTagsFromPicker`
- `PrayerApp/Views/Tags/TagPickerPage.xaml.cs` — modal implements `IPageSheetModal`
- `PrayerApp/ViewModels/TagPickerViewModel.cs:212` — `DoneAsync` (signals dismiss before awaiting PopModal)
- `PrayerApp/AppShell.xaml.cs:120` — Shell navigation / push animation
- `PrayerApp/Platforms/iOS/Handlers/ModalPageSheetHandler.cs` — iPad PageSheet mapping (no-op on iPhone per `IPageSheetModal.cs` comment: *"On iPhone, PageSheet and FullScreen are visually identical — no effect."*)
- Lesson: `Lessons/maui-toolbaritem-automationid-set-once.md` — *"the 'rebuild' path has its own state bugs (lost icons, lost handlers)"*

---

## Analysis

### Two symptoms, but the code paths are different

**Save toolbar item** is managed by `UpdateToolbarItems()` at `PrayerDetailPage.xaml.cs:154-167`. It calls `ToolbarItems.Clear()` then re-adds cached `_saveToolbarItem` / `_saveAndNewToolbarItem` instances. Triggered by:
- `OnAppearing` (fires after modal pop on iPhone full-screen modals).
- `IsReadOnly` or `ShowSaveAndNew` PropertyChanged.

**Tab bar** is pure Shell chrome. Nothing in our code hides it on `PrayerDetailPage` — only `PrayerTimePage.xaml` sets `Shell.TabBarIsVisible="False"`. Its disappearance is a platform-layer event, not our C#.

Both vanishing together points to a **Shell-chrome-layout event**, not two independent logic bugs.

### Ranked hypotheses

1. **(Strongest) `ToolbarItems.Clear()+Add()` rebuild racing with the modal dismiss.** `UpdateToolbarItems` at `PrayerDetailPage.xaml.cs:156` unconditionally clears then re-adds the same cached `_saveToolbarItem` instance. Our lesson already flags this pattern as fragile. After `TagPickerPage` pops, `OnAppearing` fires → `UpdateToolbarItems` runs. If the prior parent teardown hasn't fully settled (modal dismiss animation still in flight, or iPhone PageSheet semantics leave a layout pending), the re-add can silently no-op at the platform layer. C# collection has one item, native bar shows zero.

2. **Shell chrome re-layout after modal dismiss + keyboard hide on iOS.** `SafeAreaEdges="Container"` on `PrayerDetailPage` relies on Shell to compute insets. A modal dismiss + `AdjustResize`-style keyboard hide happening in the same frame can leave UIKit's presentation context stale — collapsing both the nav-bar ToolbarItems area and the tab bar to zero-height / pushed off-screen. Nothing in our code explicitly hides them, consistent with a platform layout glitch.

3. **Race between `SyncTagsFromPicker` and `OnAppearing`.** `TagPickerViewModel.DoneAsync` sets `_dismissed.TrySetResult()` **before** awaiting `PopModalAsync`, so `SyncTagsFromPicker` runs (mutating `SelectedTags` → `RebuildEditTagChips` on the covered page) while the modal animation is still finishing. Its `Clear()/Add()` on `editTagChips` forces a layout pass *before* `OnAppearing`'s `UpdateToolbarItems`. If Shell catches the first layout with stale bounds, chrome can render empty.

### Best single explanation

#1 and #2 are the same underlying MAUI Shell bug class, triggered by the specific sequence:

```
modal pop → keyboard hide → OnAppearing → ToolbarItems.Clear()+Add()
```

happening within one or two frames. The tester's iPhone 17 means this is not iPad-only — but the iPhone 17 is a high-refresh / fast device, which tightens the timing window for this kind of race.

### Re: PageSheet as a contributing factor

Ken's instinct — that the PageSheet modal presentation might be part of it — is reasonable. `TagPickerPage` implements `IPageSheetModal`, mapped to `UIModalPresentationStyle.PageSheet` on iOS in `ModalPageSheetHandler.cs`. On iPhone, Apple renders PageSheet as full-screen from iOS 13+, but the **presentation context** semantics are still PageSheet (e.g., parent view controller stays in the stack rather than being covered by a new window). That means:

- On iPhone PageSheet, the parent's `viewDidDisappear`/`viewDidAppear` lifecycle fires differently from a true full-screen modal.
- MAUI's `OnDisappearing`/`OnAppearing` are bridged from those, so there may be a window where OnAppearing fires before UIKit has fully restored the parent's layout context.

That said: PageSheet is there for the iPad UX tradeoff and doesn't obviously need to change. The hardening below targets the C# side (where we have control), leaving PageSheet semantics alone.

---

## Recommended hardening (ship without repro)

### 1. Diff-instead-of-rebuild in `UpdateToolbarItems`

Only mutate `ToolbarItems` when the desired set differs from the current set. Removes the Clear+Add race on every `OnAppearing`. ~15 lines.

```csharp
private void UpdateToolbarItems(PrayerRequestDetailViewModel vm)
{
    var desired = new List<ToolbarItem>();
    if (vm.IsReadOnly)
    {
        desired.Add(_editToolbarItem);
    }
    else
    {
        if (vm.ShowSaveAndNew) desired.Add(_saveAndNewToolbarItem);
        desired.Add(_saveToolbarItem);
    }

    if (ToolbarItems.SequenceEqual(desired)) return;

    ToolbarItems.Clear();
    foreach (var item in desired) ToolbarItems.Add(item);
}
```

### 2. Defensive safety net at end of `OnAppearing`

If, after our update, `IsEditable && ToolbarItems.Count == 0`, log the condition and force a rebuild on the next dispatcher tick. Catches the race if it happens again and gives us a breadcrumb.

### 3. (Optional, later) Revisit `_dismissed.TrySetResult()` ordering in `TagPickerViewModel.DoneAsync`

Right now the parent runs `SyncTagsFromPicker` while the child's `PopModalAsync` is still awaited. Flipping the order (await PopModal first, then signal) would serialize the layout work and remove hypothesis #3 as a contributor. Small behavior change — defer unless #1/#2 don't hold.

### 4. Keep `IPageSheetModal` on `TagPickerPage`

PageSheet is the right iPad UX; the hardening above doesn't require dropping it. If a second repro points at PageSheet specifically on iPhone, we can revisit — but don't change it preemptively.

---

## Next step if seen again

Before any further input: grab a screenshot + `Shell.Current.CurrentPage` page source dump. Capture:
- `ToolbarItems.Count` on the current page
- `Shell.GetTabBarIsVisible(page)` value
- Current orientation
- Whether the page's VerticalStackLayout is still laid out (via `Height` / `Y`)

That tells us in one pass whether the Save toolbar is *logically* present (#1 failure) or *both logical and visible are gone* (#2 failure).

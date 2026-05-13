# StyledPicker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a `StyledPicker` shared component, migrate the 2 broken Picker call-sites to use it, and ship the shared-components convention doc.

**Architecture:** `ContentView` subclass at `PrayerApp/Views/Shared/StyledPicker.{xaml,xaml.cs}` exposing `BindableProperty` declarations in code-behind. Inner Picker bindings via `{Binding <Prop>, Source={x:Reference root}}`. Matches the existing `LoadingOverlay` precedent 1:1.

**Tech Stack:** .NET 10 MAUI (`Microsoft.Maui.Controls 10.0.50`), XAML, `BindableProperty`, xUnit 2.9 (existing suite), Git.

**Spec:** `docs/superpowers/specs/2026-05-12-styled-picker-design.md` (commit `032dd1f`).

**Issue:** https://github.com/kenmulford/PracticingPrayer/issues/35

**Branch:** `fix/35-styled-picker-component` (off `dev`, pushable per OSS issue-linked-branch rule).

**Testing note:** `PrayerApp.Tests` is a `Microsoft.NET.Sdk` (non-MAUI) project that does not compile View XAML. xUnit unit tests for `StyledPicker` are not infrastructure-feasible without adding `<Compile Include>` for the generated XAML partials AND wiring the MAUI SDK into the test project. **YAGNI** for this slice. The regression net for this work is: (a) the existing **729 passing + 4 skipped** xUnit baseline stays green, (b) the Android build succeeds, (c) Mac UAT confirms the visual chrome on both migrated pages post-merge.

---

## File map

**Create:**
- `PrayerApp/Views/Shared/StyledPicker.xaml`
- `PrayerApp/Views/Shared/StyledPicker.xaml.cs`
- `docs/conventions/shared-components.md`

**Modify:**
- `PrayerApp/Views/ConfirmImportPage.xaml` — add `xmlns:shared` namespace, replace inline composite at lines 38-45
- `PrayerApp/Views/PrayerCard/PrayerCardPage.xaml` — add `xmlns:shared` namespace, replace inline composite at lines 40-48

**No test files created** (see Testing note above).

---

## Task 1: Branch setup

**Files:**
- (no files modified)

- [ ] **Step 1: Create the issue-linked branch off dev**

Run:
```bash
git checkout dev && git pull origin dev && git checkout -b fix/35-styled-picker-component
```

Expected: clean working tree on `fix/35-styled-picker-component`. `git status` shows nothing to commit.

---

## Task 2: Create `StyledPicker` code-behind

**Files:**
- Create: `PrayerApp/Views/Shared/StyledPicker.xaml.cs`

- [ ] **Step 1: Write `StyledPicker.xaml.cs`**

Create file `PrayerApp/Views/Shared/StyledPicker.xaml.cs` with this exact content:

```csharp
using System.Collections;

namespace PrayerApp.Views.Shared;

/// <summary>
/// Reusable styled Picker with the app's inset-chrome convention.
/// Wraps the inner Picker in a <see cref="Border"/> styled by
/// <c>PickerField</c> and adds a decorative <c>PickerIndicator</c> chevron.
/// Consumers set BindableProperty values on this component; they pass
/// through to the encapsulated Picker via {x:Reference root} bindings
/// in StyledPicker.xaml.
/// </summary>
public partial class StyledPicker : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(StyledPicker));

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IList), typeof(StyledPicker));

    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(StyledPicker),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly BindableProperty ItemDisplayBindingProperty =
        BindableProperty.Create(nameof(ItemDisplayBinding), typeof(BindingBase), typeof(StyledPicker));

    public static readonly BindableProperty SemanticHintProperty =
        BindableProperty.Create(nameof(SemanticHint), typeof(string), typeof(StyledPicker));

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public IList? ItemsSource
    {
        get => (IList?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public BindingBase? ItemDisplayBinding
    {
        get => (BindingBase?)GetValue(ItemDisplayBindingProperty);
        set => SetValue(ItemDisplayBindingProperty, value);
    }

    public string? SemanticHint
    {
        get => (string?)GetValue(SemanticHintProperty);
        set => SetValue(SemanticHintProperty, value);
    }

    public StyledPicker()
    {
        InitializeComponent();
    }
}
```

---

## Task 3: Create `StyledPicker` XAML

**Files:**
- Create: `PrayerApp/Views/Shared/StyledPicker.xaml`

- [ ] **Step 1: Write `StyledPicker.xaml`**

Create file `PrayerApp/Views/Shared/StyledPicker.xaml` with this exact content:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="PrayerApp.Views.Shared.StyledPicker"
             x:Name="root">
    <Border Style="{StaticResource PickerField}">
        <Grid ColumnDefinitions="*,Auto">
            <Picker Grid.Column="0"
                    Title="{Binding Title, Source={x:Reference root}}"
                    ItemsSource="{Binding ItemsSource, Source={x:Reference root}}"
                    ItemDisplayBinding="{Binding ItemDisplayBinding, Source={x:Reference root}}"
                    SelectedItem="{Binding SelectedItem, Source={x:Reference root}, Mode=TwoWay}"
                    AutomationId="{Binding AutomationId, Source={x:Reference root}}"
                    SemanticProperties.Hint="{Binding SemanticHint, Source={x:Reference root}}"
                    HorizontalOptions="Fill" />
            <Label Grid.Column="1" Style="{StaticResource PickerIndicator}" />
        </Grid>
    </Border>
</ContentView>
```

- [ ] **Step 2: Verify Android build succeeds**

Run:
```bash
dotnet build PrayerApp/PrayerApp.csproj -f net10.0-android 2>&1 | tail -20
```

Expected: `Build succeeded.` with 0 errors. (Warnings about LF/CRLF are noise; ignore.)

If the build fails, the most likely cause is `InitializeComponent()` not resolving — confirm `x:Class="PrayerApp.Views.Shared.StyledPicker"` in the XAML matches `namespace PrayerApp.Views.Shared` + `class StyledPicker` in the code-behind.

- [ ] **Step 3: Run the existing xUnit suite — regression check**

Run:
```bash
(cd PrayerApp.Tests && dotnet test 2>&1) | tail -5
```

Expected: `Passed!  - Failed:     0, Passed:   729, Skipped:     4, Total:   733`.

If `Passed` count differs, investigate. The component is in `Views/Shared/` which `PrayerApp.Tests.csproj` doesn't Compile-Include, so test outcomes should be unchanged from the baseline.

- [ ] **Step 4: Commit StyledPicker component**

Run:
```bash
git add PrayerApp/Views/Shared/StyledPicker.xaml PrayerApp/Views/Shared/StyledPicker.xaml.cs
git commit -m "$(cat <<'EOF'
feat(views): add StyledPicker shared component (#35)

ContentView subclass at PrayerApp/Views/Shared/StyledPicker.{xaml,xaml.cs}
that encapsulates the Border(PickerField) + Grid + Picker + Label(PickerIndicator)
composite chrome the app already uses at 4 working Picker call-sites. Matches
the existing LoadingOverlay shared-component pattern: BindableProperty
declarations in code-behind, inner Picker bindings via {x:Reference root}.

API: Title, ItemsSource, SelectedItem, ItemDisplayBinding, SemanticHint.
AutomationId is inherited from Element and the inner Picker binds to it
so consumer-set AutomationId values propagate to the native control
for Appium discovery.

Migration of broken call-sites follows in subsequent commits on this branch.
EOF
)"
```

Expected: commit succeeds. If the `/simplify` gate fires, retry per the gate's documented release-on-retry behavior — this is the first commit of the slice and `/simplify` runs at Task 8.

---

## Task 4: Migrate `ConfirmImportPage`

**Files:**
- Modify: `PrayerApp/Views/ConfirmImportPage.xaml` (namespace declaration block + lines 36-45)

- [ ] **Step 1: Add the `xmlns:shared` namespace declaration**

Edit `PrayerApp/Views/ConfirmImportPage.xaml` lines 2-7 from:

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:viewModels="clr-namespace:PrayerApp.ViewModels"
             xmlns:behaviors="clr-namespace:PrayerApp.Behaviors"
             x:Class="PrayerApp.Views.ConfirmImportPage"
             x:DataType="viewModels:ConfirmImportViewModel"
```

to:

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:viewModels="clr-namespace:PrayerApp.ViewModels"
             xmlns:behaviors="clr-namespace:PrayerApp.Behaviors"
             xmlns:shared="clr-namespace:PrayerApp.Views.Shared"
             x:Class="PrayerApp.Views.ConfirmImportPage"
             x:DataType="viewModels:ConfirmImportViewModel"
```

- [ ] **Step 2: Replace the inline Collection-picker composite with `<shared:StyledPicker>`**

Edit `PrayerApp/Views/ConfirmImportPage.xaml` lines 36-45 from:

```xml
                    <Label Text="Collection"
                           Style="{StaticResource FormLabel}" />
                    <Grid ColumnDefinitions="*, Auto">
                        <Picker ItemsSource="{Binding AvailableBoxes}"
                                SelectedItem="{Binding SelectedBox}"
                                AutomationId="ConfirmImport_Picker_Box"
                                SemanticProperties.Hint="Choose which collection this imported card belongs to"
                                HorizontalOptions="Fill" />
                        <Label Grid.Column="1" Style="{StaticResource PickerIndicator}" />
                    </Grid>
```

to:

```xml
                    <Label Text="Collection"
                           Style="{StaticResource FormLabel}" />
                    <shared:StyledPicker ItemsSource="{Binding AvailableBoxes}"
                                         SelectedItem="{Binding SelectedBox}"
                                         AutomationId="ConfirmImport_Picker_Box"
                                         SemanticHint="Choose which collection this imported card belongs to" />
```

Notes for the engineer:
- Attribute changed: `SemanticProperties.Hint=` → `SemanticHint=` (StyledPicker exposes this as a direct property and forwards to the inner Picker's `SemanticProperties.Hint`).
- `HorizontalOptions="Fill"` is omitted because `<shared:StyledPicker>` controls its own inner Picker's layout. If a future call-site needs to override outer layout, set it on `<shared:StyledPicker>` directly (ContentView passes through to the encapsulated tree).
- `AutomationId` is the same — keeps the existing UITest selector working post-migration.

- [ ] **Step 3: Verify Android build succeeds**

Run:
```bash
dotnet build PrayerApp/PrayerApp.csproj -f net10.0-android 2>&1 | tail -20
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 4: Run the existing xUnit suite — regression check**

Run:
```bash
(cd PrayerApp.Tests && dotnet test 2>&1) | tail -5
```

Expected: `Passed!  - Failed:     0, Passed:   729, Skipped:     4, Total:   733`.

- [ ] **Step 5: Commit the ConfirmImportPage migration**

Run:
```bash
git add PrayerApp/Views/ConfirmImportPage.xaml
git commit -m "$(cat <<'EOF'
fix(import): migrate ConfirmImportPage Collection picker to StyledPicker (#35)

Replaces the inline Border-less Grid+Picker+Label composite at
lines 36-45 with <shared:StyledPicker>. This is the primary fix for
issue #35 — the chevron now sits inside the PickerField Border chrome
exactly like the 4 working call-sites in PrayerDetailPage.

SemanticProperties.Hint attribute moved to the SemanticHint property
on StyledPicker (which forwards to the inner Picker's
SemanticProperties.Hint). AutomationId preserved.
EOF
)"
```

---

## Task 5: Migrate `PrayerCardPage`

**Files:**
- Modify: `PrayerApp/Views/PrayerCard/PrayerCardPage.xaml` (namespace declaration block + lines 40-48)

- [ ] **Step 1: Confirm the file's current namespace declaration block**

Run:
```bash
sed -n '1,10p' PrayerApp/Views/PrayerCard/PrayerCardPage.xaml
```

If `xmlns:shared="clr-namespace:PrayerApp.Views.Shared"` is already present in the file (it might be, since some pages already have it), **skip Step 2** and go to Step 3. Otherwise add it via Step 2.

- [ ] **Step 2: Add the `xmlns:shared` namespace declaration if missing**

Add this line to the `<ContentPage ...>` opening element's attribute block, after the last existing `xmlns:` declaration:

```xml
             xmlns:shared="clr-namespace:PrayerApp.Views.Shared"
```

Preserve attribute alignment for diff readability.

- [ ] **Step 3: Replace the inline Collection-picker composite with `<shared:StyledPicker>`**

Edit `PrayerApp/Views/PrayerCard/PrayerCardPage.xaml` lines 40-48 from:

```xml
                            <Label Text="Collection" Style="{StaticResource FormLabel}" />
                            <Grid ColumnDefinitions="*, Auto">
                                <Picker ItemsSource="{Binding AvailableBoxes}"
                                        SelectedItem="{Binding SelectedBox}"
                                        AutomationId="Card_Picker_Box"
                                        SemanticProperties.Hint="Choose which collection this card belongs to"
                                        HorizontalOptions="Fill" />
                                <Label Style="{StaticResource PickerIndicator}" />
                            </Grid>
```

to:

```xml
                            <Label Text="Collection" Style="{StaticResource FormLabel}" />
                            <shared:StyledPicker ItemsSource="{Binding AvailableBoxes}"
                                                 SelectedItem="{Binding SelectedBox}"
                                                 AutomationId="Card_Picker_Box"
                                                 SemanticHint="Choose which collection this card belongs to" />
```

- [ ] **Step 4: Verify Android build succeeds**

Run:
```bash
dotnet build PrayerApp/PrayerApp.csproj -f net10.0-android 2>&1 | tail -20
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 5: Run the existing xUnit suite — regression check**

Run:
```bash
(cd PrayerApp.Tests && dotnet test 2>&1) | tail -5
```

Expected: `Passed!  - Failed:     0, Passed:   729, Skipped:     4, Total:   733`.

- [ ] **Step 6: Commit the PrayerCardPage migration**

Run:
```bash
git add PrayerApp/Views/PrayerCard/PrayerCardPage.xaml
git commit -m "$(cat <<'EOF'
fix(cards): migrate PrayerCardPage Collection picker to StyledPicker (#35)

Adjacent finding from issue #35 triage — PrayerCardPage's Collection
picker had the same missing-Border-wrapper bug as ConfirmImportPage.
Migrating to <shared:StyledPicker> applies the same fix and the
emerging convention.

PrayerDetailPage's 4 working Picker call-sites are intentionally
deferred to a follow-up Issue (architect's call: soak the component
on 2 sites before migrating working ones with regression risk).
EOF
)"
```

---

## Task 6: Author the shared-components convention doc

**Files:**
- Create: `docs/conventions/shared-components.md`

- [ ] **Step 1: Write `docs/conventions/shared-components.md`**

Create file `docs/conventions/shared-components.md` with this exact content:

```markdown
# Shared Components — Authoring Convention

This document captures the convention for shared composite components in PrayerApp. It exists so future shared-component authors (including AI agents) don't re-litigate the design decisions documented in [docs/superpowers/specs/2026-05-12-styled-picker-design.md](../superpowers/specs/2026-05-12-styled-picker-design.md).

## When to author a shared component

When 3+ concrete call-sites in the codebase repeat the same composite XAML structure (a layout pattern wrapping one or more controls). Don't introduce a shared component on speculation — wait for the third use case.

## Location

`PrayerApp/Views/Shared/<ComponentName>.{xaml,xaml.cs}`

## Mechanism

`ContentView` subclass with `BindableProperty` declarations in code-behind.

Why ContentView (and not TemplatedView, Style, or a control subclass):

- **Style on the inner control** — cannot add wrapping or sibling elements (fundamental XAML constraint). Styles like `PickerField` (Border) and `PickerIndicator` (Label) are still authored in `Resources/Styles/Styles.xaml` and consumed inside the component's XAML; the shared-component layer is what composes them into a single call-site element.
- **TemplatedView + ControlTemplate** — saves one `BindableProperty` (`Content`) vs `ContentView`. Negligible. Diverges from the only shared-component precedent in this repo (`LoadingOverlay`). `TemplateBinding` *class* is `[Obsolete]` in MAUI 10; the markup form `{TemplateBinding}` is identical verbosity to `{x:Reference root}`.
- **Subclassing the inner control directly** (e.g., `StyledPicker : Picker`) — handler owns native rendering; adding chrome requires per-platform `PrependToMapping` / `AppendToMapping` customization. Higher complexity. Out of scope for this convention.

## Inner-binding pattern

- Set `x:Name="root"` on the component's `<ContentView>` root element.
- Inside the visual tree, bind inner controls to the component's BindableProperties via `{Binding <Property>, Source={x:Reference root}}`.

## API surface

Expose only the BindableProperties consumers need to set. Pass them through to the encapsulated inner control(s) via the binding pattern above.

Naming: prefer the inner control's own property names where they exist (e.g., `ItemsSource`, `SelectedItem`, `Title`). For attached properties that don't map directly to a bindable name, pick a clean alias (e.g., `SemanticHint` forwards to the inner control's `SemanticProperties.Hint`).

`AutomationId` is inherited from `Element` and should be re-bound through to the inner platform-native control so Appium UITest selectors keep working without consumer changes.

## Testing

`PrayerApp.Tests` is a `Microsoft.NET.Sdk` (non-MAUI) project and cannot instantiate XAML-backed `ContentView` subclasses (the auto-generated partial class is unavailable to the test project). xUnit unit tests for shared components are not infrastructure-feasible without test-project rework.

Regression net for shared components:

1. The existing xUnit suite stays green (currently 729 passing / 4 skipped).
2. `dotnet build PrayerApp/PrayerApp.csproj -f net10.0-android` succeeds.
3. Mac UAT confirms the visual chrome on each migrated call-site post-merge.

## Existing components following this convention

- `PrayerApp/Views/Shared/LoadingOverlay.{xaml,xaml.cs}` — wraps `ActivityIndicator` with a scrim; binds via `IsLoading`.
- `PrayerApp/Views/Shared/StyledPicker.{xaml,xaml.cs}` — wraps `Picker` with the `PickerField` Border chrome and `PickerIndicator` chevron; binds via `Title`, `ItemsSource`, `SelectedItem`, `ItemDisplayBinding`, `SemanticHint`.

## Forward-pointer

If `Entry`, `Editor`, or `Switch` acquire composite chrome that gets repeated in 3+ call-sites in the future, follow this same pattern: name them `StyledEntry`, `StyledEditor`, `StyledSwitch`; place in `PrayerApp/Views/Shared/`; use `ContentView` + BindableProperty code-behind. Don't build these on speculation; wait for the 3rd concrete use case.
```

- [ ] **Step 2: Commit the convention doc**

Run:
```bash
git add docs/conventions/shared-components.md
git commit -m "$(cat <<'EOF'
docs(conventions): shared-components authoring convention (#35)

Captures the convention for shared composite components in
PrayerApp/Views/Shared/. Documents the ContentView + BindableProperty
mechanism, rejected alternatives, inner-binding pattern, API-surface
principles, testing realities (xUnit can't instantiate XAML-backed
ContentViews in this repo's test setup), and the two existing
components following the convention (LoadingOverlay, StyledPicker).

Includes a forward-pointer paragraph for hypothetical future
StyledEntry / StyledEditor / StyledSwitch — build only at 3+ concrete
use cases per the staff role brief's anti-premature-abstraction rule.
EOF
)"
```

If the `/simplify` gate fires (markdown is exempt from /simplify per role brief), retry per the gate's documented release-on-retry behavior.

---

## Task 7: `/simplify` trio review

**Files:**
- Review-only across all changes on this branch (Tasks 2-6).

- [ ] **Step 1: Run the `/simplify` skill on the cumulative diff**

Invoke the `simplify` skill. It dispatches three `lead-qa-engineer` subagents in parallel (code-reuse, code-quality, efficiency) and aggregates findings.

- [ ] **Step 2: Address findings inline if they're real**

For each finding the trio surfaces:
- If it's a real issue, fix it in the relevant file(s) on this branch and commit the fix as a separate "fix(simplify): …" commit.
- If it's a false positive, skip it.

If `/simplify` itself fixes anything, the commit-gate may fire on the next commit — retry per gate's release-on-retry behavior.

- [ ] **Step 3: Re-run regression check after any fixes**

Run:
```bash
(cd PrayerApp.Tests && dotnet test 2>&1) | tail -5
```

Expected: `Passed!  - Failed:     0, Passed:   729, Skipped:     4, Total:   733`.

---

## Task 8: Push the branch

**Files:**
- (no files modified)

- [ ] **Step 1: Push the branch to origin**

Run:
```bash
git push -u origin fix/35-styled-picker-component
```

Expected: branch creates on origin, tracking established. URL printed.

- [ ] **Step 2: Verify commit history**

Run:
```bash
git --no-pager log --oneline dev..HEAD
```

Expected: 4 commits (Task 2/3 combined, Task 4, Task 5, Task 6), plus any `/simplify` fix commits. Order:

```
<sha> docs(conventions): shared-components authoring convention (#35)
<sha> fix(cards): migrate PrayerCardPage Collection picker to StyledPicker (#35)
<sha> fix(import): migrate ConfirmImportPage Collection picker to StyledPicker (#35)
<sha> feat(views): add StyledPicker shared component (#35)
```

---

## Task 9: Post #35 comment with branch link + Mac UAT instructions

**Files:**
- (no files modified locally; posts a comment on GitHub Issue #35)

- [ ] **Step 1: Write the #35 comment to a tempfile**

Create file `$env:TEMP/pp-issue-35-slice.md` (PowerShell) or `$USERPROFILE/AppData/Local/Temp/pp-issue-35-slice.md` (bash) with this content:

```markdown
**Implementation shipped on branch [`fix/35-styled-picker-component`](https://github.com/kenmulford/PracticingPrayer/tree/fix/35-styled-picker-component).**

This work introduces `StyledPicker` — a reusable `ContentView` shared component at `PrayerApp/Views/Shared/StyledPicker.{xaml,xaml.cs}` — and migrates the two broken Picker call-sites (`ConfirmImportPage`, `PrayerCardPage`) to use it. Also ships a shared-components authoring convention doc at `docs/conventions/shared-components.md`.

**Scope intentionally limited:**

- The 4 working Picker call-sites in `PrayerDetailPage.xaml` are NOT migrated in this branch. Per architect review, defer to a follow-up Issue so the new component soaks on 2 sites first.

**Design + plan artifacts** (committed to `dev`):

- Spec: `docs/superpowers/specs/2026-05-12-styled-picker-design.md`
- Plan: `docs/superpowers/plans/2026-05-12-styled-picker-implementation.md`

**Mac UAT — 4 paths to verify post-build:**

1. Build the iOS Debug target from this branch; install on iPhone 17 (or any iOS 26+ device).
2. Open the share-extension flow → import a `.prayercard` file → land on `ConfirmImportPage`. Verify: the "Collection" picker shows the inset chrome (rounded Border outline, chevron sits inside the border, no overhang).
3. Open any prayer card → tap "Edit" on the card itself → land on `PrayerCardPage`. Verify: the "Collection" picker shows the same inset chrome.
4. On both pages: tap the picker, confirm the picker modal opens, select a different collection, confirm the selection round-trips correctly.

After UAT confirms, squash-merge this branch to `dev`, delete the branch, and file the follow-up Issue: "Migrate PrayerDetailPage Pickers to StyledPicker" (covers the 4 working sites at lines 124-135, 179, 206, 226 of `PrayerDetailPage.xaml`).
```

- [ ] **Step 2: Post the comment**

Run (bash):
```bash
gh issue comment 35 --repo kenmulford/PracticingPrayer --body-file "$USERPROFILE/AppData/Local/Temp/pp-issue-35-slice.md"
```

Expected: URL of the new comment is printed.

- [ ] **Step 3: Clean up the tempfile**

Run:
```bash
rm "$USERPROFILE/AppData/Local/Temp/pp-issue-35-slice.md"
```

---

## After Mac UAT (out-of-band — not a plan step)

User runs Mac UAT post-merge. Then:

1. Squash-merge `fix/35-styled-picker-component` to `dev`, push `dev`, delete remote+local branch.
2. File follow-up Issue on PracticingPrayer: "Migrate PrayerDetailPage Pickers to StyledPicker" with the 4 specific lines (124-135, 179, 206, 226). Link from the convention doc's "Existing components following this convention" section if scope is broadened, otherwise just link from the Issue.
3. Close `#35` with reference to the merged commit SHA + the `closed-completed` label.

---

## Spec coverage self-review

| Spec section | Plan task |
|---|---|
| Component skeleton (XAML + code-behind) | Tasks 2 + 3 |
| Migration scope: ConfirmImportPage | Task 4 |
| Migration scope: PrayerCardPage | Task 5 |
| Migration scope: PrayerDetailPage DEFERRED | Documented in #35 comment (Task 9) + follow-up Issue out-of-band |
| Convention doc | Task 6 |
| Testing (xUnit RED first) | **Reframed** — xUnit not infrastructure-feasible for this component (test project is non-MAUI). Documented in plan header, convention doc, and #35 comment. Regression net = existing 729 tests + Mac UAT. |
| Risks (trim/AOT, hot-reload, Mac UAT, two-way, Title color) | Trim/AOT + hot-reload validated implicitly by `dotnet build`. Mac UAT enumerated in #35 comment. Two-way + Title color documented in spec; not action items. |
| Success criteria | All covered by tasks above + UAT step. |

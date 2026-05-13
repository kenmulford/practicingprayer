# StyledPicker — Design Spec

- **Date:** 2026-05-12
- **Issue:** https://github.com/kenmulford/PracticingPrayer/issues/35
- **Reviewed by:** Senior Frontend Developer subagent + Digital Solutions Architect subagent (2026-05-12)
- **Status:** Design locked; ready for implementation plan

## Problem

`ConfirmImportPage.xaml:38-45` and `PrayerCardPage.xaml:40-48` host Pickers that lack the established composite chrome — they're missing the `<Border Style="PickerField">` wrapper that the 4 working Picker sites in `PrayerDetailPage.xaml` use. The visible symptom is a chevron that overhangs the right edge of the Picker frame instead of being inset (Issue #35). The structural symptom is duplicated `Border + Grid + Picker + Label` boilerplate at every Picker call-site that the compiler can't enforce — the bug fails open (missing wrapper renders working-but-wrong, not a compile error).

Issue #35 names ConfirmImportPage. PrayerCardPage exhibits the same bug (found during triage; same root cause; bundled into this work).

## Decision

Introduce a single shared component, `StyledPicker`, that encapsulates the composite chrome. Migrate the 2 broken sites to use it. Document the shared-components convention so future similar components (StyledEntry / StyledEditor / StyledSwitch, if/when 3+ use cases for any of them emerge) follow the same shape.

### Mechanism — `ContentView` subclass

`PrayerApp/Views/Shared/StyledPicker.{xaml,xaml.cs}` — a `ContentView` subclass with `BindableProperty` declarations in code-behind, inner-Picker bindings via `{x:Reference root}`. Matches the existing `LoadingOverlay` pattern 1:1 (only shared-component precedent in this repo).

**Rejected alternatives (with evidence):**

| Alternative | Why rejected |
|---|---|
| XAML `<Style>` on Picker | Styles can only set properties on a single control; cannot add wrapping or sibling elements. Fundamental XAML constraint (not MAUI-specific). |
| `<ControlTemplate>` on Picker | `Picker : View` (not `TemplatedView`); has no `ControlTemplate` property in MAUI 10. Verified against [Picker class docs](https://learn.microsoft.com/dotnet/api/microsoft.maui.controls.picker?view=net-maui-10.0). |
| `TemplatedView` subclass + `ControlTemplate` resource | Saves one bindable property (`Content`) vs `ContentView`. Negligible. Diverges from the only shared-component precedent in the repo. `TemplateBinding` class is `[Obsolete]` in MAUI 10 anyway — the verbosity edge I claimed earlier doesn't exist. |
| Subclass `Picker` directly | `PickerHandler` owns native rendering; adding chrome would require per-platform handler-mapping customization (`PrependToMapping` / `AppendToMapping`). Higher complexity, out of scope. |

The closest MS-official idiom for this shape of component is the [`CardView` ContentView tutorial](https://learn.microsoft.com/dotnet/maui/user-interface/controls/contentview?view=net-maui-10.0#create-a-custom-control). CommunityToolkit.Maui has no equivalent component (verified against current toolkit view list).

### Component skeleton

**`PrayerApp/Views/Shared/StyledPicker.xaml`:**

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

**`PrayerApp/Views/Shared/StyledPicker.xaml.cs`:**

```csharp
using System.Collections;

namespace PrayerApp.Views.Shared;

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

    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public IList? ItemsSource { get => (IList?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public BindingBase? ItemDisplayBinding { get => (BindingBase?)GetValue(ItemDisplayBindingProperty); set => SetValue(ItemDisplayBindingProperty, value); }
    public string? SemanticHint { get => (string?)GetValue(SemanticHintProperty); set => SetValue(SemanticHintProperty, value); }

    public StyledPicker() => InitializeComponent();
}
```

Note: `AutomationId` is inherited from `Element`; no need to redeclare. We bind the *inner* Picker's `AutomationId` to the *component's* `AutomationId` so consumers can keep specifying `AutomationId="ConfirmImport_Picker_Box"` on the `<shared:StyledPicker>` and the value propagates to the platform-native control for Appium discovery.

## Migration scope

| File | Sites | This work? |
|---|---|---|
| `ConfirmImportPage.xaml:38-45` (Collection picker) | 1 | ✅ migrate |
| `PrayerCardPage.xaml:40-48` (Collection picker) | 1 | ✅ migrate (adjacent finding, same bug as Issue #35) |
| `PrayerDetailPage.xaml` (4 Picker sites) | 4 | ❌ defer to follow-up Issue — currently working, zero user win, real regression risk on solo-dev cadence |

After this work lands: file follow-up Issue "Migrate PrayerDetailPage Pickers to StyledPicker."

## Convention doc

Author `docs/conventions/shared-components.md`. Contents:

- **Location:** `PrayerApp/Views/Shared/<ComponentName>.{xaml,xaml.cs}`
- **Mechanism:** `ContentView` subclass with `BindableProperty` declarations in code-behind
- **Inner-binding pattern:** `x:Name="root"` on the component's `ContentView`, inner bindings via `{Binding <Prop>, Source={x:Reference root}}`
- **API surface principle:** expose only the bindable properties consumers need to set; pass them through to the encapsulated inner control(s)
- **Existing components following this convention:** `LoadingOverlay`, `StyledPicker`
- **Forward-pointer paragraph:** if Entry, Editor, or Switch acquire composite chrome in the future, follow this same pattern. Build only when 3+ concrete use cases exist for any of them (per Staff SWE role brief's anti-premature-abstraction rule).

## Testing

Per `feedback_tdd_first_for_new_behavior` (universal scope) — RED test first.

**xUnit tests** at `PrayerApp.Tests/Views/Shared/StyledPickerTests.cs`:

1. **Construction** — instantiate `new StyledPicker()`, verify no exception.
2. **BindableProperty round-trip** — for each of `Title`, `ItemsSource`, `SelectedItem`, `ItemDisplayBinding`, `SemanticHint`: set the property on the component, query `GetValue` for the corresponding `BindableProperty`, verify the value matches.
3. **Inner-Picker reflection** (if practical without instantiating the visual tree) — verify the inner Picker's bound properties reflect the component's. May require either visual-tree walking or deferring to integration-style verification.

Mac UAT covers the visual chrome (chevron inset, single chevron, no overhang) — out of xUnit scope.

## Risks

Per frontend-dev review:

1. **Trim/AOT (.NET 10):** `ItemDisplayBinding` passthrough with `BindingBase` typing is safe. Avoid `OnPlatform` markup extension inside `StyledPicker.xaml` (not trim-safe); use `OnPlatform<T>` if needed later.
2. **XAML Hot Reload:** handles BindableProperty additions reliably. Renaming or retyping a property requires a full rebuild — lock the property surface before merge.
3. **iOS Mac UAT:** native Picker chrome (`UITextField` + action sheet on tap) renders inside the Border. Manual `▼` Label sits next to native control — verify visually that the result reads as a single inset chevron, not two.
4. **`SelectedItem` two-way default:** matches the implicit two-way behavior the 4 working PrayerDetailPage sites already rely on. No behavior change for those sites; the migrated sites get the same.
5. **`Title` placeholder color:** not exposed in this version. If a future design pass needs it, add as a passthrough property at that time.

## Out of scope

- Migration of PrayerDetailPage's 4 Picker call-sites (follow-up Issue after this work soaks)
- `StyledEntry`, `StyledEditor`, `StyledSwitch` (speculation; build when 3+ concrete use cases for any of them exist)
- OnPlatform-conditional chevron (skip on Android M3 / Windows where native draws its own) — no evidence yet that the redundant `▼` causes a visual problem on those platforms
- Handler-level customization of the native Picker control (`PrependToMapping` / `AppendToMapping`)
- Refactor or rename of `PickerField` / `PickerIndicator` styles — they remain in `Styles.xaml` and are consumed by `StyledPicker.xaml` directly

## Success criteria

1. `PrayerApp/Views/Shared/StyledPicker.xaml` + `.xaml.cs` exist and compile
2. xUnit `StyledPickerTests` is green
3. `ConfirmImportPage.xaml` Collection picker uses `<shared:StyledPicker>`; original Border+Grid+Picker+Label boilerplate removed
4. `PrayerCardPage.xaml` Collection picker likewise migrated
5. `docs/conventions/shared-components.md` exists with the contents above
6. Existing xUnit suite stays green (Slice 1 baseline: 729 passing / 4 skipped / 0 failed)
7. Mac UAT post-merge confirms chevron is visibly inset on both pages on iOS, no overhang, no visual duplication
8. Follow-up Issue filed for PrayerDetailPage migration; linked from convention doc

## Implementation order (high-level — detailed plan to follow via writing-plans skill)

1. Create `StyledPicker.{xaml,xaml.cs}` with the BindableProperty surface above
2. Write `StyledPickerTests.cs` (RED — should fail until component compiles)
3. Verify tests pass; component compiles
4. Migrate `ConfirmImportPage.xaml` Collection picker call-site
5. Migrate `PrayerCardPage.xaml` Collection picker call-site
6. Author `docs/conventions/shared-components.md`
7. Run full xUnit suite — confirm 729 + new tests, all passing
8. `/simplify` trio review
9. Commit + push branch `fix/35-styled-picker-component`
10. Post #35 comment with branch link + Mac UAT instructions
11. Mac UAT (Ken) → squash-merge to `dev` → file follow-up Issue for PrayerDetailPage migration → close #35

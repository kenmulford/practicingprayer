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

- Set `x:Name="root"` on the component's `<ContentView>` root element. (`LoadingOverlay` uses `x:Name="overlayRoot"` as a pre-convention legacy — leave it; new components use `root`.)
- Declare `xmlns:local="clr-namespace:PrayerApp.Views.Shared"` in the component's XAML namespace block.
- Bind inner controls to the component's BindableProperties via:

  ```xml
  {Binding <Property>, Source={x:Reference root}, x:DataType=local:<ComponentName>}
  ```

  The `x:DataType` annotation is required to keep compiled bindings warning-clean under MAUI 10's `MauiEnableXamlCBindingWithSourceCompilation=true` (project default). Without it, XC0023 fires per memory `2026-04-16_feedback_xaml_binding_warnings.md`.

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

- `PrayerApp/Views/Shared/LoadingOverlay.{xaml,xaml.cs}` — wraps `ActivityIndicator` with a scrim; binds via `IsLoading`. Uses `x:Name="overlayRoot"` (pre-convention legacy; the `root` naming is the standard for new components).
- `PrayerApp/Views/Shared/StyledPicker.{xaml,xaml.cs}` — wraps `Picker` with the `PickerField` Border chrome and `PickerIndicator` chevron; binds via `Title`, `ItemsSource`, `SelectedItem`, `ItemDisplayBinding`, `SemanticHint`.

## Forward-pointer

If `Entry`, `Editor`, or `Switch` acquire composite chrome that gets repeated in 3+ call-sites in the future, follow this same pattern: name them `StyledEntry`, `StyledEditor`, `StyledSwitch`; place in `PrayerApp/Views/Shared/`; use `ContentView` + BindableProperty code-behind. Don't build these on speculation; wait for the 3rd concrete use case.

using Microsoft.Maui.Controls;

namespace PrayerApp.Behaviors;

/// <summary>
/// Attached property that watches a string source and calls
/// <see cref="VisualStateManager.GoToState"/> on the host view when it changes.
/// Drives a <c>VisualStateGroup</c> from a bound VM property — keeps state cues
/// a function of bound state instead of imperative trigger closures.
/// </summary>
/// <remarks>
/// Introduced for issue #33 P1A — migrating <c>IsHighlighted</c> /
/// <c>IsMultiSelected</c> DataTriggers on the prayer-card border to a shared
/// <c>CardStates</c> VSM group. Generic; can drive any VSM group on any
/// <see cref="VisualElement"/>.
/// </remarks>
public static class VisualStateBindingBehavior
{
    public static readonly BindableProperty SourceProperty =
        BindableProperty.CreateAttached(
            propertyName: "Source",
            returnType: typeof(string),
            declaringType: typeof(VisualStateBindingBehavior),
            defaultValue: null,
            defaultBindingMode: BindingMode.OneWay,
            propertyChanged: OnSourceChanged);

    public static string? GetSource(BindableObject view) =>
        (string?)view.GetValue(SourceProperty);

    public static void SetSource(BindableObject view, string? value) =>
        view.SetValue(SourceProperty, value);

    private static void OnSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is VisualElement element && newValue is string state && !string.IsNullOrEmpty(state))
        {
            VisualStateManager.GoToState(element, state);
        }
    }
}

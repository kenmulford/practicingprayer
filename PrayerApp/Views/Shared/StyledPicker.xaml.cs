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
        BindableProperty.Create(nameof(ItemDisplayBinding), typeof(BindingBase), typeof(StyledPicker),
            propertyChanged: OnItemDisplayBindingChanged);

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

    // Forward ItemDisplayBinding to the inner Picker only when the consumer supplied one.
    // Picker.ItemDisplayBinding is a plain CLR property (not a BindableProperty), so we
    // assign directly. Forwarding it unconditionally via XAML caused empty rows whenever
    // consumers (ConfirmImportPage, PrayerCardPage) left it null and relied on ToString().
    static void OnItemDisplayBindingChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not StyledPicker self || self.InnerPicker is null)
            return;

        self.InnerPicker.ItemDisplayBinding = newValue as BindingBase;
    }
}

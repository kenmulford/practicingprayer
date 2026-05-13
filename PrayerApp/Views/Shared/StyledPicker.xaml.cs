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

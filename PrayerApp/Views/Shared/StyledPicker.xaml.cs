using System.Collections;

namespace PrayerApp.Views.Shared;

/// <summary>
/// Reusable styled Picker with the app's inset-chrome convention.
/// Renders the inner Picker alongside a decorative <c>PickerIndicator</c>
/// chevron. Consumers set BindableProperty values on this component; they
/// pass through to the encapsulated Picker via {x:Reference root} bindings
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

    /// <summary>
    /// Per-item display binding for the inner Picker. Mirrors
    /// <see cref="Picker.ItemDisplayBinding"/>: a plain CLR property of type
    /// <see cref="BindingBase"/>, NOT a BindableProperty.
    ///
    /// Why not a BindableProperty: when XAML markup <c>{Binding Title, x:DataType=...}</c>
    /// targets a BindableProperty, the parser treats it as "wire a binding to this
    /// property" — it sets up a binding from the consumer's BindingContext path
    /// 'Title' INTO the property, instead of passing the Binding object as a literal
    /// value. With no 'Title' on the consumer page's view-model, the inner Picker
    /// receives null and falls back to <c>object.ToString()</c>, rendering type names
    /// like "PrayerApp.Models.PrayerCard" in the dropdown.
    ///
    /// Picker.ItemDisplayBinding sidesteps this by being a plain CLR property: with no
    /// BindableProperty machinery, the XAML parser passes the Binding instance literally.
    /// This shim mirrors that exactly so consumers can use the same XAML shape they
    /// would on a raw Picker.
    /// </summary>
    public BindingBase? ItemDisplayBinding
    {
        get => InnerPicker?.ItemDisplayBinding;
        set
        {
            if (InnerPicker is not null)
                InnerPicker.ItemDisplayBinding = value;
        }
    }

    public string? SemanticHint
    {
        get => (string?)GetValue(SemanticHintProperty);
        set => SetValue(SemanticHintProperty, value);
    }

    /// <summary>
    /// Horizontal alignment of the inner Picker's selected-item text. Plain
    /// CLR property (NOT a BindableProperty) so XAML literal values like
    /// <c>HorizontalTextAlignment="End"</c> pass through as-is rather than
    /// being interpreted as a binding source path. Defaults to
    /// <see cref="TextAlignment.Start"/> to preserve label-above call sites
    /// (e.g., PrayerDetailPage); inset-grouped disclosure rows
    /// (ConfirmImportPage / PrayerCardPage) opt into <c>End</c> for the
    /// iOS HIG Settings.app pattern.
    /// </summary>
    public TextAlignment HorizontalTextAlignment
    {
        get => InnerPicker?.HorizontalTextAlignment ?? TextAlignment.Start;
        set
        {
            if (InnerPicker is not null)
                InnerPicker.HorizontalTextAlignment = value;
        }
    }

    public StyledPicker()
    {
        InitializeComponent();
    }
}

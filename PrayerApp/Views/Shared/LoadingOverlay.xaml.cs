namespace PrayerApp.Views.Shared;

/// <summary>
/// Reusable list-page loading indicator. Wraps an <see cref="ActivityIndicator"/>
/// styled with the <c>SaveActivityIndicator</c> resource and exposes a single
/// bindable <see cref="IsLoading"/> property. Drop in as the LAST child of a
/// list page's root layout so it z-orders above the CollectionView and stays
/// visible during refresh on populated lists.
/// </summary>
public partial class LoadingOverlay : ContentView
{
    public static readonly BindableProperty IsLoadingProperty = BindableProperty.Create(
        nameof(IsLoading),
        typeof(bool),
        typeof(LoadingOverlay),
        defaultValue: false);

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public LoadingOverlay()
    {
        InitializeComponent();
    }
}

using PrayerApp.Helpers;
using PrayerApp.ViewModels;

namespace PrayerApp.Views.Boxes;

public partial class BoxesPage : ContentPage
{
    private readonly BoxesViewModel _vm;

    public BoxesPage(BoxesViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    private void OnBackgroundTapped(object? sender, TappedEventArgs e)
        => _vm.DeselectAll();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await PageSync.OnAppearingAsync(_vm);
    }
}

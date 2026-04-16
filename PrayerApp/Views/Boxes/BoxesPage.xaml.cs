using PrayerApp.ViewModels;

namespace PrayerApp.Views.Boxes;

public partial class BoxesPage : ContentPage
{
    private readonly BoxesViewModel _vm;
    private bool _loaded;

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
        await App.InitTask;

        if (!_loaded)
        {
            _loaded = true;
            await _vm.LoadAsync();
        }
        else
        {
            await _vm.RefreshAsync();
        }
    }
}

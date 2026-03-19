using PrayerApp.ViewModels;

namespace PrayerApp.Views.PrayerCard;

public partial class PrayerCardsPage : ContentPage
{
    private bool _loaded;

    public PrayerCardsPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_loaded && BindingContext is PrayerCardsViewModel vm)
        {
            _loaded = true;
            await vm.LoadAsync();
        }
    }

    private void ContentPage_NavigatedTo(object sender, NavigatedToEventArgs e)
    {
    }
}

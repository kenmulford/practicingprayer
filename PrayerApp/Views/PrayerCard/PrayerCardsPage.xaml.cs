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
        if (BindingContext is PrayerCardsViewModel vm)
        {
            if (!_loaded)
            {
                _loaded = true;
                await vm.LoadAsync();
            }
            else
            {
                // Subsequent visits — refresh data that may have changed on other tabs
                // (e.g. prayers added via QuickAdd on home page)
                await vm.RefreshAsync();
            }
        }
    }

    private void ContentPage_NavigatedTo(object sender, NavigatedToEventArgs e)
    {
    }
}

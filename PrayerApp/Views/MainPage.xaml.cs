using PrayerApp.ViewModels;

namespace PrayerApp.Views;

public partial class MainPage : ContentPage
{
    private readonly HomeViewModel _homeViewModel;

    public MainPage()
    {
        InitializeComponent();

        _homeViewModel = new HomeViewModel();
        BindingContext = _homeViewModel;

        BtnQuickAdd.Clicked += async (s, e) =>
            await Shell.Current.Navigation.PushModalAsync(new QuickAddPage());

        BtnPrayerTime.Clicked += async (s, e) =>
        {
            var action = await DisplayActionSheetAsync("Prayer Time", "Cancel", null, "All Requests", "By Tags");
            if (action == "All Requests")
                await Shell.Current.GoToAsync($"{nameof(PrayerTime.PrayerTimePage)}?scope=all");
            else if (action == "By Tags")
                await Shell.Current.Navigation.PushModalAsync(new PrayerTime.PrayerTimeScopePage());
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _homeViewModel.LoadAsync();
    }
}

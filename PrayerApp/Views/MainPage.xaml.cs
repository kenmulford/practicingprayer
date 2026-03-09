namespace PrayerApp.Views;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BtnQuickAdd.Clicked += async (s, e) =>
            await Shell.Current.Navigation.PushModalAsync(new QuickAddPage());

        BtnPrayerTime.Clicked += async (s, e) =>
        {
            var action = await DisplayActionSheet("Prayer Time", "Cancel", null, "All Requests", "By Tags");
            if (action == "All Requests")
                await Shell.Current.GoToAsync($"{nameof(PrayerTime.PrayerTimePage)}?scope=all");
            else if (action == "By Tags")
                await Shell.Current.Navigation.PushModalAsync(new PrayerTime.PrayerTimeScopePage());
        };
    }
}

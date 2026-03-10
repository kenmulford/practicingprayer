using AppSettings = PrayerApp.Services.Settings;

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

        // First launch: ask for the user's name
        if (!AppSettings.UserNameSet)
        {
            await PromptForNameAsync();
        }

        UpdateGreeting();
    }

    private async Task PromptForNameAsync()
    {
        var name = await DisplayPromptAsync(
            title: "Welcome!",
            message: "What's your name?",
            accept: "Let's go",
            cancel: "Skip",
            placeholder: "Enter your name",
            maxLength: 50,
            keyboard: Keyboard.Text);

        // Mark as set regardless of whether they provided a name or skipped
        AppSettings.UserNameSet = true;
        AppSettings.UserName = name?.Trim() ?? string.Empty;
    }

    private void UpdateGreeting()
    {
        var name = AppSettings.UserName;
        var hour = DateTime.Now.Hour;

        var timeGreeting = hour switch
        {
            >= 5 and < 12  => "Good morning",
            >= 12 and < 17 => "Good afternoon",
            >= 17 and < 21 => "Good evening",
            _              => "Good night"
        };

        LblGreeting.Text = string.IsNullOrWhiteSpace(name)
            ? $"{timeGreeting}!"
            : $"{timeGreeting}, {name}!";
    }
}

namespace PrayerApp.Views;

public partial class Settings : ContentPage
{
    // TODO: Replace with the hosted privacy policy URL before publishing
    private const string PrivacyPolicyUrl = "https://example.com/privacy";

	public Settings()
	{
		InitializeComponent();
        // Removed: manual OnAppearing() call — MAUI invokes it automatically on page appearance
        TapPrivacyPolicy.Tapped += async (_, _) => await Launcher.OpenAsync(PrivacyPolicyUrl);
	}

    protected override void OnAppearing()
    {
        base.OnAppearing();
        chkSettingsAllowNotifications.IsToggled = PrayerApp.Services.Settings.AllowNotifications;
    }

    private async void btnClearSettings_Clicked(object sender, EventArgs e)
    {
        PrayerApp.Services.Settings.ClearSettings();
        await DisplayAlertAsync("Settings Cleared", "The next time the app runs all DB info will be reset.", "OK");
    }

    private void chkSettingsAllowNotifications_Toggled(object sender, ToggledEventArgs e)
    {
        PrayerApp.Services.Settings.AllowNotifications = e.Value;
    }
}
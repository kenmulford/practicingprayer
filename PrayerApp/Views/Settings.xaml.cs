namespace PrayerApp.Views;

public partial class Settings : ContentPage
{
	public Settings()
	{
		InitializeComponent();
	}

    private void btnClearSettings_Clicked(object sender, EventArgs e)
    {
        PrayerApp.Services.Settings.ClearSettings();
        DisplayAlertAsync("Settings Cleared", "The next time the app runs all DB info will be reset.", "OK");
    }
}
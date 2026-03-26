namespace PrayerApp.Views.Settings;

public partial class AboutPage : ContentPage
{
    private const string PrivacyPolicyUrl = "https://practicingprayerapp.com/privacy";
    private const string WebsiteUrl = "https://practicingprayerapp.com";

    public AboutPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        lblVersion.Text = $"Version {AppInfo.Current.VersionString} (build {AppInfo.Current.BuildString})";
    }

    private async void OnPrivacyClicked(object? sender, EventArgs e)
        => await Launcher.OpenAsync(PrivacyPolicyUrl);

    private async void OnWebsiteClicked(object? sender, EventArgs e)
        => await Launcher.OpenAsync(WebsiteUrl);
}

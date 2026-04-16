namespace PrayerApp.Views.Settings;

public partial class SettingsHubPage : ContentPage
{
    public SettingsHubPage()
    {
        InitializeComponent();
    }

    private async void OnAppSettingsTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(AppSettingsPage));

    private async void OnCollectionsTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(Routes.BoxesPage);

    private async void OnBackupTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(BackupPage));

    private async void OnAboutTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(AboutPage));

    private async void OnHelpTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(HelpPage));
}

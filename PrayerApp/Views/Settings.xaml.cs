using PrayerApp.Services;

namespace PrayerApp.Views;

public partial class Settings : ContentPage
{
    private const string PrivacyPolicyUrl = "https://practicingprayerapp.com/privacy";

    private readonly IBackupService _backupService;
    private readonly IDiagnosticLog _diagnosticLog;

    public Settings()
    {
        InitializeComponent();
        _backupService = IPlatformApplication.Current!.Services.GetRequiredService<IBackupService>();
        _diagnosticLog = IPlatformApplication.Current!.Services.GetRequiredService<IDiagnosticLog>();
        TapPrivacyPolicy.Tapped += async (_, _) => await Launcher.OpenAsync(PrivacyPolicyUrl);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        chkSettingsAllowNotifications.IsToggled = PrayerApp.Services.Settings.AllowNotifications;

        var logPath = _diagnosticLog.GetLogPath();
        btnSendDiagnostics.IsVisible = File.Exists(logPath) && new FileInfo(logPath).Length > 0;
    }

    private void chkSettingsAllowNotifications_Toggled(object sender, ToggledEventArgs e)
    {
        PrayerApp.Services.Settings.AllowNotifications = e.Value;
    }

    private async void btnBackup_Clicked(object sender, EventArgs e)
    {
        btnBackup.IsEnabled = false;
        try
        {
            await _backupService.ExportAsync();
        }
        finally
        {
            btnBackup.IsEnabled = true;
        }
    }

    private async void btnSendDiagnostics_Clicked(object sender, EventArgs e)
    {
        var logPath = _diagnosticLog.GetLogPath();
        if (!File.Exists(logPath)) return;

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = "Diagnostic Log",
            File = new ShareFile(logPath)
        });
    }

    private async void btnRestore_Clicked(object sender, EventArgs e)
    {
        bool confirmed = await DisplayAlertAsync(
            "Restore Backup",
            "This will permanently replace all your current prayer data. This cannot be undone. Continue?",
            "Restore",
            "Cancel");

        if (!confirmed) return;

        btnRestore.IsEnabled = false;
        bool success = await _backupService.ImportAsync();
        // On success the app navigates to //MainPage; only re-enable on failure
        if (!success) btnRestore.IsEnabled = true;
    }
}

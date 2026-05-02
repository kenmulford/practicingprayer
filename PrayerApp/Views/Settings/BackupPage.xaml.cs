using PrayerApp.Services;

namespace PrayerApp.Views.Settings;

public partial class BackupPage : ContentPage
{
    private readonly IBackupService _backupService;

    public BackupPage(IBackupService backupService)
    {
        InitializeComponent();
        _backupService = backupService;
    }

    private async void OnBackupClicked(object? sender, EventArgs e)
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

    private async void OnRestoreClicked(object? sender, EventArgs e)
    {
        bool confirmed = await DisplayAlertAsync(
            "Restore Backup",
            "This will permanently replace all your current prayer data. This cannot be undone. Continue?",
            "Restore",
            "Cancel");

        if (!confirmed) return;

        btnRestore.IsEnabled = false;
        bool success = await _backupService.ImportAsync();
        if (!success) btnRestore.IsEnabled = true;
    }
}

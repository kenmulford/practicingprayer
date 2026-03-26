using PrayerApp.Services;

namespace PrayerApp.Views.Settings;

public partial class BackupPage : ContentPage
{
    private readonly IBackupService _backupService;
    private readonly IDiagnosticLog _diagnosticLog;

    public BackupPage(IBackupService backupService, IDiagnosticLog diagnosticLog)
    {
        InitializeComponent();
        _backupService = backupService;
        _diagnosticLog = diagnosticLog;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var logPath = _diagnosticLog.GetLogPath();
        diagnosticsSection.IsVisible = File.Exists(logPath) && new FileInfo(logPath).Length > 0;
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

    private async void OnSendDiagnosticsClicked(object? sender, EventArgs e)
    {
        var logPath = _diagnosticLog.GetLogPath();
        if (!File.Exists(logPath)) return;

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = "Diagnostic Log",
            File = new ShareFile(logPath)
        });
    }
}

using PrayerApp.Services;

namespace PrayerApp.Views.Settings;

public partial class AboutPage : ContentPage
{
    private const string PrivacyPolicyUrl = "https://practicingprayerapp.com/privacy";
    private const string WebsiteUrl = "https://practicingprayerapp.com";

    private readonly IDiagnosticLog _diagnosticLog;

    public AboutPage(IDiagnosticLog diagnosticLog)
    {
        InitializeComponent();
        _diagnosticLog = diagnosticLog;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        lblVersion.Text = $"Version {AppInfo.Current.VersionString} (build {AppInfo.Current.BuildString})";

        var logPath = _diagnosticLog.GetLogPath();
        btnSendDiagnostics.IsVisible = File.Exists(logPath) && new FileInfo(logPath).Length > 0;
    }

    private async void OnPrivacyClicked(object? sender, EventArgs e)
        => await Launcher.OpenAsync(PrivacyPolicyUrl);

    private async void OnWebsiteClicked(object? sender, EventArgs e)
        => await Launcher.OpenAsync(WebsiteUrl);

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

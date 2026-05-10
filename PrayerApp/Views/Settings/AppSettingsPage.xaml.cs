using PrayerApp.Services;
using PrayerApp.Views;

namespace PrayerApp.Views.Settings;

public partial class AppSettingsPage : ContentPage
{
    public AppSettingsPage()
    {
        InitializeComponent();
#if DEBUG
        DeveloperSection.IsVisible = true;
#endif
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        chkAllowNotifications.IsToggled = PrayerApp.Services.Settings.AllowNotifications;
        entryOverdueThreshold.Text = PrayerApp.Services.Settings.OverdueDayThreshold.ToString();
        chkLandscapeMode.IsToggled = PrayerApp.Services.Settings.PrayerTimeLandscape;
        timePickerDefaultNotify.Time = new TimeSpan(
            PrayerApp.Services.Settings.DefaultNotifyHour,
            PrayerApp.Services.Settings.DefaultNotifyMinute, 0);
    }

    private async void OnAllowNotificationsToggled(object? sender, ToggledEventArgs e)
    {
        if (!e.Value)
        {
            PrayerApp.Services.Settings.AllowNotifications = false;
            return;
        }

        var notificationService = IPlatformApplication.Current!.Services.GetRequiredService<INotificationService>();
        var granted = await notificationService.RequestPermissionAsync();
        PrayerApp.Services.Settings.AllowNotifications = granted;
        if (!granted)
            chkAllowNotifications.IsToggled = false;
    }

    private void OnDefaultTimeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TimePicker.Time)) return;
        if (timePickerDefaultNotify.Time is not { } time) return;
        PrayerApp.Services.Settings.DefaultNotifyHour = time.Hours;
        PrayerApp.Services.Settings.DefaultNotifyMinute = time.Minutes;
    }

    private void OnOverdueThresholdChanged(object? sender, TextChangedEventArgs e)
    {
        if (int.TryParse(e.NewTextValue, out var days) && days >= 1)
            PrayerApp.Services.Settings.OverdueDayThreshold = days;
    }

    private void OnLandscapeModeToggled(object? sender, ToggledEventArgs e)
        => PrayerApp.Services.Settings.PrayerTimeLandscape = e.Value;

    private void OnBackgroundTapped(object? sender, TappedEventArgs e)
    {
        if (entryOverdueThreshold.IsFocused)
            entryOverdueThreshold.Unfocus();
    }

    private void OnStageSamplePayloadClicked(object? sender, EventArgs e)
    {
#if DEBUG
        // Mirror the Slice 2 (Android intent) and Slice 3 (iOS extension)
        // platform-layer dispatch entry point so the smoke path is
        // structurally identical to production, even though this click is
        // already on the UI thread.
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var services = IPlatformApplication.Current!.Services;
            services.GetRequiredService<IImportPayloadService>().StagePayload(
                "1. Pray for Mom\n" +
                "2. Pray for Dad\n" +
                "3. Pray for Sis");
            await services.GetRequiredService<INavigationService>()
                .PushModalWithNavigationBarAsync(
                    services.GetRequiredService<ConfirmImportPage>());
        });
#endif
    }

    private void OnStageParserRegressionPayloadClicked(object? sender, EventArgs e)
    {
#if DEBUG
        // Regression payload for the blank-line block-splitting fix
        // (TextSelectionParser, 2026-05-10). Pre-fix: 6 prayers (one per
        // non-empty line). Post-fix: 3 prayers (Jim/Frank/John each with a
        // name title and folded details). The Appium test
        // `ImportFlowTests.Import_BlankLineBlocks_StagedPayload_ProducesThreePrayers`
        // taps this button and asserts the 3-prayer outcome.
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var services = IPlatformApplication.Current!.Services;
            services.GetRequiredService<IImportPayloadService>().StagePayload(
                "Jim\n" +
                "Looking for a new job, praying for interviews this week.\n" +
                "\n" +
                "Frank\n" +
                "Wife is due this week with their third child. Praying for safe delivery!\n" +
                "\n" +
                "John\n" +
                "Work has been so busy he hasn't been focused on his family. He needs strength to work toward better work/life balance.");
            await services.GetRequiredService<INavigationService>()
                .PushModalWithNavigationBarAsync(
                    services.GetRequiredService<ConfirmImportPage>());
        });
#endif
    }
}

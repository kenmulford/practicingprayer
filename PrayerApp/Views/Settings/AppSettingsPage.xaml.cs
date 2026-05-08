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
}

using PrayerApp.Services;

namespace PrayerApp.Views.Settings;

public partial class AppSettingsPage : ContentPage
{
    public AppSettingsPage()
    {
        InitializeComponent();
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

    private void OnAllowNotificationsToggled(object? sender, ToggledEventArgs e)
        => PrayerApp.Services.Settings.AllowNotifications = e.Value;

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
}

using PrayerApp.Services;

namespace PrayerApp.Views.PrayerTime;

public partial class PrayerTimePage : ContentPage
{
    private readonly IOrientationService _orientationService;

    public PrayerTimePage()
    {
        InitializeComponent();
        _orientationService = IPlatformApplication.Current!.Services.GetRequiredService<IOrientationService>();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _orientationService.LockLandscape();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _orientationService.Unlock();
    }
}

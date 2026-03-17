using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Views.PrayerTime;

public partial class PrayerTimePage : ContentPage
{
    private readonly IOrientationService _orientationService;
    private readonly IOnboardingService _onboardingService;

    public PrayerTimePage()
    {
        InitializeComponent();
        _orientationService = IPlatformApplication.Current!.Services.GetRequiredService<IOrientationService>();
        _onboardingService = IPlatformApplication.Current!.Services.GetRequiredService<IOnboardingService>();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _orientationService.LockLandscape();
        _onboardingService.Advance(); // PrayerTime → PrayerTimeActive

        // Pause auto-mode when the window is backgrounded; resume when it returns
        if (Window is not null)
        {
            Window.Stopped  += OnWindowStopped;
            Window.Resumed  += OnWindowResumed;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _orientationService.LockPortrait();

        // Unsubscribe window lifecycle events
        if (Window is not null)
        {
            Window.Stopped  -= OnWindowStopped;
            Window.Resumed  -= OnWindowResumed;
        }

        // Stop auto-mode fully when leaving the page
        if (BindingContext is PrayerTimeViewModel vm)
            vm.StopAutoMode();
    }

    private void OnWindowStopped(object? sender, EventArgs e)
    {
        if (BindingContext is PrayerTimeViewModel vm)
            vm.PauseAutoMode();
    }

    private void OnWindowResumed(object? sender, EventArgs e)
    {
        if (BindingContext is PrayerTimeViewModel vm)
            vm.ResumeAutoMode();
    }
}

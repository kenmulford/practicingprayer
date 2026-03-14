using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;
using PrayerApp.Views.Onboarding;

namespace PrayerApp.Views;

public partial class MainPage : ContentPage
{
    private readonly HomeViewModel _homeViewModel;
    private readonly IOnboardingService _onboardingService;

    public MainPage()
    {
        InitializeComponent();

        _homeViewModel = new HomeViewModel();
        BindingContext = _homeViewModel;

        _onboardingService = IPlatformApplication.Current!.Services
            .GetRequiredService<IOnboardingService>();

        BtnQuickAdd.Clicked += async (s, e) =>
            await Shell.Current.Navigation.PushModalAsync(new QuickAddPage());

        BtnPrayerTime.Clicked += async (s, e) =>
        {
            var action = await DisplayActionSheetAsync("Prayer Time", "Cancel", null, "All Requests", "By Tags");
            if (action == "All Requests")
                await Shell.Current.GoToAsync($"{nameof(PrayerTime.PrayerTimePage)}?scope=all");
            else if (action == "By Tags")
                await Shell.Current.Navigation.PushModalAsync(new PrayerTime.PrayerTimeScopePage());
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _homeViewModel.LoadAsync();

        // Show welcome popup on first visit — one-shot guard prevents re-showing on back navigation
        if (_onboardingService.CurrentStep == OnboardingStep.Welcome
            && !_onboardingService.WelcomeShownThisSession)
        {
            _onboardingService.MarkWelcomeShown();
            await this.ShowPopupAsync(new OnboardingWelcomePopup(_onboardingService),
                new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = false },
                CancellationToken.None);
        }
    }
}

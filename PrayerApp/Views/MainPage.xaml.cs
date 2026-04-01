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
    private readonly IOrientationService? _orientationService;
    private readonly IServiceProvider _services;
    private bool _prayerTimeNavigating;

    public MainPage(HomeViewModel homeViewModel, IOnboardingService onboardingService,
        IOrientationService orientationService, IServiceProvider services)
    {
        InitializeComponent();

        _homeViewModel = homeViewModel;
        BindingContext = _homeViewModel;

        _onboardingService = onboardingService;
        _orientationService = orientationService;
        _services = services;

        BtnQuickAdd.Clicked += async (s, e) =>
            await Shell.Current.Navigation.PushModalAsync(
                _services.GetRequiredService<QuickAddPage>());

        BtnPrayerTime.Clicked += async (s, e) => await LaunchPrayerTimeAsync();

        // Last Prayed metric card also launches Prayer Time
        LastPrayedCard.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await LaunchPrayerTimeAsync())
        });
    }

    private async Task LaunchPrayerTimeAsync()
    {
        if (_prayerTimeNavigating) return;
        _prayerTimeNavigating = true;
        try
        {
            if (!_homeViewModel.HasActivePrayers)
            {
                await DisplayAlertAsync("No Prayer Requests",
                    "Add a prayer card and some prayer requests to get started with Prayer Time.",
                    "OK");
                return;
            }

            if (!_homeViewModel.HasTags)
            {
                await Shell.Current.GoToAsync($"{Routes.PrayerTimePage}?scope=all");
                return;
            }

            var action = await DisplayActionSheetAsync("Prayer Time", "Cancel", null, "All Requests", "By Tags");
            if (action == "All Requests")
                await Shell.Current.GoToAsync($"{Routes.PrayerTimePage}?scope=all");
            else if (action == "By Tags")
                await Shell.Current.Navigation.PushModalAsync(
                    _services.GetRequiredService<PrayerTime.PrayerTimeScopePage>());
        }
        finally
        {
            _prayerTimeNavigating = false;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _orientationService?.LockPortrait();
        await App.InitTask; // ensure DB seeding is complete before loading data
        await _homeViewModel.LoadAsync();

        // Show welcome popup on first visit — skip if user entered via share link
        if (_onboardingService.CurrentStep == OnboardingStep.Welcome
            && !_onboardingService.WelcomeShownThisSession
            && !_onboardingService.IsDeepLinkSession)
        {
            _onboardingService.MarkWelcomeShown();
            try
            {
                await this.ShowPopupAsync(new OnboardingWelcomePopup(_onboardingService),
                    new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = false },
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Onboarding popup failed: {ex}");
            }
        }
    }
}

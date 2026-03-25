// PrayerApp/Views/Onboarding/OnboardingWelcomePopup.xaml.cs
using CommunityToolkit.Maui.Views;
using PrayerApp.Services;

namespace PrayerApp.Views.Onboarding;

public partial class OnboardingWelcomePopup : Popup
{
    private readonly IOnboardingService _onboardingService;

    public OnboardingWelcomePopup(IOnboardingService onboardingService)
    {
        InitializeComponent();
        _onboardingService = onboardingService;

        BtnGetStarted.Clicked += async (_, _) =>
        {
            await CloseAsync(CancellationToken.None);
            _onboardingService.Advance(); // Welcome → CreateCard
            await Shell.Current.GoToAsync("//CardsPage");
        };

        SkipButton.Clicked += async (_, _) =>
        {
            await CloseAsync(CancellationToken.None);
            _onboardingService.Skip();
        };
    }
}

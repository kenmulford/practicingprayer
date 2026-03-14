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
            _onboardingService.Advance(); // Welcome → CreateCard
            await CloseAsync(CancellationToken.None);
            await Shell.Current.GoToAsync("//CardsPage");
        };

        SkipTap.Tapped += async (_, _) =>
        {
            _onboardingService.Skip();
            await CloseAsync(CancellationToken.None);
        };
    }
}

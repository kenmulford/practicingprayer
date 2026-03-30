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

        if (_onboardingService.IsShareTutorial)
        {
            TitleLabel.Text = "New: Share Prayers!";
            SubtitleLabel.Text = "You can now share prayer cards and requests with others. Let us show you how.";
            BtnGetStarted.Text = "Show Me";
        }

        Opened += async (_, _) =>
        {
            await Task.Delay(100);
            BtnGetStarted.SetSemanticFocus();
        };

        BtnGetStarted.Clicked += async (_, _) =>
        {
            await CloseAsync(CancellationToken.None);
            _onboardingService.Advance(); // Welcome → CreateCard (new user) or Welcome → ShareIntro (share tutorial)
            await Shell.Current.GoToAsync("//CardsPage");
        };

        SkipButton.Clicked += async (_, _) =>
        {
            await CloseAsync(CancellationToken.None);
            _onboardingService.Skip();
        };
    }
}

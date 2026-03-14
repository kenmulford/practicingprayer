// PrayerApp/Views/Onboarding/OnboardingCompletePopup.xaml.cs
using CommunityToolkit.Maui.Views;
using PrayerApp.Services;

namespace PrayerApp.Views.Onboarding;

public partial class OnboardingCompletePopup : Popup
{
    public OnboardingCompletePopup()
    {
        InitializeComponent();
        BtnDone.Clicked += async (_, _) =>
        {
            PrayerApp.Services.Settings.OnboardingComplete = true;
            await CloseAsync(CancellationToken.None);
        };
    }
}

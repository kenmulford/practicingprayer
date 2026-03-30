namespace PrayerApp.Models;

public enum OnboardingStep
{
    None,
    Welcome,
    CreateCard,
    NameCard,
    AddRequest,
    NameRequest,
    ShareIntro,
    SharePrayer,
    PrayerTimeHighlight,
    Complete,

    // Legacy values — kept for migration from pre-1.0.7 installs.
    // OnboardingService maps these to ShareIntro on startup.
    PrayerTime = 100,
    PrayerTimeActive = 101
}

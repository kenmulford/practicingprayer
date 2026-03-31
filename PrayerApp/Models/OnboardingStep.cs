namespace PrayerApp.Models;

public enum OnboardingStep
{
    None,
    Welcome,
    CreateCard,
    NameCard,
    AddRequest,
    NameRequest,
    PrayerTimeHighlight,
    Complete,

    // Legacy values — kept for migration from older installs.
    // OnboardingService maps these to PrayerTimeHighlight on startup.
    PrayerTime = 100,
    PrayerTimeActive = 101,
    ShareIntro = 200,
    SharePrayer = 201
}

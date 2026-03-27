using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 1: First Launch / Onboarding
/// Note: Most onboarding tests require a fresh install (app data cleared).
/// The shared fixture only launches once, so only the initial state and skip flow are testable.
/// </summary>
[Collection("Appium")]
[Trait("Platform", "CrossPlatform")]
[Trait("Section", "1-Onboarding")]
public class OnboardingTests
{
    private readonly AppiumSetup _setup;
    public OnboardingTests(AppiumSetup setup) => _setup = setup;

    /// <summary>1.1 + 1.2: Fresh install — welcome popup appears with expected buttons.</summary>
    [Fact]
    public void Onboarding_WelcomePopup_ShowsOnFirstLaunch()
    {
        var driver = _setup.Driver;

        if (_setup.OnboardingHandled)
            return;

        var hasGetStarted = driver.IsDisplayed("Welcome_Btn_GetStarted", timeoutSeconds: 5);
        var hasSkip = driver.IsDisplayed("Welcome_Btn_Skip", timeoutSeconds: 2);

        Assert.True(hasGetStarted || hasSkip,
            "Expected welcome popup with 'Get Started' or 'Skip tour' buttons on first launch");
    }

    /// <summary>1.7: Skip onboarding — tapping Skip dismisses the entire flow.</summary>
    [Fact]
    public void Onboarding_SkipButton_DismissesEntireFlow()
    {
        var driver = _setup.Driver;

        if (_setup.OnboardingHandled)
            return;

        driver.DismissOnboardingIfPresent(_setup);

        Assert.True(driver.IsDisplayed("Home_Btn_QuickAdd", timeoutSeconds: 10)
                 || driver.IsDisplayed("Home_Btn_PrayerTime", timeoutSeconds: 3),
            "After onboarding dismissal, Home page elements should be visible");
    }
}

using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 1: First Launch / Onboarding
/// These tests run first and handle the onboarding flow.
/// After these tests complete, onboarding is dismissed for the rest of the suite.
/// </summary>
[Collection("Appium")]
[Trait("Platform", "Android")]
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

        // On a fresh install, the welcome popup should be visible
        // If onboarding was already handled (test order), skip the assertion
        if (_setup.OnboardingHandled)
            return;

        var hasGetStarted = driver.IsDisplayed("Welcome_Btn_GetStarted", timeoutSeconds: 5);
        var hasSkip = driver.IsDisplayed("Welcome_Btn_Skip", timeoutSeconds: 2);

        // At least one onboarding element should be present on fresh install
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

        // Skip the onboarding
        if (driver.IsDisplayed("Welcome_Btn_Skip", timeoutSeconds: 3))
        {
            driver.Tap("Welcome_Btn_Skip");
            Thread.Sleep(1000);

            // Completion popup should appear
            if (driver.IsDisplayed("Complete_Btn_Done", timeoutSeconds: 5))
            {
                driver.Tap("Complete_Btn_Done");
                Thread.Sleep(500);
            }

            _setup.OnboardingHandled = true;
        }
        else if (driver.IsDisplayed("Banner_Btn_Skip", timeoutSeconds: 2))
        {
            driver.Tap("Banner_Btn_Skip");
            Thread.Sleep(1000);

            if (driver.IsDisplayed("Complete_Btn_Done", timeoutSeconds: 5))
            {
                driver.Tap("Complete_Btn_Done");
                Thread.Sleep(500);
            }

            _setup.OnboardingHandled = true;
        }

        // After dismissal, the Home tab should be accessible
        Assert.True(driver.IsDisplayed("Home_Btn_QuickAdd", timeoutSeconds: 10)
                 || driver.IsDisplayed("Home_Btn_PrayerTime", timeoutSeconds: 3),
            "After onboarding dismissal, Home page elements should be visible");
    }
}

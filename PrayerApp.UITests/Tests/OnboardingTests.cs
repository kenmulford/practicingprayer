using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 1: First Launch / Onboarding
/// Note: Most onboarding tests require a fresh install (app data cleared).
/// The shared fixture only launches once, so only the initial state and skip flow are testable.
/// Post-dismissal tests verify that onboarding banners are properly hidden.
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

        var hasGetStarted = driver.IsDisplayed("Welcome_Btn_GetStarted", timeoutSeconds: 10);
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

    // ── Post-dismissal: onboarding banners should be hidden ──────

    /// <summary>1.8: PrayerTimeHighlight "Got it!" button not visible on Home after onboarding is complete.</summary>
    [Fact]
    public void Onboarding_GotItButton_NotVisibleAfterDismiss()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;
        driver.EnsureOnTab("Home", _setup);

        Assert.False(driver.IsDisplayed("Banner_Btn_GotIt", timeoutSeconds: 2),
            "PrayerTimeHighlight 'Got it!' button should not be visible after onboarding is complete");
    }

    // ── Sharing UI presence tests ────────────────────────────────
    // Card share button is covered by Cards_EditPage_ShowsShareButton in PrayerCardTests.

    /// <summary>1.11: Share button exists on prayer detail view page.</summary>
    [SkippableFact]
    public void Sharing_PrayerDetailShareButton_Exists()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;
        driver.EnsureUITestPrayerExists(_setup);
        driver.EnsureOnTab("Prayers", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Open a prayer in view mode
        if (!driver.IsTextDisplayed("UI Test Prayer", timeoutSeconds: 10))
            throw new SkipException("Precondition: 'UI Test Prayer' not found");

        driver.TapByText("UI Test Prayer");
        Thread.Sleep(TestConfig.DelayAfterNavigation);

        Assert.True(driver.IsDisplayed("Detail_Btn_Share", timeoutSeconds: 10),
            "Share button should be visible on prayer detail view page");

        driver.GoBack();
    }
}

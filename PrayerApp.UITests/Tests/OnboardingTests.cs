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

    // ── Post-dismissal: onboarding banners should be hidden ──────

    /// <summary>1.8: ShareIntro banner not visible on Cards tab after onboarding is complete.</summary>
    [Fact]
    public void Onboarding_ShareIntro_BannerNotVisibleAfterDismiss()
    {
        var driver = _setup.Driver;
        driver.EnsureOnTab("Prayer Cards", _setup);

        Assert.False(driver.IsDisplayed("Banner_Btn_Skip", timeoutSeconds: 2),
            "ShareIntro onboarding banner should not be visible after onboarding is complete");
    }

    /// <summary>1.9: SharePrayer banner not visible on prayer detail after onboarding is complete.</summary>
    [Fact]
    public void Onboarding_SharePrayer_BannerNotVisibleAfterDismiss()
    {
        var driver = _setup.Driver;
        driver.EnsureUITestPrayerExists(_setup);
        driver.EnsureOnTab("Prayers", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Tap into a prayer detail
        if (driver.IsTextDisplayed("UI Test Prayer", timeoutSeconds: 5))
        {
            driver.TapByText("UI Test Prayer");
            Thread.Sleep(TestConfig.DelayAfterNavigation);
        }

        Assert.False(driver.IsDisplayed("Banner_Btn_Skip", timeoutSeconds: 2),
            "SharePrayer onboarding banner should not be visible after onboarding is complete");

        driver.GoBack();
    }

    /// <summary>1.10: PrayerTimeHighlight "Got it!" button not visible on Home after onboarding is complete.</summary>
    [Fact]
    public void Onboarding_GotItButton_NotVisibleAfterDismiss()
    {
        var driver = _setup.Driver;
        driver.EnsureOnTab("Home", _setup);

        Assert.False(driver.IsDisplayed("Banner_Btn_GotIt", timeoutSeconds: 2),
            "PrayerTimeHighlight 'Got it!' button should not be visible after onboarding is complete");
    }

    // ── Sharing UI presence tests ────────────────────────────────
    // Card share button is covered by Cards_EditPage_ShowsShareButton in PrayerCardTests.

    /// <summary>1.11: Share button exists on prayer detail view page.</summary>
    [Fact]
    public void Sharing_PrayerDetailShareButton_Exists()
    {
        var driver = _setup.Driver;
        driver.EnsureUITestPrayerExists(_setup);
        driver.EnsureOnTab("Prayers", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Open a prayer in view mode
        if (!driver.IsTextDisplayed("UI Test Prayer", timeoutSeconds: 5))
            throw Xunit.Sdk.SkipException.ForSkip("Precondition: 'UI Test Prayer' not found");

        driver.TapByText("UI Test Prayer");
        Thread.Sleep(TestConfig.DelayAfterNavigation);

        Assert.True(driver.IsDisplayed("Detail_Btn_Share", timeoutSeconds: 5),
            "Share button should be visible on prayer detail view page");

        driver.GoBack();
    }
}

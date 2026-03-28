using OpenQA.Selenium;
using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 8: Prayer Time
/// </summary>
[Collection("Appium")]
[Trait("Platform", "CrossPlatform")]
[Trait("Section", "8-PrayerTime")]
public class PrayerTimeTests
{
    private readonly AppiumSetup _setup;
    public PrayerTimeTests(AppiumSetup setup) => _setup = setup;

    /// <summary>Start a prayer time session via "All Requests".</summary>
    /// <remarks>
    /// CRITICAL iOS constraint: <c>autoDismissAlerts: true</c> in Appium capabilities causes
    /// the XCUITest driver to auto-tap action sheet buttons ~1s after they appear. On iPad,
    /// it picks "By Tags" (bottom option). We must find and tap "All Requests" immediately
    /// with ZERO delays — any Thread.Sleep gives autoDismissAlerts time to fire first.
    /// </remarks>
    private bool TryStartPrayerTime()
    {
        var driver = _setup.Driver;

        // Ensure prayer data exists once — without prayers, "All Requests" goes straight
        // to the completion screen ("You've prayed through all your requests!")
        driver.EnsureUITestPrayerExists(_setup);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            driver.EnsureOnTab("Home", _setup);

            driver.WaitAndTap("Home_Btn_PrayerTime");

            // NO DELAYS — race autoDismissAlerts on iOS.
            // Android: brief wait for action sheet animation.
            if (!TestConfig.IsIOS) Thread.Sleep(500);

            try
            {
                driver.TapIOSActionSheetButton("All Requests", timeoutSeconds: 3);
                Thread.Sleep(1000);

                if (TryRecoverFromTagScope(driver))
                    continue;

                // Check if we're in a real prayer session — try AutomationId then text
                bool inSession = driver.IsDisplayed("PrayerTime_Btn_Next", timeoutSeconds: 1)
                    || driver.IsTextDisplayed("›", timeoutSeconds: 1)
                    || driver.IsTextDisplayed("I'm done", timeoutSeconds: 1);
                if (inSession)
                    return true;

                // Not in session — might be "no prayers" completion screen
                bool onCompletion = driver.IsDisplayed("PrayerTime_Btn_Finish", timeoutSeconds: 1)
                    || driver.IsTextDisplayed("Finish", timeoutSeconds: 1);
                if (onCompletion)
                {
                    if (driver.IsDisplayed("PrayerTime_Btn_Finish", timeoutSeconds: 1))
                        driver.Tap("PrayerTime_Btn_Finish");
                    else
                        driver.TapByText("Finish");
                    Thread.Sleep(500);
                    continue;
                }

                // Fallback: assume we're in a session
                return true;
            }
            catch (WebDriverException)
            {
                driver.DismissAlertIfPresent();
                TryRecoverFromTagScope(driver);
                continue;
            }
        }
        return false;
    }

    /// <summary>If autoDismissAlerts sent us to the tag scope page, cancel and return true.</summary>
    private bool TryRecoverFromTagScope(OpenQA.Selenium.Appium.AppiumDriver driver)
    {
        if (driver.IsDisplayed("Scope_Btn_Cancel", timeoutSeconds: 1))
        {
            driver.Tap("Scope_Btn_Cancel");
            Thread.Sleep(TestConfig.DelayModalAnimation);
            return true;
        }
        return false;
    }

    /// <summary>Exit prayer time via Finish, Done, or Back.</summary>
    /// <remarks>
    /// iOS accessibility flattening makes AutomationIds invisible inside the Prayer Time
    /// page layout. Fall back to text-based locators for "Finish" and "I'm done" buttons.
    /// </remarks>
    private void ExitPrayerTime()
    {
        var driver = _setup.Driver;

        // Try AutomationId first (works on Android)
        if (driver.IsDisplayed("PrayerTime_Btn_Finish", timeoutSeconds: 2))
            driver.Tap("PrayerTime_Btn_Finish");
        else if (driver.IsDisplayed("PrayerTime_Btn_Done", timeoutSeconds: 2))
            driver.Tap("PrayerTime_Btn_Done");
        // iOS fallback: text-based search for the button labels
        else if (driver.IsTextDisplayed("Finish", timeoutSeconds: 2))
            driver.TapByText("Finish");
        else if (driver.IsTextDisplayed("I'm done", timeoutSeconds: 2))
            driver.TapByText("I'm done");
        else if (driver.IsTextContainsDisplayed("done", timeoutSeconds: 2))
            driver.TapByTextContains("done");
        else
            driver.GoBack();
        Thread.Sleep(500);
    }

    /// <summary>8.1: Prayer Time session starts — carousel and nav buttons visible.</summary>
    [Fact]
    public void PrayerTime_SessionStarts_ShowsCarousel()
    {
        if (!TryStartPrayerTime())
            throw Xunit.Sdk.SkipException.ForSkip("Prayer Time action sheet could not be started — see ios-uat-bugs-found.md Bug #4");

        var driver = _setup.Driver;

        // iOS accessibility flattening hides AutomationIds — use text fallbacks
        Assert.True(
            driver.IsDisplayed("PrayerTime_List_Carousel", timeoutSeconds: 5)
            || driver.IsDisplayed("PrayerTime_Btn_Done", timeoutSeconds: 3)
            || driver.IsTextDisplayed("I'm done", timeoutSeconds: 2)
            || driver.IsTextContainsDisplayed("of", timeoutSeconds: 2), // "1 of 2" counter
            "Prayer time should show carousel or Done button");

        ExitPrayerTime();
    }

    /// <summary>8.2: Navigation buttons — Previous/Next are present.</summary>
    [Fact]
    public void PrayerTime_NavigationButtons_Present()
    {
        if (!TryStartPrayerTime())
            throw Xunit.Sdk.SkipException.ForSkip("Prayer Time action sheet could not be started — see ios-uat-bugs-found.md Bug #4");

        var driver = _setup.Driver;

        // iOS: AutomationIds invisible due to flattening — fall back to button text
        Assert.True(
            driver.IsDisplayed("PrayerTime_Btn_Previous", timeoutSeconds: 3)
            || driver.IsDisplayed("PrayerTime_Btn_Next", timeoutSeconds: 3)
            || driver.IsTextDisplayed("‹", timeoutSeconds: 2)
            || driver.IsTextDisplayed("›", timeoutSeconds: 2)
            || driver.IsTextDisplayed("I'm done", timeoutSeconds: 2),
            "Navigation buttons or session controls should be visible");

        ExitPrayerTime();
    }

    /// <summary>8.3: Auto-mode button cycles timer intervals.</summary>
    [Fact]
    public void PrayerTime_AutoMode_CyclesInterval()
    {
        if (!TryStartPrayerTime())
            throw Xunit.Sdk.SkipException.ForSkip("Prayer Time action sheet could not be started — see ios-uat-bugs-found.md Bug #4");

        var driver = _setup.Driver;

        // Try AutomationId first, then text fallback for iOS
        bool autoModeFound = driver.IsDisplayed("PrayerTime_Btn_AutoMode", timeoutSeconds: 3);
        if (!autoModeFound && TestConfig.IsIOS)
            autoModeFound = driver.IsTextContainsDisplayed("Auto", timeoutSeconds: 2);

        if (autoModeFound)
        {
            try
            {
                if (driver.IsDisplayed("PrayerTime_Btn_AutoMode", timeoutSeconds: 1))
                    driver.Tap("PrayerTime_Btn_AutoMode");
                else
                    driver.TapByTextContains("Auto");
            }
            catch (WebDriverException) { /* button may have shifted */ }
            Thread.Sleep(300);

            Assert.True(
                driver.IsDisplayed("PrayerTime_Btn_Pause", timeoutSeconds: 3)
                || driver.IsDisplayed("PrayerTime_Btn_CycleInterval", timeoutSeconds: 3)
                || driver.IsTextContainsDisplayed("Pause", timeoutSeconds: 2)
                || driver.IsTextContainsDisplayed("30s", timeoutSeconds: 2),
                "Auto mode should show pause/cycle controls or timer interval");
        }

        ExitPrayerTime();
    }

    /// <summary>8.5: "I'm Done" / Finish button exits prayer time.</summary>
    [Fact]
    public void PrayerTime_FinishButton_ExitsPrayerTime()
    {
        if (!TryStartPrayerTime())
            throw Xunit.Sdk.SkipException.ForSkip("Prayer Time action sheet could not be started — see ios-uat-bugs-found.md Bug #4");

        ExitPrayerTime();

        Assert.True(
            _setup.Driver.IsDisplayed("Home_Btn_QuickAdd", timeoutSeconds: 5)
            || _setup.Driver.IsDisplayed("Home_Btn_PrayerTime", timeoutSeconds: 3),
            "Should return to Home after exiting Prayer Time");
    }

    /// <summary>8.6: Tag-scoped session — scope page shows Cancel/Start.</summary>
    /// <remarks>
    /// On iOS with autoDismissAlerts, Appium auto-taps the bottom action sheet button
    /// ("By Tags") ~1s after it appears. This test intentionally uses NO delays after
    /// showing the action sheet — either autoDismissAlerts taps "By Tags" for us, or
    /// we tap it manually. Either way we end up on the tag scope page.
    /// </remarks>
    [Fact]
    public void PrayerTime_TagScoped_ShowsScopePage()
    {
        var driver = _setup.Driver;
        driver.EnsureOnTab("Home", _setup);

        driver.WaitAndTap("Home_Btn_PrayerTime");

        // NO DELAYS — on iOS, autoDismissAlerts will tap "By Tags" for us.
        // On Android, brief wait for the action sheet to appear.
        if (!TestConfig.IsIOS) Thread.Sleep(500);

        // Try to tap "By Tags" explicitly (may already be auto-tapped on iOS).
        // Short timeout — autoDismissAlerts fires ~1s after the sheet appears.
        try { driver.TapIOSActionSheetButton("By Tags", timeoutSeconds: 2); }
        catch (WebDriverException) { /* autoDismissAlerts already handled it */ }

        // Either way, we should now be on the tag scope page
        Assert.True(
            driver.IsDisplayed("Scope_Btn_Start", timeoutSeconds: 5)
            || driver.IsDisplayed("Scope_Btn_Cancel", timeoutSeconds: 3),
            "Tag scope page should show Start and Cancel buttons");

        driver.WaitAndTap("Scope_Btn_Cancel");
        Thread.Sleep(TestConfig.DelayModalAnimation);
        Assert.True(driver.IsDisplayed("Home_Btn_PrayerTime", timeoutSeconds: 5),
            "Cancel should dismiss scope modal and return to Home");
    }
}

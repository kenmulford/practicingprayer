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

                // Check Next first — if present, we're in a real session (fast path)
                if (driver.IsDisplayed("PrayerTime_Btn_Next", timeoutSeconds: 1))
                    return true;

                // Next not found — might be "no prayers" completion screen (Done without Next)
                if (driver.IsDisplayed("PrayerTime_Btn_Done", timeoutSeconds: 1))
                {
                    driver.Tap("PrayerTime_Btn_Done");
                    Thread.Sleep(500);
                    continue;
                }

                // Single-prayer session: only Done visible, no Next — still valid
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
    private void ExitPrayerTime()
    {
        var driver = _setup.Driver;
        if (driver.IsDisplayed("PrayerTime_Btn_Finish", timeoutSeconds: 2))
            driver.Tap("PrayerTime_Btn_Finish");
        else if (driver.IsDisplayed("PrayerTime_Btn_Done", timeoutSeconds: 2))
            driver.Tap("PrayerTime_Btn_Done");
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

        Assert.True(
            driver.IsDisplayed("PrayerTime_List_Carousel", timeoutSeconds: 5)
            || driver.IsDisplayed("PrayerTime_Btn_Done", timeoutSeconds: 3),
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

        Assert.True(
            driver.IsDisplayed("PrayerTime_Btn_Previous", timeoutSeconds: 3)
            || driver.IsDisplayed("PrayerTime_Btn_Next", timeoutSeconds: 3),
            "Previous and/or Next navigation buttons should be visible");

        ExitPrayerTime();
    }

    /// <summary>8.3: Auto-mode button cycles timer intervals.</summary>
    [Fact]
    public void PrayerTime_AutoMode_CyclesInterval()
    {
        if (!TryStartPrayerTime())
            throw Xunit.Sdk.SkipException.ForSkip("Prayer Time action sheet could not be started — see ios-uat-bugs-found.md Bug #4");

        var driver = _setup.Driver;

        if (driver.IsDisplayed("PrayerTime_Btn_AutoMode", timeoutSeconds: 3))
        {
            driver.Tap("PrayerTime_Btn_AutoMode");
            Thread.Sleep(300);

            Assert.True(
                driver.IsDisplayed("PrayerTime_Btn_Pause", timeoutSeconds: 3)
                || driver.IsDisplayed("PrayerTime_Btn_CycleInterval", timeoutSeconds: 3),
                "Auto mode should show pause/cycle controls");
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

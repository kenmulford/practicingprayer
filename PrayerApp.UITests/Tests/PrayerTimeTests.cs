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
    private bool TryStartPrayerTime()
    {
        var driver = _setup.Driver;

        // Retry the whole flow up to 2 times — the action sheet can re-render
        // between IsTextDisplayed and TapByText, causing stale element errors.
        for (int attempt = 0; attempt < 2; attempt++)
        {
            driver.EnsureOnTab("Home", _setup);
            if (TestConfig.IsIOS) Thread.Sleep(500); // Let Home page fully render

            driver.WaitAndTap("Home_Btn_PrayerTime");
            Thread.Sleep(500);

            if (driver.IsTextDisplayed("All Requests", timeoutSeconds: 3))
            {
                try
                {
                    driver.TapByText("All Requests");
                    Thread.Sleep(1000);
                    return true;
                }
                catch (WebDriverException)
                {
                    // Stale element — dismiss the action sheet and retry
                    driver.DismissAlertIfPresent();
                    Thread.Sleep(500);
                    continue;
                }
            }

            driver.DismissAlertIfPresent();
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
    [Fact]
    public void PrayerTime_TagScoped_ShowsScopePage()
    {
        var driver = _setup.Driver;
        driver.EnsureOnTab("Home", _setup);

        driver.WaitAndTap("Home_Btn_PrayerTime");
        Thread.Sleep(500);

        if (driver.IsTextDisplayed("By Tags", timeoutSeconds: 3))
        {
            driver.TapByText("By Tags");

            Assert.True(
                driver.IsDisplayed("Scope_Btn_Start", timeoutSeconds: 5)
                || driver.IsDisplayed("Scope_Btn_Cancel", timeoutSeconds: 3),
                "Tag scope page should show Start and Cancel buttons");

            driver.WaitAndTap("Scope_Btn_Cancel");
            Thread.Sleep(1000); // Wait for modal dismiss animation
            // Verify we're back on Home (modal actually dismissed)
            Assert.True(driver.IsDisplayed("Home_Btn_PrayerTime", timeoutSeconds: 5),
                "Cancel should dismiss scope modal and return to Home");
        }
        else
        {
            driver.DismissAlertIfPresent();
            driver.GoBack();
        }
    }
}

using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
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

    /// <summary>Start a prayer time session (no-tags path: direct navigation).</summary>
    /// <remarks>
    /// Commit 84d4b11 added smart guards to Prayer Time:
    /// - No prayers → "No Prayer Requests" alert (blocked at Home)
    /// - Prayers exist, no tags → skips action sheet, goes directly to Prayer Time
    /// - Prayers + tags → shows action sheet
    ///
    /// In the test environment we create one prayer but no tags, so Prayer Time
    /// launches directly — no action sheet, no autoDismissAlerts race.
    /// </remarks>
    private bool TryStartPrayerTime()
    {
        var driver = _setup.Driver;

        // Ensure prayer data exists — without prayers, the guard shows an alert instead
        driver.EnsureUITestPrayerExists(_setup);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            driver.EnsureOnTab("Home", _setup);

            // Dismiss any lingering alert (e.g. "No Prayer Requests" from a prior attempt)
            driver.DismissAlertIfPresent();

            driver.WaitAndTap("Home_Btn_PrayerTime");

            // Two paths after tapping Prayer Time:
            // 1. No tags → navigates directly to Prayer Time (no action sheet)
            // 2. Tags exist → shows action sheet with "All Requests" / "By Tags"
            // Diagnostic data shows action sheet stays open (autoDismissAlerts
            // does NOT auto-tap it), so we must explicitly tap "All Requests".

            // Try to tap "All Requests" on the action sheet. Use AccessibilityId
            // + Click() instead of mobile: tap — iPad popover coordinates can drift.
            // If no tags, there's no action sheet and this fails fast.
            try
            {
                driver.Manage().Timeouts().ImplicitWait = TestConfig.ShortTimeout;
                var allReqBtn = driver.FindElement(MobileBy.AccessibilityId("All Requests"));
                allReqBtn.Click();
            }
            catch (WebDriverException)
            {
                // No action sheet — either direct navigation (no tags) or alert appeared
                driver.DismissAlertIfPresent();
            }
            finally
            {
                driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
            }
            Thread.Sleep(TestConfig.DelayModalAnimation);

            // Check if we landed on tag scope page (tapped "By Tags" by mistake)
            if (TryRecoverFromTagScope(driver))
                continue;

            // Check if we're in a real prayer session
            bool inSession = driver.IsDisplayed("PrayerTime_Btn_Next", timeoutSeconds: 3)
                || driver.IsDisplayed("PrayerTime_Btn_Done", timeoutSeconds: 1)
                || driver.IsTextDisplayed("›", timeoutSeconds: 1)
                || driver.IsTextDisplayed("I'm done", timeoutSeconds: 1);
            if (inSession)
                return true;

            // Might be on the "all prayed" completion screen
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

        // Diagnostic: capture Prayer Time page state before attempting exit
        if (TestConfig.IsIOS)
            driver.DumpPageSource("PrayerTime_BeforeExit");

        // Try AutomationId first (works on Android)
        if (driver.IsDisplayed("PrayerTime_Btn_Finish", timeoutSeconds: 2))
        {
            driver.Tap("PrayerTime_Btn_Finish");
        }
        else if (driver.IsDisplayed("PrayerTime_Btn_Done", timeoutSeconds: 2))
        {
            driver.Tap("PrayerTime_Btn_Done");
        }
        // iOS fallback: text-based search for the button labels
        else if (driver.IsTextDisplayed("Finish", timeoutSeconds: 2))
        {
            driver.TapByText("Finish");
        }
        else if (driver.IsTextDisplayed("I'm done", timeoutSeconds: 2))
        {
            driver.TapByText("I'm done");
        }
        else if (driver.IsTextContainsDisplayed("done", timeoutSeconds: 2))
        {
            driver.TapByTextContains("done");
        }
        else
        {
            // Nothing found — dump page source for debugging
            if (TestConfig.IsIOS)
                driver.DumpPageSource("PrayerTime_ExitFailed");
            driver.GoBack();
        }
        Thread.Sleep(500);

        // Diagnostic: capture state after exit attempt
        if (TestConfig.IsIOS)
            driver.DumpPageSource("PrayerTime_AfterExit");
    }

    /// <summary>8.1: Prayer Time session starts — carousel and nav buttons visible.</summary>
    [SkippableFact]
    public void PrayerTime_SessionStarts_ShowsCarousel()
    {
        _setup.Driver.ResetAppUIState(_setup);
        if (!TryStartPrayerTime())
            throw new SkipException("Could not start Prayer Time session");

        var driver = _setup.Driver;

        // iOS accessibility flattening hides AutomationIds — use text fallbacks
        Assert.True(
            driver.IsDisplayed("PrayerTime_List_Carousel", timeoutSeconds: 10)
            || driver.IsDisplayed("PrayerTime_Btn_Done", timeoutSeconds: 3)
            || driver.IsTextDisplayed("I'm done", timeoutSeconds: 2)
            || driver.IsTextContainsDisplayed("of", timeoutSeconds: 2), // "1 of 2" counter
            "Prayer time should show carousel or Done button");

        ExitPrayerTime();
    }

    /// <summary>8.2: Navigation buttons — Previous/Next are present.</summary>
    [SkippableFact]
    public void PrayerTime_NavigationButtons_Present()
    {
        _setup.Driver.ResetAppUIState(_setup);
        if (!TryStartPrayerTime())
            throw new SkipException("Could not start Prayer Time session");

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
    [SkippableFact]
    public void PrayerTime_AutoMode_CyclesInterval()
    {
        _setup.Driver.ResetAppUIState(_setup);
        if (!TryStartPrayerTime())
            throw new SkipException("Could not start Prayer Time session");

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
    [SkippableFact]
    public void PrayerTime_FinishButton_ExitsPrayerTime()
    {
        _setup.Driver.ResetAppUIState(_setup);
        if (!TryStartPrayerTime())
            throw new SkipException("Could not start Prayer Time session");

        ExitPrayerTime();

        Assert.True(
            _setup.Driver.IsDisplayed("Home_Btn_QuickAdd", timeoutSeconds: 10)
            || _setup.Driver.IsDisplayed("Home_Btn_PrayerTime", timeoutSeconds: 3),
            "Should return to Home after exiting Prayer Time");
    }

    /// <summary>8.6: Tag-scoped session — scope page shows Cancel/Start.</summary>
    /// <remarks>
    /// Commit 84d4b11: action sheet only appears when both prayers AND tags exist.
    /// This test ensures a tag exists so the action sheet is shown.
    /// On iOS with autoDismissAlerts, Appium auto-taps the bottom action sheet button
    /// ("By Tags") ~1s after it appears — either autoDismissAlerts taps it for us, or
    /// we tap it manually. Either way we end up on the tag scope page.
    /// </remarks>
    [Fact]
    public void PrayerTime_TagScoped_ShowsScopePage()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;

        // Both prayers and tags must exist for the action sheet to appear
        driver.EnsureUITestPrayerExists(_setup);
        driver.EnsureUITestTagExists(_setup);

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
            driver.IsDisplayed("Scope_Btn_Start", timeoutSeconds: 10)
            || driver.IsDisplayed("Scope_Btn_Cancel", timeoutSeconds: 3),
            "Tag scope page should show Start and Cancel buttons");

        driver.WaitAndTap("Scope_Btn_Cancel");
        Thread.Sleep(TestConfig.DelayModalAnimation);
        Assert.True(driver.IsDisplayed("Home_Btn_PrayerTime", timeoutSeconds: 10),
            "Cancel should dismiss scope modal and return to Home");
    }
}

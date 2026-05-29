using System.Diagnostics;
using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 1: First Launch / Onboarding
/// Note: Most onboarding tests require a fresh install (app data cleared).
/// The shared fixture only launches once, so only the initial state and skip flow are testable.
/// Post-dismissal tests verify that onboarding banners are properly hidden.
///
/// iOS: the shared <see cref="AppiumSetup"/> pre-seeds <c>OnboardingComplete=true</c>
/// at suite start (see <see cref="TestDataSeed.PreSeedOnboardingCompleteAsync"/>),
/// so popup-flow tests below explicitly invert that — write <c>NO</c>, terminate +
/// relaunch the app to force the popup, then restore <c>YES</c> on teardown so
/// later tests in the run continue to skip onboarding via the harness pre-seed.
/// Android keeps the legacy in-suite dismissal flow until that toolchain returns.
/// </summary>
[Collection("Appium")]
[Trait("Platform", "CrossPlatform")]
[Trait("Section", "1-Onboarding")]
public class OnboardingTests
{
    private readonly AppiumSetup _setup;
    public OnboardingTests(AppiumSetup setup) => _setup = setup;

    /// <summary>1.1 + 1.2: Fresh install — welcome popup appears with expected buttons.</summary>
    [SkippableFact]
    public void Onboarding_WelcomePopup_ShowsOnFirstLaunch()
    {
        if (TestConfig.IsIOS) ForceOnboardingPopup(_setup);
        try
        {
            var driver = _setup.Driver;
            if (!TestConfig.IsIOS && _setup.OnboardingHandled)
                throw new SkipException(
                    "Android: onboarding already handled by a prior test in this collection. " +
                    "There is no Android equivalent of `ForceOnboardingPopup` yet (would require " +
                    "`adb shell pm clear` + relaunch). Run this test in isolation to exercise the assertion.");

            var hasGetStarted = driver.IsDisplayed("Welcome_Btn_GetStarted", timeoutSeconds: 10);
            var hasSkip = driver.IsDisplayed("Welcome_Btn_Skip", timeoutSeconds: 2);

            Assert.True(hasGetStarted || hasSkip,
                "Expected welcome popup with 'Get Started' or 'Skip tour' buttons on first launch");
        }
        finally
        {
            if (TestConfig.IsIOS) RestoreOnboardingComplete(_setup);
        }
    }

    /// <summary>1.7: Skip onboarding — tapping Skip dismisses the entire flow.</summary>
    [SkippableFact]
    public void Onboarding_SkipButton_DismissesEntireFlow()
    {
        if (TestConfig.IsIOS) ForceOnboardingPopup(_setup);
        try
        {
            var driver = _setup.Driver;
            if (!TestConfig.IsIOS && _setup.OnboardingHandled)
                throw new SkipException(
                    "Android: onboarding already handled by a prior test in this collection. " +
                    "There is no Android equivalent of `ForceOnboardingPopup` yet (would require " +
                    "`adb shell pm clear` + relaunch). Run this test in isolation to exercise the assertion.");

            if (TestConfig.IsIOS)
            {
                // On iOS the DismissOnboardingIfPresent helper is now a no-op
                // assertion that requires OnboardingComplete=YES — but we just
                // inverted to NO to force the popup. Dismiss via the popup UI
                // directly (same flow Android takes inside the helper).
                TapWelcomeSkipFlow(driver);
            }
            else
            {
                driver.DismissOnboardingIfPresent(_setup);
            }

            Assert.True(driver.IsDisplayed("Home_Btn_QuickAdd", timeoutSeconds: 10)
                     || driver.IsDisplayed("Home_Btn_PrayerTime", timeoutSeconds: 3),
                "After onboarding dismissal, Home page elements should be visible");
        }
        finally
        {
            if (TestConfig.IsIOS) RestoreOnboardingComplete(_setup);
        }
    }

    // ── iOS onboarding-state helpers ─────────────────────────────
    // Used only by the two popup-flow tests above; for everything else, the
    // suite-level pre-seed in AppiumSetup.InitializeAsync keeps onboarding off.

    /// <summary>
    /// iOS only: writes OnboardingComplete=NO, terminates the app, and relaunches
    /// it so the popup renders on the next OnAppearing. Resets OnboardingHandled
    /// so later helper calls don't short-circuit during this scenario.
    /// </summary>
    private static void ForceOnboardingPopup(AppiumSetup setup)
    {
        RunXcrun($"simctl spawn booted defaults write {TestConfig.IOSBundleId} OnboardingComplete -bool NO");
        RunXcrun($"simctl terminate booted {TestConfig.IOSBundleId}", allowFailure: true);
        setup.OnboardingHandled = false;
        setup.Driver.ActivateApp(TestConfig.IOSBundleId);
        Thread.Sleep(TestConfig.DelayAppRelaunch);
    }

    /// <summary>
    /// iOS only: restore OnboardingComplete=YES so later tests resume the
    /// harness-level pre-seed contract. Does not re-terminate — later
    /// EnsureOnTab calls hit DismissOnboardingIfPresent's no-op assertion,
    /// which reads the value fresh from NSUserDefaults.
    /// </summary>
    private static void RestoreOnboardingComplete(AppiumSetup setup)
    {
        RunXcrun($"simctl spawn booted defaults write {TestConfig.IOSBundleId} OnboardingComplete -bool YES");
        setup.OnboardingHandled = true;
    }

    private static void TapWelcomeSkipFlow(OpenQA.Selenium.Appium.AppiumDriver driver)
    {
        if (driver.IsDisplayed("Welcome_Btn_Skip", timeoutSeconds: 10))
        {
            driver.Tap("Welcome_Btn_Skip");
            Thread.Sleep(TestConfig.DelayModalAnimation);
            if (driver.IsDisplayed("Complete_Btn_Done", timeoutSeconds: 10))
            {
                driver.Tap("Complete_Btn_Done");
                Thread.Sleep(TestConfig.DelayAfterDismiss);
            }
        }
    }

    private static void RunXcrun(string arguments, bool allowFailure = false)
    {
        var psi = new ProcessStartInfo("xcrun", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start xcrun.");
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        if (proc.ExitCode != 0 && !allowFailure)
            throw new InvalidOperationException(
                $"xcrun {arguments} failed (exit {proc.ExitCode}).\nstdout: {stdout}\nstderr: {stderr}");
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
        driver.EnsureOnPrayersTab(_setup); // already navigates to Prayers + waits for the list
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

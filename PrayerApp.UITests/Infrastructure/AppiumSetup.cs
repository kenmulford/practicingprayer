using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.Android.Interfaces;
using OpenQA.Selenium.Appium.iOS;
using Xunit;

namespace PrayerApp.UITests.Infrastructure;

/// <summary>
/// Shared xUnit class fixture that manages the Appium driver lifecycle.
/// One driver instance is shared across all tests in a collection.
/// </summary>
public class AppiumSetup : IAsyncLifetime
{
    public AppiumDriver Driver { get; private set; } = null!;

    /// <summary>Whether onboarding has been handled (dismissed or verified) this session.</summary>
    public bool OnboardingHandled { get; set; }

    public async Task InitializeAsync()
    {
        // Register the SQLitePCL provider before the first in-process SQLite use
        // below (TestDataSeed.SeedAsync builds the seed DB on the test host).
        // #158 swapped sqlite-net-pcl -> sqlite-net-base, which no longer
        // implicitly registers a provider; the app does this at MauiProgram.cs
        // (Batteries_V2.Init), but the UITest host has no MAUI startup to do it.
        SQLitePCL.Batteries_V2.Init();

        // Seed deterministic baseline data BEFORE Appium launches the app.
        // Terminates the app, pushes a pre-built SQLite file into the app's
        // data dir, so every suite starts with known Collections/Cards/Prayers.
        await TestDataSeed.SeedAsync();

        // Pre-seed OnboardingComplete=true in NSUserDefaults (iOS only) so the
        // welcome popup never appears. Replaces the in-suite DismissOnboardingIfPresent
        // flow, which depended on popup-render timing and was empirically flaky
        // across iOS versions / simulator devices.
        await TestDataSeed.PreSeedOnboardingCompleteAsync();

        CreateDriver();
        // Wait for the app to fully load (splash screen + initial page render).
        // Inline 3000ms — one-off post-driver-create splash settle, not part of
        // the TestConfig.Delay* per-test sweep (#11). Tuning this number is a
        // session-level concern, not a per-action concern.
        await Task.Delay(3000);
    }

    /// <summary>
    /// Check if the driver session and app are alive using the documented
    /// mobile: queryAppState API. If the app isn't in the foreground,
    /// try to activate it. If the session is dead, recreate it.
    /// </summary>
    public void EnsureSessionAlive()
    {
        try
        {
            var appId = TestConfig.IsIOS ? TestConfig.IOSBundleId : TestConfig.AndroidPackage;
            var paramName = TestConfig.IsIOS ? "bundleId" : "appId";
            var state = Driver.ExecuteScript("mobile: queryAppState",
                new Dictionary<string, object> { { paramName, appId } });
            int appState = Convert.ToInt32(state);

            if (appState < 3) // not running or background-suspended
            {
                Driver.ActivateApp(appId);
                Thread.Sleep(TestConfig.DelayLongSettle);
            }
        }
        catch (WebDriverException)
        {
            RecreateDriver();
        }
    }

    /// <summary>Tear down the current driver and create a fresh session with retry.</summary>
    private void RecreateDriver()
    {
        try { Driver.Quit(); } catch { }
        try { Driver.Dispose(); } catch { }

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            CreateDriver();
            Thread.Sleep(TestConfig.DelayAppRelaunch);
            OnboardingHandled = false;

            try
            {
                Driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
                _ = Driver.PageSource;
                return;
            }
            catch
            {
                try { Driver.Quit(); } catch { }
                try { Driver.Dispose(); } catch { }
            }
        }
        // All 3 attempts failed — driver may be unusable; next test will likely throw
    }

    private void CreateDriver()
    {
        var options = TestConfig.GetOptions();

        if (TestConfig.IsAndroid)
        {
            var androidDriver = new AndroidDriver(TestConfig.AppiumServerUri, options, TestConfig.SessionTimeout);
            Driver = androidDriver;
            Driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
            ApplyAndroidTreeReadSettings(androidDriver);
            return;
        }

        Driver = new IOSDriver(TestConfig.AppiumServerUri, options, TestConfig.SessionTimeout);
        Driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
    }

    /// <summary>
    /// Issue #117: make the shared UiAutomator2 session's accessibility-tree read
    /// reliable on the Cards page right after an in-place tap-to-expand. Without
    /// these, probing chips on the transient/non-idle post-expand window
    /// intermittently returns a blank tree (root <c>displayed=false</c>, 0 children),
    /// even though <c>adb shell uiautomator dump</c> and a FRESH Appium session both
    /// read a full ~112 KB tree at the same instant — so the defect is the shared
    /// session's read, not the app's realization.
    /// <para>
    /// Applied here (not as a capability) so the single code path covers the initial
    /// session AND every <see cref="RecreateDriver"/> retry. Android-only: these are
    /// UiAutomator2 settings; the iOS XCUITest driver ignores them.
    /// </para>
    /// <para>
    /// Sourced from the appium-uiautomator2-driver Settings API
    /// (github.com/appium/appium-uiautomator2-driver — "Settings"):
    /// <list type="bullet">
    /// <item><c>enableMultiWindows=true</c> — "include all windows that the user can
    /// interact with … while building the XML page source". UiAutomator2 defaults to
    /// only the single active window; mid-animation that active window can be a
    /// transient one whose root reads <c>displayed=false</c> (the blank tree).
    /// Aggregating all windows pulls the real app window into the source so the chips
    /// are present once on screen.</item>
    /// <item><c>waitForIdleTimeout=100</c> (default 10000 ms) — the per-lookup wait for
    /// the UI to go idle. A MAUI CollectionView mid-expand is never fully idle, so the
    /// default makes each lookup block ~10 s then read a stale/transient frame. The doc
    /// says: "Consider lowering the value of this setting if you experience long delays
    /// while interacting with accessibility elements." 100 ms keeps lookups responsive.</item>
    /// </list>
    /// Deliberately NOT set: <c>allowInvisibleElements</c> (would let IsDisplayed match
    /// <c>displayed=false</c> chips → false greens) and <c>ignoreUnimportantViews</c>
    /// (hierarchy compression can drop the very chip nodes under assertion).
    /// </para>
    /// </summary>
    private static void ApplyAndroidTreeReadSettings(AndroidDriver driver)
    {
        try
        {
            // Settings setter issues the documented Update Settings command
            // (POST /session/:id/appium/settings), using the EXACT UiAutomator2
            // Setting names from the driver docs. Accessed through the IHasSettings
            // interface (AndroidDriver implements it). Avoids the typed Configurator*
            // convenience methods, whose key (the UiAutomator Configurator's idle
            // timeout) differs from the driver Setting documented in the Settings API.
            ((IHasSettings)driver).Settings = new Dictionary<string, object>
            {
                ["enableMultiWindows"] = true,
                ["waitForIdleTimeout"] = 100,
            };
        }
        catch (WebDriverException)
        {
            // Best-effort: an older UiAutomator2 build that rejects a key shouldn't
            // abort session creation. The tests still fall back to their settle +
            // controlled-scroll path; they just lose the tree-read hardening.
        }
    }

    public async Task DisposeAsync()
    {
        if (Driver != null)
        {
            Driver.Quit();
            Driver.Dispose();
        }
        await Task.CompletedTask;
    }
}

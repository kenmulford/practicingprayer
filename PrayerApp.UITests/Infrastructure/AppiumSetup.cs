using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
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

    /// <summary>Whether the current session is responsive. Tests can check this to skip gracefully.</summary>
    public bool SessionHealthy { get; private set; } = true;

    public async Task InitializeAsync()
    {
        CreateDriver();
        // Wait for the app to fully load (splash screen + initial page render)
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
                Thread.Sleep(2000);
            }
        }
        catch (Exception)
        {
            RecreateDriver();
        }
    }

    /// <summary>Tear down the current driver and create a fresh session with retry.</summary>
    private void RecreateDriver()
    {
        SessionHealthy = false;
        try { Driver.Quit(); } catch { }
        try { Driver.Dispose(); } catch { }

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            CreateDriver();
            Thread.Sleep(5000);
            OnboardingHandled = false;

            try
            {
                Driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
                _ = Driver.PageSource;
                SessionHealthy = true;
                return;
            }
            catch
            {
                try { Driver.Quit(); } catch { }
                try { Driver.Dispose(); } catch { }
            }
        }
        // All 3 attempts failed — SessionHealthy stays false
    }

    private void CreateDriver()
    {
        var options = TestConfig.GetOptions();

        Driver = TestConfig.IsAndroid
            ? new AndroidDriver(TestConfig.AppiumServerUri, options, TestConfig.SessionTimeout)
            : new IOSDriver(TestConfig.AppiumServerUri, options, TestConfig.SessionTimeout);

        Driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
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

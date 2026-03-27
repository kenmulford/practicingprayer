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

    public async Task InitializeAsync()
    {
        CreateDriver();
        // Wait for the app to fully load (splash screen + initial page render)
        await Task.Delay(3000);
    }

    /// <summary>
    /// Check if the driver session is alive. If the UiAutomator2 instrumentation
    /// crashed, recreate the driver to recover.
    /// </summary>
    public void EnsureSessionAlive()
    {
        try
        {
            // Quick health check — PageSource requires a live session.
            // Use try/catch around ImplicitWait too since it throws
            // NotImplementedException when the session is fully dead.
            Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(1);
            _ = Driver.PageSource;
            Driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
        }
        catch (Exception)
        {
            RecreateDriver();
        }
    }

    /// <summary>Tear down the current driver and create a fresh session.</summary>
    private void RecreateDriver()
    {
        try { Driver.Quit(); } catch { }
        try { Driver.Dispose(); } catch { }
        CreateDriver();
        Thread.Sleep(5000); // Wait for app to fully load after session restart
        OnboardingHandled = false; // Onboarding may show again

        // Verify the new session is responsive — including ImplicitWait
        // which throws NotImplementedException on dead sessions
        try
        {
            Driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
            _ = Driver.PageSource;
        }
        catch (Exception)
        {
            // Second attempt — sometimes WDA needs extra time on iOS
            Thread.Sleep(5000);
            try
            {
                Driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
                _ = Driver.PageSource;
            }
            catch (Exception)
            {
                // Third attempt — full recreate
                try { Driver.Quit(); } catch { }
                try { Driver.Dispose(); } catch { }
                CreateDriver();
                Thread.Sleep(5000);
            }
        }
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

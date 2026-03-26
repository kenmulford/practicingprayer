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
        var options = TestConfig.GetOptions();

        Driver = TestConfig.IsAndroid
            ? new AndroidDriver(TestConfig.AppiumServerUri, options, TestConfig.SessionTimeout)
            : new IOSDriver(TestConfig.AppiumServerUri, options, TestConfig.SessionTimeout);

        Driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;

        // Wait for the app to fully load (splash screen + initial page render)
        await Task.Delay(3000);
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

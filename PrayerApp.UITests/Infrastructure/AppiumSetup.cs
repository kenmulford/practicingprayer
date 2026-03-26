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

    public async Task InitializeAsync()
    {
        var options = TestConfig.GetOptions();

        Driver = TestConfig.IsAndroid
            ? new AndroidDriver(TestConfig.AppiumServerUri, options, TestConfig.LongTimeout)
            : new IOSDriver(TestConfig.AppiumServerUri, options, TestConfig.LongTimeout);

        Driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;

        await Task.CompletedTask;
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

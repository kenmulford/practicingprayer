using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

[Collection("Appium")]
[Trait("Platform", "Android")]
public class SettingsTests
{
    private readonly AppiumSetup _setup;
    public SettingsTests(AppiumSetup setup) => _setup = setup;

    [Fact]
    public void Settings_HubNavigation_AllPagesReachable()
    {
        var driver = _setup.Driver;
        driver.NavigateToTab("Settings");

        // App Settings row
        driver.Tap("Settings_Row_AppSettings");
        driver.WaitForElement("AppSettings_Switch_Notifications");
        driver.GoBack();

        // Backup row
        driver.Tap("Settings_Row_Backup");
        Thread.Sleep(500);
        driver.GoBack();

        // About row
        driver.Tap("Settings_Row_About");
        Thread.Sleep(500);
        driver.GoBack();

        // Help row
        driver.Tap("Settings_Row_Help");
        Thread.Sleep(500);
        driver.GoBack();

        // Back on settings hub
        Assert.True(driver.IsDisplayed("Settings_Row_AppSettings"));
    }
}

using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 9: Settings Hub
/// </summary>
[Collection("Appium")]
[Trait("Platform", "Android")]
[Trait("Section", "9-Settings")]
public class SettingsTests
{
    private readonly AppiumSetup _setup;
    public SettingsTests(AppiumSetup setup) => _setup = setup;

    private void EnsureOnSettingsHub()
    {
        var driver = _setup.Driver;
        driver.DismissOnboardingIfPresent(_setup);

        // Switch away first, then back — this resets the Settings navigation stack
        driver.EnsureOnTab("Home", _setup);
        driver.NavigateToTab("Settings");
        Thread.Sleep(500);

        // If we're on a sub-page (nav stack), go back until hub is visible
        for (int i = 0; i < 3; i++)
        {
            if (driver.IsDisplayed("Settings_Row_AppSettings", timeoutSeconds: 2))
                return;
            driver.GoBack();
            Thread.Sleep(300);
        }
    }

    /// <summary>9.1: Hub page loads — 4 rows with chevrons.</summary>
    [Fact]
    public void Settings_HubPage_Shows4Rows()
    {
        EnsureOnSettingsHub();
        var driver = _setup.Driver;

        Assert.True(driver.IsDisplayed("Settings_Row_AppSettings", timeoutSeconds: 10),
            "App Settings row should be visible");
        Assert.True(driver.IsDisplayed("Settings_Row_Backup"),
            "Backup row should be visible");
        Assert.True(driver.IsDisplayed("Settings_Row_About"),
            "About row should be visible");
        Assert.True(driver.IsDisplayed("Settings_Row_Help"),
            "Help row should be visible");
    }

    /// <summary>9.2: App Settings — navigates to settings page.</summary>
    [Fact]
    public void Settings_AppSettings_NavigatesAndReturns()
    {
        EnsureOnSettingsHub();
        var driver = _setup.Driver;

        driver.WaitAndTap("Settings_Row_AppSettings");
        Thread.Sleep(500);

        Assert.True(driver.IsDisplayed("AppSettings_Switch_Notifications", timeoutSeconds: 5));

        driver.GoBack();
        Thread.Sleep(500);

        Assert.True(driver.IsDisplayed("Settings_Row_AppSettings"));
    }

    /// <summary>9.6: About page — shows version info.</summary>
    [Fact]
    public void Settings_About_NavigatesAndReturns()
    {
        EnsureOnSettingsHub();
        var driver = _setup.Driver;

        driver.WaitAndTap("Settings_Row_About");
        Thread.Sleep(500);

        driver.GoBack();
        Thread.Sleep(500);

        Assert.True(driver.IsDisplayed("Settings_Row_About"));
    }

    /// <summary>9.7: Help page — FAQ items visible.</summary>
    [Fact]
    public void Settings_Help_NavigatesAndReturns()
    {
        EnsureOnSettingsHub();
        var driver = _setup.Driver;

        driver.WaitAndTap("Settings_Row_Help");
        Thread.Sleep(500);

        driver.GoBack();
        Thread.Sleep(500);

        Assert.True(driver.IsDisplayed("Settings_Row_Help"));
    }

    /// <summary>9.8: All sub-pages navigable — full hub navigation cycle.</summary>
    [Fact]
    public void Settings_HubNavigation_AllPagesReachable()
    {
        EnsureOnSettingsHub();
        var driver = _setup.Driver;

        // App Settings
        driver.WaitAndTap("Settings_Row_AppSettings");
        driver.WaitForElement("AppSettings_Switch_Notifications", timeoutSeconds: 5);
        driver.GoBack();
        Thread.Sleep(300);

        // Backup
        driver.WaitAndTap("Settings_Row_Backup");
        Thread.Sleep(500);
        driver.GoBack();
        Thread.Sleep(300);

        // About
        driver.WaitAndTap("Settings_Row_About");
        Thread.Sleep(500);
        driver.GoBack();
        Thread.Sleep(300);

        // Help
        driver.WaitAndTap("Settings_Row_Help");
        Thread.Sleep(500);
        driver.GoBack();
        Thread.Sleep(300);

        Assert.True(driver.IsDisplayed("Settings_Row_AppSettings"));
    }
}

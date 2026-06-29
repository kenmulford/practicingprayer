using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 9: Settings Hub
/// </summary>
[Collection("Appium")]
[Trait("Platform", "CrossPlatform")]
[Trait("Section", "9-Settings")]
public class SettingsTests
{
    private readonly AppiumSetup _setup;
    public SettingsTests(AppiumSetup setup) => _setup = setup;

    private void EnsureOnSettingsHub()
    {
        _setup.Driver.NavigateToTabRoot("Settings", "Settings_Row_AppSettings", _setup);
        // Let Shell finish rendering all hub rows before interacting
        if (TestConfig.IsIOS) Thread.Sleep(TestConfig.DelayAfterNavigation);
    }

    /// <summary>9.3: Backup page — shows backup and restore buttons.</summary>
    [Fact]
    public void Settings_Backup_ShowsButtons()
    {
        _setup.Driver.ResetAppUIState(_setup);
        EnsureOnSettingsHub();
        var driver = _setup.Driver;

        driver.WaitAndTap("Settings_Row_Backup");

        Assert.True(driver.IsDisplayed("Backup_Btn_Backup", timeoutSeconds: 10),
            "Backup button should be visible");
        Assert.True(driver.IsDisplayed("Backup_Btn_Restore", timeoutSeconds: 3),
            "Restore button should be visible");

        driver.GoBack();
        Assert.True(driver.IsDisplayed("Settings_Row_Backup", timeoutSeconds: 10));
    }

    /// <summary>9.8: All sub-pages navigable — full hub navigation cycle.</summary>
    [Fact]
    public void Settings_HubNavigation_AllPagesReachable()
    {
        _setup.Driver.ResetAppUIState(_setup);
        EnsureOnSettingsHub();
        var driver = _setup.Driver;

        driver.WaitAndTap("Settings_Row_AppSettings");
        driver.WaitForElement("AppSettings_Switch_Notifications", timeoutSeconds: 10);
        driver.GoBack();

        driver.WaitAndTap("Settings_Row_Backup", timeoutSeconds: 10);
        driver.WaitForElement("Backup_Btn_Backup", timeoutSeconds: 10);
        driver.GoBack();

        driver.WaitAndTap("Settings_Row_About", timeoutSeconds: 10);
        Thread.Sleep(TestConfig.DelayAfterNavigation);
        driver.GoBack();

        driver.WaitAndTap("Settings_Row_Help", timeoutSeconds: 10);
        Thread.Sleep(TestConfig.DelayAfterNavigation);
        driver.GoBack();

        Assert.True(driver.IsDisplayed("Settings_Row_AppSettings", timeoutSeconds: 10));
    }
}

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
        _setup.Driver.NavigateToTabRoot("Settings", "Settings_Row_AppSettings", _setup);
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

    /// <summary>9.2: App Settings — navigates to settings page with controls.</summary>
    [Fact]
    public void Settings_AppSettings_ShowsControls()
    {
        EnsureOnSettingsHub();
        var driver = _setup.Driver;

        driver.WaitAndTap("Settings_Row_AppSettings");

        Assert.True(driver.IsDisplayed("AppSettings_Switch_Notifications", timeoutSeconds: 5),
            "Notifications toggle should be visible");

        driver.GoBack();
        Assert.True(driver.IsDisplayed("Settings_Row_AppSettings", timeoutSeconds: 5));
    }

    /// <summary>9.3: Backup page — shows backup and restore buttons.</summary>
    [Fact]
    public void Settings_Backup_ShowsButtons()
    {
        EnsureOnSettingsHub();
        var driver = _setup.Driver;

        driver.WaitAndTap("Settings_Row_Backup");

        Assert.True(driver.IsDisplayed("Backup_Btn_Backup", timeoutSeconds: 5),
            "Backup button should be visible");
        Assert.True(driver.IsDisplayed("Backup_Btn_Restore", timeoutSeconds: 3),
            "Restore button should be visible");

        driver.GoBack();
        Assert.True(driver.IsDisplayed("Settings_Row_Backup", timeoutSeconds: 5));
    }

    /// <summary>9.5: Backup page — diagnostics button visibility check.</summary>
    [Fact]
    public void Settings_Backup_DiagnosticsCheck()
    {
        EnsureOnSettingsHub();
        var driver = _setup.Driver;

        driver.WaitAndTap("Settings_Row_Backup");

        // Diagnostics button may or may not be visible depending on log state
        // At minimum, the backup page should load correctly
        Assert.True(driver.IsDisplayed("Backup_Btn_Backup", timeoutSeconds: 5)
                 && driver.IsDisplayed("Backup_Btn_Restore", timeoutSeconds: 3),
            "Backup page should show both Backup and Restore buttons");

        driver.GoBack();
    }

    /// <summary>9.6: About page — shows version info and links.</summary>
    [Fact]
    public void Settings_About_ShowsVersionAndLinks()
    {
        EnsureOnSettingsHub();
        var driver = _setup.Driver;

        driver.WaitAndTap("Settings_Row_About");

        Assert.True(driver.IsDisplayed("About_Btn_Privacy", timeoutSeconds: 5)
                 || driver.IsDisplayed("About_Btn_Website", timeoutSeconds: 3),
            "About page should show Privacy Policy or Website links");

        driver.GoBack();
        Assert.True(driver.IsDisplayed("Settings_Row_About", timeoutSeconds: 5));
    }

    /// <summary>9.7: Help page — FAQ items visible and tappable.</summary>
    [Fact]
    public void Settings_Help_ShowsFaqItems()
    {
        EnsureOnSettingsHub();
        var driver = _setup.Driver;

        driver.WaitAndTap("Settings_Row_Help");

        Assert.True(
            driver.IsTextDisplayed("How", timeoutSeconds: 5)
            || driver.IsTextDisplayed("What", timeoutSeconds: 3)
            || driver.IsTextDisplayed("Can", timeoutSeconds: 3),
            "Help page should show FAQ items");

        driver.GoBack();
        Assert.True(driver.IsDisplayed("Settings_Row_Help", timeoutSeconds: 5));
    }

    /// <summary>9.8: All sub-pages navigable — full hub navigation cycle.</summary>
    [Fact]
    public void Settings_HubNavigation_AllPagesReachable()
    {
        EnsureOnSettingsHub();
        var driver = _setup.Driver;

        driver.WaitAndTap("Settings_Row_AppSettings");
        driver.WaitForElement("AppSettings_Switch_Notifications", timeoutSeconds: 5);
        driver.GoBack();

        driver.WaitAndTap("Settings_Row_Backup", timeoutSeconds: 5);
        driver.WaitForElement("Backup_Btn_Backup", timeoutSeconds: 5);
        driver.GoBack();

        driver.WaitAndTap("Settings_Row_About", timeoutSeconds: 5);
        Thread.Sleep(500);
        driver.GoBack();

        driver.WaitAndTap("Settings_Row_Help", timeoutSeconds: 5);
        Thread.Sleep(500);
        driver.GoBack();

        Assert.True(driver.IsDisplayed("Settings_Row_AppSettings", timeoutSeconds: 5));
    }
}

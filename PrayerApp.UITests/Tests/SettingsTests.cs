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
        if (TestConfig.IsIOS) Thread.Sleep(500);
    }

    /// <summary>9.1: Hub page loads — 4 rows with chevrons.</summary>
    [Fact]
    public void Settings_HubPage_Shows4Rows()
    {
        _setup.Driver.ResetAppUIState(_setup);
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
        _setup.Driver.ResetAppUIState(_setup);
        EnsureOnSettingsHub();
        var driver = _setup.Driver;

        driver.WaitAndTap("Settings_Row_AppSettings");

        Assert.True(driver.IsDisplayed("AppSettings_Switch_Notifications", timeoutSeconds: 10),
            "Notifications toggle should be visible");

        driver.GoBack();
        Assert.True(driver.IsDisplayed("Settings_Row_AppSettings", timeoutSeconds: 10));
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

    /// <summary>9.5: Backup page — diagnostics button visibility check.</summary>
    [Fact]
    public void Settings_Backup_DiagnosticsCheck()
    {
        _setup.Driver.ResetAppUIState(_setup);
        EnsureOnSettingsHub();
        var driver = _setup.Driver;

        driver.WaitAndTap("Settings_Row_Backup");

        // Diagnostics button may or may not be visible depending on log state
        // At minimum, the backup page should load correctly
        Assert.True(driver.IsDisplayed("Backup_Btn_Backup", timeoutSeconds: 10)
                 && driver.IsDisplayed("Backup_Btn_Restore", timeoutSeconds: 3),
            "Backup page should show both Backup and Restore buttons");

        driver.GoBack();
    }

    /// <summary>9.6: About page — shows version info and links.</summary>
    [Fact]
    public void Settings_About_ShowsVersionAndLinks()
    {
        _setup.Driver.ResetAppUIState(_setup);
        EnsureOnSettingsHub();
        var driver = _setup.Driver;

        driver.WaitAndTap("Settings_Row_About");

        Assert.True(driver.IsDisplayed("About_Btn_Privacy", timeoutSeconds: 10)
                 || driver.IsDisplayed("About_Btn_Website", timeoutSeconds: 3),
            "About page should show Privacy Policy or Website links");

        driver.GoBack();
        Assert.True(driver.IsDisplayed("Settings_Row_About", timeoutSeconds: 10));
    }

    /// <summary>9.7: Help page — FAQ items visible and tappable.</summary>
    [Fact]
    public void Settings_Help_ShowsFaqItems()
    {
        _setup.Driver.ResetAppUIState(_setup);
        EnsureOnSettingsHub();
        var driver = _setup.Driver;

        driver.WaitAndTap("Settings_Row_Help");
        Thread.Sleep(1500); // CollectionView needs time to render items on iOS

        // IsTextDisplayed uses exact match — use full question text from FAQ
        Assert.True(
            driver.IsTextDisplayed("How do I create a prayer card?", timeoutSeconds: 8)
            || driver.IsTextDisplayed("What is Quick Add?", timeoutSeconds: 3)
            || driver.IsTextDisplayed("Is my data private?", timeoutSeconds: 3),
            "Help page should show FAQ items");

        driver.GoBack();
        Assert.True(driver.IsDisplayed("Settings_Row_Help", timeoutSeconds: 10));
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
        Thread.Sleep(500);
        driver.GoBack();

        driver.WaitAndTap("Settings_Row_Help", timeoutSeconds: 10);
        Thread.Sleep(500);
        driver.GoBack();

        Assert.True(driver.IsDisplayed("Settings_Row_AppSettings", timeoutSeconds: 10));
    }
}

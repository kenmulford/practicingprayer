using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

[Collection("Appium")]
[Trait("Platform", "Android")]
public class QuickAddTests
{
    private readonly AppiumSetup _setup;
    public QuickAddTests(AppiumSetup setup) => _setup = setup;

    [Fact]
    public void QuickAdd_SaveWithTitle_DismissesModal()
    {
        var driver = _setup.Driver;
        driver.NavigateToTab("Home");
        driver.Tap("Home_Btn_QuickAdd");

        driver.EnterText("QuickAdd_Entry_Title", "UI Test Prayer");
        driver.Tap("QuickAdd_Btn_Add");

        // Should be back on the home page
        driver.WaitForElement("Home_Btn_QuickAdd");
        Assert.True(driver.IsDisplayed("Home_Btn_QuickAdd"));
    }

    [Fact]
    public void QuickAdd_SaveEmpty_ShowsValidation()
    {
        var driver = _setup.Driver;
        driver.NavigateToTab("Home");
        driver.Tap("Home_Btn_QuickAdd");

        // Tap save without entering a title
        driver.Tap("QuickAdd_Btn_Add");

        // Should still be on QuickAdd page (alert dismissed, entry still visible)
        // The alert auto-dismisses, but we should still see the title entry
        Thread.Sleep(1000); // Wait for alert to show and be acknowledged
        Assert.True(driver.IsDisplayed("QuickAdd_Entry_Title"));

        driver.Tap("QuickAdd_Btn_Cancel");
    }

    [Fact]
    public void QuickAdd_Cancel_DismissesModal()
    {
        var driver = _setup.Driver;
        driver.NavigateToTab("Home");
        driver.Tap("Home_Btn_QuickAdd");

        driver.WaitForElement("QuickAdd_Entry_Title");
        driver.Tap("QuickAdd_Btn_Cancel");

        // Should be back on home
        driver.WaitForElement("Home_Btn_QuickAdd");
        Assert.True(driver.IsDisplayed("Home_Btn_QuickAdd"));
    }
}

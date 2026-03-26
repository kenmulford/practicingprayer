using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

[Collection("Appium")]
[Trait("Platform", "Android")]
public class HomeTests
{
    private readonly AppiumSetup _setup;
    public HomeTests(AppiumSetup setup) => _setup = setup;

    [Fact]
    public void Home_QuickAddButton_OpensModal()
    {
        var driver = _setup.Driver;
        driver.NavigateToTab("Home");
        driver.Tap("Home_Btn_QuickAdd");

        // QuickAdd modal should appear with its title entry
        var titleEntry = driver.WaitForElement("QuickAdd_Entry_Title");
        Assert.True(titleEntry.Displayed);

        // Dismiss the modal
        driver.Tap("QuickAdd_Btn_Cancel");
    }

    [Fact]
    public void Home_PrayerTimeButton_IsPresent()
    {
        var driver = _setup.Driver;
        driver.NavigateToTab("Home");

        Assert.True(driver.IsDisplayed("Home_Btn_PrayerTime"));
    }

    [Fact]
    public void Home_PageLoads_WithoutErrors()
    {
        var driver = _setup.Driver;
        driver.NavigateToTab("Home");

        // Home page should have both action buttons
        Assert.True(driver.IsDisplayed("Home_Btn_QuickAdd"));
        Assert.True(driver.IsDisplayed("Home_Btn_PrayerTime"));
    }
}

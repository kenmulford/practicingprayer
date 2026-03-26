using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

[Collection("Appium")]
[Trait("Platform", "Android")]
public class PrayerListTests
{
    private readonly AppiumSetup _setup;
    public PrayerListTests(AppiumSetup setup) => _setup = setup;

    [Fact]
    public void Prayers_PageLoads_ShowsFilterButtons()
    {
        var driver = _setup.Driver;
        driver.NavigateToTab("Prayers");

        Assert.True(driver.IsDisplayed("List_Filter_Active"));
        Assert.True(driver.IsDisplayed("List_Filter_Answered"));
        Assert.True(driver.IsDisplayed("List_Filter_All"));
    }

    [Fact]
    public void Prayers_FilterButtons_AreClickable()
    {
        var driver = _setup.Driver;
        driver.NavigateToTab("Prayers");

        // Tap each filter — should not crash
        driver.Tap("List_Filter_Answered");
        Thread.Sleep(300);
        driver.Tap("List_Filter_All");
        Thread.Sleep(300);
        driver.Tap("List_Filter_Active");
        Thread.Sleep(300);

        // Still on the page
        Assert.True(driver.IsDisplayed("List_Filter_Active"));
    }

    [Fact]
    public void Prayers_SearchBar_IsPresent()
    {
        var driver = _setup.Driver;
        driver.NavigateToTab("Prayers");

        Assert.True(driver.IsDisplayed("List_Search_Prayers"));
    }
}

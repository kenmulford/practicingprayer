using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 2: Home Tab
/// </summary>
[Collection("Appium")]
[Trait("Platform", "Android")]
[Trait("Section", "2-Home")]
public class HomeTests
{
    private readonly AppiumSetup _setup;
    public HomeTests(AppiumSetup setup) => _setup = setup;

    /// <summary>2.1: Home loads with data — Quick Add and Prayer Time buttons visible.</summary>
    [Fact]
    public void Home_PageLoads_ShowsDashboard()
    {
        _setup.Driver.EnsureOnTab("Home", _setup);
        var driver = _setup.Driver;

        Assert.True(driver.IsDisplayed("Home_Btn_QuickAdd"),
            "Quick Add button should be visible on Home page");
        Assert.True(driver.IsDisplayed("Home_Btn_PrayerTime"),
            "Prayer Time button should be visible on Home page");
    }
}

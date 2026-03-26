using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

[Collection("Appium")]
[Trait("Platform", "Android")]
public class TagTests
{
    private readonly AppiumSetup _setup;
    public TagTests(AppiumSetup setup) => _setup = setup;

    [Fact]
    public void Tags_PageLoads_ShowsTagList()
    {
        var driver = _setup.Driver;
        driver.NavigateToTab("Tags");

        var list = driver.WaitForElement("Tags_List_Tags");
        Assert.True(list.Displayed);
    }

    [Fact]
    public void Tags_PageLoads_WithoutErrors()
    {
        var driver = _setup.Driver;
        driver.NavigateToTab("Tags");

        // The page should load and show the tag list
        Assert.True(driver.IsDisplayed("Tags_List_Tags"));
    }
}

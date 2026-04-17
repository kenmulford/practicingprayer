using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 2: Home Tab
/// </summary>
[Collection("Appium")]
[Trait("Platform", "CrossPlatform")]
[Trait("Section", "2-Home")]
public class HomeTests
{
    private readonly AppiumSetup _setup;
    public HomeTests(AppiumSetup setup) => _setup = setup;

    /// <summary>2.1: Home loads with data — Quick Add and Prayer Time buttons visible.</summary>
    [Fact]
    public void Home_PageLoads_ShowsDashboard()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Home", _setup);
        var driver = _setup.Driver;

        Assert.True(driver.IsDisplayed("Home_Btn_QuickAdd"),
            "Quick Add button should be visible on Home page");
        Assert.True(driver.IsDisplayed("Home_Btn_PrayerTime"),
            "Prayer Time button should be visible on Home page");
    }

    /// <summary>2.2: Metric cards — all four metric cards render on home page.</summary>
    [Fact]
    public void Home_MetricCards_Visible()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Home", _setup);
        var driver = _setup.Driver;

        // Home content is inside a ScrollView — scroll if not immediately visible
        if (!driver.IsDisplayed("Home_Metric_Cards", timeoutSeconds: 10))
            driver.ScrollDownTo("Home_Metric_Cards", maxScrolls: 2);

        Assert.True(driver.IsDisplayed("Home_Metric_Cards", timeoutSeconds: 3),
            "Active Cards metric should be visible");
        Assert.True(driver.IsDisplayed("Home_Metric_Unanswered", timeoutSeconds: 3),
            "Unanswered Prayers metric should be visible");
        Assert.True(driver.IsDisplayed("Home_Metric_LastPrayed", timeoutSeconds: 3),
            "Last Prayed metric should be visible");
        Assert.True(driver.IsDisplayed("Home_Metric_Overdue", timeoutSeconds: 3),
            "Overdue metric should be visible");
    }

    /// <summary>2.3: Tap Active Cards metric — navigates to Prayer Cards tab.</summary>
    [Fact]
    public void Home_TapActiveCards_NavigatesToCardsTab()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Home", _setup);
        var driver = _setup.Driver;

        driver.WaitAndTap("Home_Metric_Cards", timeoutSeconds: 10);
        Thread.Sleep(TestConfig.DelayAfterNavigation);

        Assert.True(
            driver.IsDisplayed("Cards_List_Cards", timeoutSeconds: 10)
            || driver.IsDisplayed("Cards_Search", timeoutSeconds: 3),
            "Should navigate to Prayer Cards tab after tapping Active Cards metric");

        // Return to Home for subsequent tests
        driver.NavigateToTab("Home");
    }

    /// <summary>2.4: Tap Unanswered metric — navigates to Prayers tab.</summary>
    [Fact]
    public void Home_TapUnanswered_NavigatesToPrayersTab()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Home", _setup);
        var driver = _setup.Driver;

        driver.WaitAndTap("Home_Metric_Unanswered", timeoutSeconds: 10);
        Thread.Sleep(TestConfig.DelayAfterNavigation);

        Assert.True(
            driver.IsDisplayed("List_Filter_Active", timeoutSeconds: 10)
            || driver.IsDisplayed("List_Search_Prayers", timeoutSeconds: 3),
            "Should navigate to Prayers tab after tapping Unanswered metric");

        // Return to Home for subsequent tests
        driver.NavigateToTab("Home");
    }
}

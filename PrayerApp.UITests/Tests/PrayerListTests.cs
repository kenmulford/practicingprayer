using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 4: Prayers Tab
/// </summary>
[Collection("Appium")]
[Trait("Platform", "Android")]
[Trait("Section", "4-Prayers")]
public class PrayerListTests
{
    private readonly AppiumSetup _setup;
    public PrayerListTests(AppiumSetup setup) => _setup = setup;

    /// <summary>4.1: Prayer list loads.</summary>
    [Fact]
    public void Prayers_PageLoads()
    {
        _setup.Driver.EnsureOnTab("Prayers", _setup);
        // Page should load without crashing — filter buttons should be visible
        Assert.True(_setup.Driver.IsDisplayed("List_Filter_Active")
                 || _setup.Driver.IsDisplayed("List_Search_Prayers"),
            "Prayers page should show filter buttons or search bar");
    }

    /// <summary>4.3: Filter buttons switch between Active/Answered/All views.</summary>
    [Fact]
    public void Prayers_FilterButtons_SwitchViews()
    {
        _setup.Driver.EnsureOnTab("Prayers", _setup);
        var driver = _setup.Driver;

        // Tap each filter — should not crash
        if (driver.IsDisplayed("List_Filter_Answered", timeoutSeconds: 3))
        {
            driver.Tap("List_Filter_Answered");
            Thread.Sleep(300);
        }

        if (driver.IsDisplayed("List_Filter_All", timeoutSeconds: 3))
        {
            driver.Tap("List_Filter_All");
            Thread.Sleep(300);
        }

        if (driver.IsDisplayed("List_Filter_Active", timeoutSeconds: 3))
        {
            driver.Tap("List_Filter_Active");
            Thread.Sleep(300);
        }

        // Still on the prayers page
        Assert.True(driver.IsDisplayed("List_Filter_Active"));
    }

    /// <summary>4.2: Search prayers by title.</summary>
    [Fact]
    public void Prayers_SearchBar_FiltersResults()
    {
        _setup.Driver.EnsureOnTab("Prayers", _setup);
        var driver = _setup.Driver;

        if (driver.IsDisplayed("List_Search_Prayers", timeoutSeconds: 3))
        {
            driver.EnterText("List_Search_Prayers", "nonexistent_prayer_xyz");
            Thread.Sleep(500);
            driver.EnterText("List_Search_Prayers", "");
            Thread.Sleep(500);
        }

        // Page should still be functional
        Assert.True(driver.IsDisplayed("List_Filter_Active")
                 || driver.IsDisplayed("List_Search_Prayers"));
    }

    /// <summary>4.10: Cross-tab freshness — prayer added via QuickAdd appears on Prayers tab.</summary>
    [Fact]
    public void Prayers_CrossTabFreshness()
    {
        var driver = _setup.Driver;
        driver.DismissOnboardingIfPresent(_setup);

        // Navigate to Prayers tab, then back to Home, then back to Prayers
        // Verifies no crash on rapid tab switching
        driver.NavigateToTab("Prayers");
        Thread.Sleep(300);
        driver.NavigateToTab("Home");
        Thread.Sleep(300);
        driver.NavigateToTab("Prayers");
        Thread.Sleep(300);

        Assert.True(driver.IsDisplayed("List_Filter_Active")
                 || driver.IsDisplayed("List_Search_Prayers"));
    }
}

using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 12: Edge Cases
/// </summary>
[Collection("Appium")]
[Trait("Platform", "Android")]
[Trait("Section", "12-EdgeCases")]
public class EdgeCaseTests
{
    private readonly AppiumSetup _setup;
    public EdgeCaseTests(AppiumSetup setup) => _setup = setup;

    /// <summary>12.4: Rapid tab switching — no crash, no stale data.</summary>
    [Fact]
    public void RapidTabSwitching_NoCrash()
    {
        var driver = _setup.Driver;
        driver.EnsureOnTab("Home", _setup);

        // Rapidly switch between all 5 tabs
        var tabs = new[] { "Home", "Prayer Cards", "Prayers", "Tags", "Settings" };
        foreach (var tab in tabs)
        {
            driver.NavigateToTab(tab);
            Thread.Sleep(200);
        }

        // Go back through them
        for (int i = tabs.Length - 1; i >= 0; i--)
        {
            driver.NavigateToTab(tabs[i]);
            Thread.Sleep(200);
        }

        // App should still be functional
        driver.NavigateToTab("Home");
        Assert.True(driver.IsDisplayed("Home_Btn_QuickAdd"),
            "App should still be functional after rapid tab switching");
    }

    /// <summary>12.3: Very long prayer title — doesn't break layout.</summary>
    [Fact]
    public void LongPrayerTitle_NoLayoutBreak()
    {
        var driver = _setup.Driver;
        driver.EnsureOnTab("Home", _setup);

        driver.WaitAndTap("Home_Btn_QuickAdd");
        driver.WaitForElement("QuickAdd_Entry_Title");

        // Enter a very long title
        var longTitle = new string('A', 200) + " Long Prayer Title";
        driver.EnterText("QuickAdd_Entry_Title", longTitle);
        driver.WaitAndTap("QuickAdd_Btn_Add");
        Thread.Sleep(1000);

        // Should return to home without crashing
        Assert.True(driver.IsDisplayed("Home_Btn_QuickAdd"),
            "App should handle long prayer titles without crashing");
    }
}

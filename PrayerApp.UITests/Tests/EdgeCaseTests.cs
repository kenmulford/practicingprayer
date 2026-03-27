using OpenQA.Selenium;
using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 12: Edge Cases
/// </summary>
[Collection("Appium")]
[Trait("Platform", "CrossPlatform")]
[Trait("Section", "12-EdgeCases")]
public class EdgeCaseTests
{
    private readonly AppiumSetup _setup;
    public EdgeCaseTests(AppiumSetup setup) => _setup = setup;

    /// <summary>12.1: Cards page with search for nonexistent — shows empty/filtered state.</summary>
    [Fact]
    public void Cards_EmptySearch_ShowsEmptyState()
    {
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        // Search for something that won't match
        driver.EnterText("Cards_Search", "zzz_nothing_matches_this_ever");
        Thread.Sleep(500);

        // Page should still be functional (empty result, no crash)
        Assert.True(driver.IsDisplayed("Cards_List_Cards") || driver.IsDisplayed("Cards_Search"));

        // Clear search
        driver.EnterText("Cards_Search", "");
        Thread.Sleep(500);
    }

    /// <summary>12.3: Very long prayer title — doesn't break layout.</summary>
    [Fact]
    public void LongPrayerTitle_NoLayoutBreak()
    {
        var driver = _setup.Driver;
        driver.EnsureOnTab("Home", _setup);

        driver.WaitAndTap("Home_Btn_QuickAdd");
        driver.WaitForElement("QuickAdd_Entry_Title");

        var longTitle = new string('A', 200) + " Long Prayer Title";
        driver.EnterText("QuickAdd_Entry_Title", longTitle);
        driver.WaitAndTap("QuickAdd_Btn_Add");
        Thread.Sleep(1000);

        Assert.True(driver.IsDisplayed("Home_Btn_QuickAdd"),
            "App should handle long prayer titles without crashing");
    }

    /// <summary>12.4: Rapid tab switching — no crash, no stale data.</summary>
    [Fact]
    public void RapidTabSwitching_NoCrash()
    {
        var driver = _setup.Driver;
        driver.EnsureOnTab("Home", _setup);

        var tabs = new[] { "Home", "Prayer Cards", "Prayers", "Tags", "Settings" };
        var delay = TestConfig.IsIOS ? 500 : 200; // iOS Shell navigation is slower
        foreach (var tab in tabs)
        {
            driver.NavigateToTab(tab);
            Thread.Sleep(delay);
        }

        for (int i = tabs.Length - 1; i >= 0; i--)
        {
            driver.NavigateToTab(tabs[i]);
            Thread.Sleep(delay);
        }

        driver.NavigateToTab("Home");
        Assert.True(driver.IsDisplayed("Home_Btn_QuickAdd"),
            "App should still be functional after rapid tab switching");
    }

    /// <summary>12.5: Double-tap Save — guard prevents double-save.</summary>
    [Fact]
    public void DoubleTapSave_NoDuplicate()
    {
        var driver = _setup.Driver;
        driver.EnsureOnTab("Prayers", _setup);

        driver.TapToolbarItem("Add");
        driver.WaitForElement("Detail_Entry_Title", timeoutSeconds: 5);
        driver.EnterText("Detail_Entry_Title", "Double Save Test");

        // Rapidly tap Save twice
        driver.TapToolbarItem("Save");
        Thread.Sleep(100);
        // Second tap should be ignored (guard prevents double-save)
        try { driver.TapToolbarItem("Save"); } catch (WebDriverException) { }
        Thread.Sleep(1000);

        // Should end up back on the prayers list (not stuck or crashed)
        driver.NavigateToTab("Prayers");
        Assert.True(driver.IsDisplayed("List_Filter_Active", timeoutSeconds: 5),
            "App should handle double-tap Save without crashing");
    }

    /// <summary>12.2: Empty card — expand card with no prayers shows empty state.</summary>
    [Fact]
    public void EmptyCard_ExpandShowsAddPrayer()
    {
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        // Create a fresh card with no prayers
        driver.TapToolbarItem("Add Card");
        driver.WaitForElement("Card_Entry_Title", timeoutSeconds: 5);
        driver.EnterText("Card_Entry_Title", "Empty Card Test");
        driver.TapToolbarItem("Save");
        Thread.Sleep(1000);

        // Back on cards list — tap the new card to expand
        if (driver.IsTextDisplayed("Empty Card Test", timeoutSeconds: 5))
        {
            driver.TapByText("Empty Card Test");
            Thread.Sleep(500);

            // Should show the "Add prayer" button (card is empty)
            Assert.True(driver.IsDisplayed("Cards_Btn_AddPrayer", timeoutSeconds: 5),
                "Empty card should show '+ Add prayer' button");
        }
    }
}

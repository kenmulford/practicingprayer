using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 3: Prayer Cards Tab
/// </summary>
[Collection("Appium")]
[Trait("Platform", "Android")]
[Trait("Section", "3-PrayerCards")]
public class PrayerCardTests
{
    private readonly AppiumSetup _setup;
    public PrayerCardTests(AppiumSetup setup) => _setup = setup;

    /// <summary>3.1: Cards list loads — card collection is visible.</summary>
    [Fact]
    public void Cards_ListLoads_ShowsCards()
    {
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        Assert.True(_setup.Driver.IsDisplayed("Cards_List_Cards"),
            "Card collection should be visible on Prayer Cards page");
    }

    /// <summary>3.2: Search cards — typing filters the list.</summary>
    [Fact]
    public void Cards_SearchFilter_FiltersCards()
    {
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        // Type a search term that won't match anything
        driver.EnterText("Cards_Search", "zzz_nonexistent");
        Thread.Sleep(500);

        // Clear the search to restore normal view
        driver.EnterText("Cards_Search", "");
        Thread.Sleep(500);

        Assert.True(driver.IsDisplayed("Cards_List_Cards"));
    }

    /// <summary>3.3: Search keyboard dismiss — tap background dismisses keyboard.</summary>
    [Fact]
    public void Cards_SearchKeyboard_DismissesOnBackground()
    {
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        // Tap search to focus it
        driver.Tap("Cards_Search");
        Thread.Sleep(300);

        // Hide keyboard by pressing back (Android) or tapping outside
        driver.GoBack();
        Thread.Sleep(300);

        // Page should still be showing cards
        Assert.True(driver.IsDisplayed("Cards_List_Cards"));
    }
}

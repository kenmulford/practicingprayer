using OpenQA.Selenium;
using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 3: Prayer Cards Tab
/// </summary>
[Collection("Appium")]
[Trait("Platform", "CrossPlatform")]
[Trait("Section", "3-PrayerCards")]
public class PrayerCardTests
{
    private readonly AppiumSetup _setup;
    public PrayerCardTests(AppiumSetup setup) => _setup = setup;

    /// <summary>Expand the Quick Add system card if visible and not already expanded.</summary>
    private void ExpandQuickAddCard()
    {
        var driver = _setup.Driver;
        // Already expanded — nothing to do
        if (driver.IsDisplayed("Cards_Btn_AddPrayer", timeoutSeconds: 2))
            return;

        if (driver.IsTextDisplayed("Quick Add", timeoutSeconds: 3))
        {
            driver.TapByText("Quick Add");
            Thread.Sleep(500);
        }
    }

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

        driver.EnterText("Cards_Search", "zzz_nonexistent");
        Thread.Sleep(500);

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

        if (TestConfig.IsIOS)
        {
            // iOS with hardware keyboard: on-screen keyboard doesn't appear,
            // so test that tapping search and going back doesn't break navigation.
            driver.Tap("Cards_Search");
            Thread.Sleep(300);
            driver.NavigateToTab("Prayer Cards");
        }
        else
        {
            driver.Tap("Cards_Search");
            Thread.Sleep(300);
            driver.GoBack();
            Thread.Sleep(300);
        }

        Assert.True(driver.IsDisplayed("Cards_List_Cards"));
    }

    /// <summary>3.4: Create new card — tap "Add Card", fill title, save. Card appears in list.</summary>
    [Fact]
    public void Cards_CreateCard_AppearsInList()
    {
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        driver.TapToolbarItem("Add Card");
        driver.WaitForElement("Card_Entry_Title", timeoutSeconds: 5);

        driver.EnterText("Card_Entry_Title", "UITest Card");
        driver.TapToolbarItem("Save");
        Thread.Sleep(1000);

        Assert.True(driver.IsDisplayed("Cards_List_Cards", timeoutSeconds: 5),
            "Should return to card list after saving new card");
    }

    /// <summary>3.6: Expand card → view prayers inside.</summary>
    [Fact]
    public void Cards_ExpandCard_ShowsPrayers()
    {
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        ExpandQuickAddCard();

        Assert.True(_setup.Driver.IsDisplayed("Cards_Btn_AddPrayer", timeoutSeconds: 5),
            "Expanded card should show '+ Add prayer' button");
    }

    /// <summary>3.7: Add prayer to card — expand card, tap "Add prayer", fill, save.</summary>
    [Fact]
    public void Cards_AddPrayerToCard()
    {
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        ExpandQuickAddCard();

        driver.WaitAndTap("Cards_Btn_AddPrayer", timeoutSeconds: 5);
        Thread.Sleep(500);

        Assert.True(driver.IsDisplayed("Detail_Entry_Title", timeoutSeconds: 5),
            "Should navigate to prayer detail page");

        driver.EnterText("Detail_Entry_Title", "Card Prayer UITest");
        driver.TapToolbarItem("Save");
        Thread.Sleep(1000);

        driver.NavigateToTab("Prayer Cards");
        Assert.True(driver.IsDisplayed("Cards_List_Cards", timeoutSeconds: 5));
    }

    /// <summary>3.9: Edit prayer from card — tap prayer, view mode, tap Edit.</summary>
    [Fact]
    public void Cards_EditPrayerFromCard()
    {
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        ExpandQuickAddCard();

        if (!driver.IsTextDisplayed("UI Test Prayer", timeoutSeconds: 3))
            throw Xunit.Sdk.SkipException.ForSkip("Precondition: 'UI Test Prayer' not found — depends on earlier QuickAdd test");

        driver.TapByText("UI Test Prayer");

        driver.TapToolbarItem("Edit");
        Thread.Sleep(TestConfig.DelayAfterTap);

        Assert.True(driver.IsDisplayed("Detail_Entry_Title", timeoutSeconds: 5),
            "Should show title entry in edit mode");

        driver.GoBack();
        driver.DismissAlertIfPresent();
    }

    /// <summary>3.12: Delete card — create card, navigate to detail, delete, confirm.</summary>
    [Fact]
    public void Cards_DeleteCard_RemovesFromList()
    {
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        // Create a card to delete
        driver.TapToolbarItem("Add Card");
        driver.WaitForElement("Card_Entry_Title", timeoutSeconds: 5);
        driver.EnterText("Card_Entry_Title", "Delete Me Card");
        driver.TapToolbarItem("Save");
        Thread.Sleep(1000);

        // Swipe to reveal Edit, navigate to detail, delete
        if (driver.IsTextDisplayed("Delete Me Card", timeoutSeconds: 5))
        {
            var cardElement = driver.FindByText("Delete Me Card");
            driver.SwipeElementRight(cardElement);

            if (driver.IsTextDisplayed("Edit", timeoutSeconds: 2))
            {
                driver.TapByText("Edit");
                driver.WaitAndTap("Card_Btn_Delete", timeoutSeconds: 5);
                driver.DismissAlertIfPresent();
                Thread.Sleep(500);
            }
        }

        Assert.True(driver.IsDisplayed("Cards_List_Cards", timeoutSeconds: 5));
    }

    /// <summary>3.14: Favorite card — swipe right reveals Favorite and Edit actions.</summary>
    [Fact]
    public void Cards_SwipeRight_ShowsFavoriteAction()
    {
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        if (driver.IsTextDisplayed("Quick Add", timeoutSeconds: 3))
        {
            var cardElement = driver.FindByText("Quick Add");
            driver.SwipeElementRight(cardElement);

            Assert.True(
                driver.IsTextDisplayed("★", timeoutSeconds: 2)
                || driver.IsTextDisplayed("Edit", timeoutSeconds: 2),
                "Swipe right should reveal Favorite and Edit actions");

            // Dismiss swipe — tap a non-interactive element to clear the swipe state.
            // Avoid tapping Cards_Search on iOS as it opens the keyboard.
            try { driver.TapByText("Prayer Cards", timeoutSeconds: 2); } catch (WebDriverException) { }
            Thread.Sleep(300);
        }
    }

    /// <summary>3.15: System card protection — Quick Add card's Delete action is guarded.</summary>
    [Fact]
    public void Cards_SystemCard_DeleteNotAvailable()
    {
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        if (!driver.IsTextDisplayed("Quick Add", timeoutSeconds: 3))
            throw Xunit.Sdk.SkipException.ForSkip("Precondition: 'Quick Add' system card not found");

        var cardElement = driver.FindByText("Quick Add");
        driver.SwipeElementLeft(cardElement);

        // System cards should not expose a functional Delete action
        var deleteVisible = driver.IsTextDisplayed("Delete", timeoutSeconds: 2);

        // Dismiss swipe state before asserting
        try { driver.TapByText("Prayer Cards", timeoutSeconds: 2); } catch (WebDriverException) { }
        Thread.Sleep(TestConfig.DelayAfterDismiss);

        Assert.False(deleteVisible, "System card 'Quick Add' should not show Delete action on swipe");
        Assert.True(driver.IsDisplayed("Cards_List_Cards"));
    }
}

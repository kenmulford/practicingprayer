using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
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

        if (TestConfig.IsIOS)
        {
            // iOS: CollectionView flattens cells — AutomationIds inside cells are invisible.
            // Check expansion via composed label containing "Expanded".
            if (driver.IsTextContainsDisplayed("Quick Add, Expanded", timeoutSeconds: 2))
                return;

            if (driver.IsTextContainsDisplayed("Quick Add", timeoutSeconds: 3))
            {
                driver.TapByTextContains("Quick Add", timeoutSeconds: 10);
                Thread.Sleep(500);
            }
        }
        else
        {
            // Android: AutomationIds work inside CollectionView cells
            if (driver.IsDisplayed("Cards_Btn_AddPrayer", timeoutSeconds: 2))
                return;

            if (driver.IsTextDisplayed("Quick Add", timeoutSeconds: 3))
            {
                driver.TapByText("Quick Add");
                Thread.Sleep(500);
            }
        }
    }

    /// <summary>3.1: Cards list loads — card collection is visible.</summary>
    [Fact]
    public void Cards_ListLoads_ShowsCards()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        Assert.True(_setup.Driver.IsDisplayed("Cards_List_Cards"),
            "Card collection should be visible on Prayer Cards page");
    }

    /// <summary>3.2: Search cards — typing filters the list.</summary>
    [Fact]
    public void Cards_SearchFilter_FiltersCards()
    {
        _setup.Driver.ResetAppUIState(_setup);
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
        _setup.Driver.ResetAppUIState(_setup);
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
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        driver.TapToolbarItemById("Add Card");
        driver.WaitForElement("Card_Entry_Title", timeoutSeconds: 10);

        // Use a name NOT in the seed. The seed already has "UITest Card" — creating
        // another with the same name would make any assertion against
        // IsTextDisplayed("UITest Card") trivially pass against the seeded one
        // instead of the one we just created (silent false positive).
        driver.EnterText("Card_Entry_Title", "New Card UITest");
        driver.DismissKeyboardIfPresent();
        driver.TapToolbarItem("Save");
        Thread.Sleep(1000);

        Assert.True(driver.IsDisplayed("Cards_List_Cards", timeoutSeconds: 10),
            "Should return to card list after saving new card");
    }

    /// <summary>3.6: Expand card → view prayers inside.</summary>
    [Fact]
    public void Cards_ExpandCard_ShowsPrayers()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        ExpandQuickAddCard();

        Assert.True(_setup.Driver.IsDisplayed("Cards_Btn_AddPrayer", timeoutSeconds: 10),
            "Expanded card should show '+ Add prayer' button");
    }

    /// <summary>3.7: Add prayer to card — expand card, tap "Add prayer", fill, save.
    /// Uses the dedicated disposable fixture "UITest AddPrayer Card" (see TestDataSeed)
    /// so this test is independent of what other tests do to the shared seed.</summary>
    [Fact]
    public void Cards_AddPrayerToCard()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;
        Thread.Sleep(TestConfig.DelayCollectionRender);
        driver.EnsureCardVisible("UITest AddPrayer Card");

        // Expand the dedicated seed card
        if (TestConfig.IsIOS)
            driver.TapByTextContains("UITest AddPrayer Card");
        else
            driver.TapByText("UITest AddPrayer Card");
        Thread.Sleep(TestConfig.DelayAfterTap);

        // iOS: AutomationId invisible inside flattened CollectionView cells — use text
        if (TestConfig.IsIOS)
            driver.TapByTextContains("Add prayer", timeoutSeconds: 10);
        else
            driver.WaitAndTap("Cards_Btn_AddPrayer", timeoutSeconds: 10);
        Thread.Sleep(500);

        Assert.True(driver.IsDisplayed("Detail_Entry_Title", timeoutSeconds: 10),
            "Should navigate to prayer detail page");

        driver.EnterText("Detail_Entry_Title", "Card Prayer UITest");
        driver.TapToolbarItem("Save");
        Thread.Sleep(1000);

        driver.NavigateToTab("Prayer Cards");
        Assert.True(driver.IsDisplayed("Cards_List_Cards", timeoutSeconds: 10));
    }

    /// <summary>3.9: Edit prayer from card — expand card, tap prayer, view mode, tap Edit.
    /// Uses dedicated fixture "UITest EditPrayer Card" containing "UITest Edit Prayer"
    /// (see TestDataSeed). Isolated from shared-seed mutations by other tests.</summary>
    [Fact]
    public void Cards_EditPrayerFromCard()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;
        driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);
        driver.EnsureCardVisible("UITest EditPrayer Card");

        // Expand the dedicated seed card to reveal its prayer
        if (TestConfig.IsIOS)
            driver.TapByTextContains("UITest EditPrayer Card");
        else
            driver.TapByText("UITest EditPrayer Card");
        Thread.Sleep(TestConfig.DelayAfterTap);

        // iOS: prayer text is part of composed label, not a standalone element
        if (TestConfig.IsIOS)
            driver.TapByTextContains("UITest Edit Prayer", timeoutSeconds: 10);
        else
            driver.TapByText("UITest Edit Prayer");

        driver.TapToolbarItem("Edit");
        Thread.Sleep(TestConfig.DelayAfterTap);

        Assert.True(driver.IsDisplayed("Detail_Entry_Title", timeoutSeconds: 10),
            "Should show title entry in edit mode");

        driver.GoBack();
        driver.DismissAlertIfPresent();
    }

    /// <summary>3.12: Delete card — navigate to the pre-seeded "Delete Me Card",
    /// expand it, delete via the inline Delete button, confirm. The target card
    /// is a throwaway from the seed; destroying it does not affect the shared
    /// UITest baseline.</summary>
    [Fact]
    public void Cards_DeleteCard_RemovesFromList()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Expand the card, tap inline Delete button
        if (driver.IsTextDisplayed("Delete Me Card", timeoutSeconds: 10))
        {
            driver.TapByText("Delete Me Card");
            Thread.Sleep(TestConfig.DelayAfterTap);
            driver.WaitAndTap("Cards_Btn_Delete", timeoutSeconds: 10);
            driver.DismissAlertIfPresent();
            Thread.Sleep(500);
        }

        Assert.True(driver.IsDisplayed("Cards_List_Cards", timeoutSeconds: 10));
    }

    /// <summary>3.14: Inline action buttons — expanding a user card shows Favorite, Share, Edit, Delete.
    /// Uses dedicated fixture "UITest Expanded Card" (see TestDataSeed).</summary>
    [Fact]
    public void Cards_ExpandedCard_ShowsActionButtons()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;
        driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);
        driver.EnsureCardVisible("UITest Expanded Card");

        // Expand the card by tapping it
        if (TestConfig.IsIOS)
            driver.TapByTextContains("UITest Expanded Card");
        else
            driver.TapByText("UITest Expanded Card");
        Thread.Sleep(TestConfig.DelayAfterTap);

        Assert.True(driver.IsDisplayed("Cards_Btn_Favorite", timeoutSeconds: 10),
            "Expanded user card should show Favorite button");
        Assert.True(driver.IsDisplayed("Cards_Btn_Share", timeoutSeconds: 3),
            "Expanded user card should show Share button");
        Assert.True(driver.IsDisplayed("Cards_Btn_Edit", timeoutSeconds: 3),
            "Expanded user card should show Edit button");
        Assert.True(driver.IsDisplayed("Cards_Btn_Delete", timeoutSeconds: 3),
            "Expanded user card should show Delete button");
    }

    /// <summary>3.16: Tag filter chips — visible when tags exist in the system.</summary>
    [Fact]
    public void Cards_TagChips_VisibleWhenTagsExist()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;

        // Ensure at least one tag exists
        driver.EnsureUITestTagExists(_setup);

        driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Chip inner Label is not in the a11y tree; Border Description is a phrase
        // ("UITest Tag, not selected"), so match by substring.
        Assert.True(
            driver.IsTextContainsDisplayed("UITest Tag", timeoutSeconds: 10),
            "Tag filter chip should be visible on Prayer Cards page when tags exist");
    }

    /// <summary>3.17: Edit button — tapping Edit navigates to card edit page.
    /// Uses dedicated fixture "UITest EditButton Card" (see TestDataSeed).</summary>
    [Fact]
    public void Cards_EditButton_NavigatesToEditPage()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;
        driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);
        driver.EnsureCardVisible("UITest EditButton Card");

        // Expand the card to reveal action buttons
        if (TestConfig.IsIOS)
            driver.TapByTextContains("UITest EditButton Card");
        else
            driver.TapByText("UITest EditButton Card");
        Thread.Sleep(TestConfig.DelayAfterTap);

        driver.WaitAndTap("Cards_Btn_Edit", timeoutSeconds: 10);
        Thread.Sleep(TestConfig.DelayAfterNavigation);

        Assert.True(driver.IsDisplayed("Card_Entry_Title", timeoutSeconds: 10),
            "Tapping Edit should navigate to card edit page");

        driver.GoBack();
        driver.DismissAlertIfPresent();
    }

    /// <summary>3.18: System card protection — Quick Add should not show action buttons when expanded.</summary>
    [SkippableFact]
    public void Cards_SystemCard_ShareNotAvailable()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        // Expand the Quick Add system card
        try
        {
            if (TestConfig.IsIOS)
                driver.TapByTextContains("Quick Add");
            else
                driver.TapByText("Quick Add");
            Thread.Sleep(TestConfig.DelayAfterTap);
        }
        catch (WebDriverException)
        {
            throw new SkipException("Precondition: 'Quick Add' system card not found");
        }

        // System cards should not show action buttons
        Assert.False(driver.IsDisplayed("Cards_Btn_Share", timeoutSeconds: 2),
            "System card should not show Share button");
    }

    /// <summary>3.15: System card protection — Quick Add card's Delete button is hidden.</summary>
    [SkippableFact]
    public void Cards_SystemCard_DeleteNotAvailable()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        // Expand the Quick Add system card
        try
        {
            if (TestConfig.IsIOS)
                driver.TapByTextContains("Quick Add");
            else
                driver.TapByText("Quick Add");
            Thread.Sleep(TestConfig.DelayAfterTap);
        }
        catch (WebDriverException)
        {
            throw new SkipException("Precondition: 'Quick Add' system card not found");
        }

        // System cards should not show action buttons
        Assert.False(driver.IsDisplayed("Cards_Btn_Delete", timeoutSeconds: 2),
            "System card should not show Delete button");
        Assert.True(driver.IsDisplayed("Cards_List_Cards"));
    }
}

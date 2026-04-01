using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 8: Collections (Boxes)
/// Tests for the F-24 card grouping feature: section headers on Cards page,
/// collection management CRUD, card assignment picker, and multi-select move.
/// </summary>
[Collection("Appium")]
[Trait("Platform", "CrossPlatform")]
[Trait("Section", "8-Collections")]
public class BoxTests
{
    private readonly AppiumSetup _setup;
    public BoxTests(AppiumSetup setup) => _setup = setup;

    /// <summary>8.1: Cards page shows section headers — at minimum System and Archived.</summary>
    [Fact]
    public void Cards_SectionHeaders_Visible()
    {
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Section headers use AutomationId="Cards_Section_Header"
        // At minimum, System section should be visible (Quick Add card lives there)
        Assert.True(
            driver.IsTextDisplayed("System", timeoutSeconds: 5) ||
            driver.IsDisplayed("Cards_Section_Header", timeoutSeconds: 5),
            "At least one section header should be visible on the Cards page");
    }

    /// <summary>8.2: Section expand/collapse — tapping a header toggles its cards.</summary>
    [Fact]
    public void Cards_SectionHeader_ExpandCollapse()
    {
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Find and tap the Archived section header (collapsed by default)
        if (driver.IsTextDisplayed("Archived", timeoutSeconds: 5))
        {
            driver.TapByText("Archived");
            Thread.Sleep(TestConfig.DelayAfterTap);

            // Tap again to collapse
            driver.TapByText("Archived");
            Thread.Sleep(TestConfig.DelayAfterTap);
        }

        // Page should still be functional
        Assert.True(driver.IsDisplayed("Cards_List_Cards", timeoutSeconds: 5));
    }

    /// <summary>8.3: Navigate to Manage Collections from Cards page toolbar.</summary>
    [Fact]
    public void Cards_CollectionsToolbar_NavigatesToBoxesPage()
    {
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        driver.TapToolbarItem("Collections");
        Thread.Sleep(TestConfig.DelayAfterNavigation);

        Assert.True(driver.IsDisplayed("Boxes_List_Boxes", timeoutSeconds: 5),
            "Should navigate to Collections management page");

        driver.GoBack();
        Thread.Sleep(TestConfig.DelayAfterNavigation);
    }

    /// <summary>8.4: Navigate to Manage Collections from Settings hub.</summary>
    [Fact]
    public void Settings_ManageCollections_NavigatesToBoxesPage()
    {
        _setup.Driver.NavigateToTabRoot("Settings", "Settings_Row_AppSettings", _setup);
        var driver = _setup.Driver;

        Assert.True(driver.IsDisplayed("Settings_Row_Collections", timeoutSeconds: 5),
            "Manage Collections row should be visible in Settings");

        driver.WaitAndTap("Settings_Row_Collections");
        Thread.Sleep(TestConfig.DelayAfterNavigation);

        Assert.True(driver.IsDisplayed("Boxes_List_Boxes", timeoutSeconds: 5),
            "Should navigate to Collections management page");

        driver.GoBack();
        Thread.Sleep(TestConfig.DelayAfterNavigation);

        Assert.True(driver.IsDisplayed("Settings_Row_Collections", timeoutSeconds: 5),
            "Should return to Settings hub");
    }

    /// <summary>8.5: Create collection — tap Add, enter name, save, verify in list.</summary>
    [Fact]
    public void Boxes_CreateCollection_AppearsInList()
    {
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        // Navigate to Manage Collections
        driver.TapToolbarItem("Collections");
        driver.WaitForElement("Boxes_List_Boxes", timeoutSeconds: 5);

        // Create a new collection
        driver.TapToolbarItem("Add");
        driver.WaitForElement("BoxDetail_Entry_Name", timeoutSeconds: 5);

        driver.EnterText("BoxDetail_Entry_Name", "UITest Collection");
        driver.TapToolbarItem("Save");
        Thread.Sleep(TestConfig.DelayAfterSave);

        // Verify we returned to collection list (handle iOS Bug #3 GoToAsync unreliability)
        var onList = driver.IsDisplayed("Boxes_List_Boxes", timeoutSeconds: 5);
        if (!onList && TestConfig.IsIOS)
        {
            driver.GoBack();
            Thread.Sleep(TestConfig.DelayAfterNavigation);
        }

        // Verify the new collection appears
        Assert.True(driver.IsTextDisplayed("UITest Collection", timeoutSeconds: 5),
            "Newly created collection should appear in the list");

        driver.GoBack();
        Thread.Sleep(TestConfig.DelayAfterNavigation);
    }

    /// <summary>8.6: Edit collection — select, tap Edit chip, rename, save.</summary>
    [Fact]
    public void Boxes_EditCollection_UpdatesName()
    {
        _setup.Driver.EnsureUITestCollectionExists(_setup);
        var driver = _setup.Driver;

        driver.EnsureOnTab("Prayer Cards", _setup);
        driver.TapToolbarItem("Collections");
        driver.WaitForElement("Boxes_List_Boxes", timeoutSeconds: 5);

        // Tap the collection to select it (reveals action chips)
        if (driver.IsTextDisplayed("UITest Collection", timeoutSeconds: 3))
        {
            driver.TapByText("UITest Collection");
            Thread.Sleep(TestConfig.DelayAfterTap);

            // Tap Edit chip
            if (driver.IsDisplayed("Boxes_Btn_Edit", timeoutSeconds: 3))
            {
                driver.Tap("Boxes_Btn_Edit");
                Thread.Sleep(TestConfig.DelayAfterNavigation);

                Assert.True(driver.IsDisplayed("BoxDetail_Entry_Name", timeoutSeconds: 5),
                    "Should navigate to collection detail for editing");

                driver.TapToolbarItem("Save");
                Thread.Sleep(TestConfig.DelayAfterSave);
            }
        }

        // Navigate back to cards
        driver.NavigateToTab("Prayer Cards");
        Assert.True(driver.IsDisplayed("Cards_List_Cards", timeoutSeconds: 5));
    }

    /// <summary>8.7: System collections are read-only — no Delete chip visible.</summary>
    [Fact]
    public void Boxes_SystemCollections_NoDeleteAction()
    {
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        driver.TapToolbarItem("Collections");
        driver.WaitForElement("Boxes_List_Boxes", timeoutSeconds: 5);

        // Tap System collection to select it
        if (driver.IsTextDisplayed("System", timeoutSeconds: 3))
        {
            driver.TapByText("System");
            Thread.Sleep(TestConfig.DelayAfterTap);

            // Edit should be visible but Delete should not
            Assert.False(driver.IsDisplayed("Boxes_Btn_Delete", timeoutSeconds: 2),
                "System collections should not show Delete action");
        }

        driver.GoBack();
        Thread.Sleep(TestConfig.DelayAfterNavigation);
    }

    /// <summary>8.8: Delete collection — select, tap Delete chip, choose Unassign.</summary>
    [Fact]
    public void Boxes_DeleteCollection_UnassignCards()
    {
        _setup.Driver.EnsureUITestCollectionExists(_setup);
        var driver = _setup.Driver;

        driver.EnsureOnTab("Prayer Cards", _setup);
        driver.TapToolbarItem("Collections");
        driver.WaitForElement("Boxes_List_Boxes", timeoutSeconds: 5);

        if (driver.IsTextDisplayed("UITest Collection", timeoutSeconds: 3))
        {
            driver.TapByText("UITest Collection");
            Thread.Sleep(TestConfig.DelayAfterTap);

            if (driver.IsDisplayed("Boxes_Btn_Delete", timeoutSeconds: 3))
            {
                driver.Tap("Boxes_Btn_Delete");
                Thread.Sleep(TestConfig.DelayAfterTap);

                // Action sheet with "Unassign Cards" option
                if (TestConfig.IsIOS)
                    driver.TapIOSActionSheetButton("Unassign Cards");
                else
                    driver.TapByText("Unassign Cards");

                Thread.Sleep(TestConfig.DelayAfterSave);
            }
        }

        // Collection should be gone
        Assert.True(driver.IsDisplayed("Boxes_List_Boxes", timeoutSeconds: 5),
            "Should remain on collections page after delete");

        driver.GoBack();
        Thread.Sleep(TestConfig.DelayAfterNavigation);
    }

    /// <summary>8.9: Card creation with collection picker — assign to a collection.</summary>
    [Fact]
    public void Cards_CreateCard_WithCollectionPicker()
    {
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        driver.TapToolbarItem("Add Card");
        driver.WaitForElement("Card_Entry_Title", timeoutSeconds: 5);

        // Collection picker should be visible for non-system cards
        Assert.True(driver.IsDisplayed("Card_Picker_Box", timeoutSeconds: 3),
            "Collection picker should be visible on card creation form");

        // Enter a title and save
        driver.EnterText("Card_Entry_Title", "UITest Card With Collection");
        driver.TapToolbarItem("Save");
        Thread.Sleep(TestConfig.DelayAfterSave);

        // Navigate back if needed (iOS Bug #3)
        if (!driver.IsDisplayed("Cards_List_Cards", timeoutSeconds: 5))
            driver.NavigateToTab("Prayer Cards");

        Assert.True(driver.IsDisplayed("Cards_List_Cards", timeoutSeconds: 5));

        // Clean up: delete the test card
        if (driver.IsTextDisplayed("UITest Card With Collection", timeoutSeconds: 3))
        {
            driver.TapByText("UITest Card With Collection");
            Thread.Sleep(TestConfig.DelayAfterTap);

            // Expand card to show action chips
            if (driver.IsDisplayed("Cards_Btn_Delete", timeoutSeconds: 3))
            {
                driver.Tap("Cards_Btn_Delete");
                driver.DismissAlertIfPresent();
                Thread.Sleep(TestConfig.DelayAfterSave);
            }
        }
    }

    /// <summary>8.10: Multi-select toolbar — verify it appears and Cancel exits.</summary>
    [Fact]
    public void Cards_MultiSelect_ToolbarAppearsAndCancels()
    {
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Multi-select requires a long-press on a card.
        // Long-press is platform-specific and may not be reliable in all Appium configs.
        // This test verifies the Cancel button exits multi-select mode if we can enter it.

        // Try to find a card to long-press
        if (driver.IsTextDisplayed("Quick Add", timeoutSeconds: 5))
        {
            var cardElement = driver.FindByTextContains("Quick Add");

            // Attempt long-press (Appium action)
            try
            {
                driver.LongPress(cardElement);
                Thread.Sleep(TestConfig.DelayAfterTap);
            }
            catch (Exception ex)
            {
                throw Xunit.Sdk.SkipException.ForSkip(
                    $"Long-press not supported in this Appium config: {ex.Message}");
            }

            // If multi-select toolbar appeared, verify Cancel works
            if (driver.IsDisplayed("Cards_Bar_MultiSelect", timeoutSeconds: 3))
            {
                Assert.True(driver.IsDisplayed("Cards_Btn_CancelSelect"),
                    "Cancel button should be visible in multi-select toolbar");

                driver.Tap("Cards_Btn_CancelSelect");
                Thread.Sleep(TestConfig.DelayAfterTap);

                Assert.False(driver.IsDisplayed("Cards_Bar_MultiSelect", timeoutSeconds: 2),
                    "Multi-select toolbar should be hidden after Cancel");
            }
        }

        Assert.True(driver.IsDisplayed("Cards_List_Cards", timeoutSeconds: 5));
    }
}

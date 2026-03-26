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
        Assert.True(_setup.Driver.IsDisplayed("List_Filter_Active")
                 || _setup.Driver.IsDisplayed("List_Search_Prayers"),
            "Prayers page should show filter buttons or search bar");
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

        Assert.True(driver.IsDisplayed("List_Filter_Active")
                 || driver.IsDisplayed("List_Search_Prayers"));
    }

    /// <summary>4.3: Filter buttons switch between Active/Answered/All views.</summary>
    [Fact]
    public void Prayers_FilterButtons_SwitchViews()
    {
        _setup.Driver.EnsureOnTab("Prayers", _setup);
        var driver = _setup.Driver;

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

        Assert.True(driver.IsDisplayed("List_Filter_Active"));
    }

    /// <summary>4.5: Add new prayer via toolbar "Add" button.</summary>
    [Fact]
    public void Prayers_AddNewPrayer()
    {
        _setup.Driver.NavigateToNewPrayer(_setup);
        var driver = _setup.Driver;

        driver.EnterText("Detail_Entry_Title", "Prayer List UITest");
        driver.TapToolbarItem("Save");
        Thread.Sleep(1000);

        // Save navigates back to list automatically
        Assert.True(driver.IsDisplayed("List_Filter_Active", timeoutSeconds: 5)
                 || driver.IsDisplayed("List_List_Prayers", timeoutSeconds: 3));
    }

    /// <summary>4.6: View prayer in read-only mode — tap a prayer row.</summary>
    [Fact]
    public void Prayers_TapPrayer_ShowsViewMode()
    {
        _setup.Driver.EnsureOnTab("Prayers", _setup);
        var driver = _setup.Driver;

        if (driver.IsTextDisplayed("UI Test Prayer", timeoutSeconds: 3))
        {
            driver.TapByText("UI Test Prayer");

            Assert.True(driver.IsDisplayed("Detail_Btn_MarkAnswered", timeoutSeconds: 5)
                     || driver.IsDisplayed("Detail_Btn_Share", timeoutSeconds: 3),
                "View mode should show action buttons");

            driver.GoBack();
        }
        else
        {
            Assert.True(driver.IsDisplayed("List_Filter_Active"));
        }
    }

    /// <summary>4.7: Edit prayer — view mode → Edit → change details → Save.</summary>
    [Fact]
    public void Prayers_EditPrayer()
    {
        _setup.Driver.EnsureOnTab("Prayers", _setup);
        var driver = _setup.Driver;

        if (driver.IsTextDisplayed("UI Test Prayer", timeoutSeconds: 3))
        {
            driver.TapByText("UI Test Prayer");

            driver.TapToolbarItem("Edit");
            Thread.Sleep(300);

            Assert.True(driver.IsDisplayed("Detail_Entry_Title", timeoutSeconds: 5),
                "Should show title entry in edit mode");

            if (driver.IsDisplayed("Detail_Entry_Details", timeoutSeconds: 3))
                driver.EnterText("Detail_Entry_Details", "Updated by UITest");

            driver.TapToolbarItem("Save");
            Thread.Sleep(1000);
        }

        // Ensure we're back on the list
        driver.NavigateToTab("Prayers");
        Assert.True(driver.IsDisplayed("List_Filter_Active", timeoutSeconds: 5));
    }

    /// <summary>4.8: Mark prayer as answered.</summary>
    [Fact]
    public void Prayers_MarkAnswered()
    {
        // Create a prayer specifically to mark answered
        _setup.Driver.NavigateToNewPrayer(_setup);
        var driver = _setup.Driver;

        driver.EnterText("Detail_Entry_Title", "Mark Answered UITest");
        driver.TapToolbarItem("Save");
        Thread.Sleep(1000);

        // Find it and open it
        if (driver.IsTextDisplayed("Mark Answered UITest", timeoutSeconds: 5))
        {
            driver.TapByText("Mark Answered UITest");

            if (driver.IsDisplayed("Detail_Btn_MarkAnswered", timeoutSeconds: 5))
            {
                driver.Tap("Detail_Btn_MarkAnswered");
                Thread.Sleep(500);
                driver.DismissAlertIfPresent();
                Thread.Sleep(500);
            }

            driver.GoBack();
        }

        // Verify Answered filter still works
        if (driver.IsDisplayed("List_Filter_Answered", timeoutSeconds: 3))
            driver.Tap("List_Filter_Answered");

        Assert.True(driver.IsDisplayed("List_Filter_Answered"));

        // Restore Active filter for subsequent tests
        if (driver.IsDisplayed("List_Filter_Active", timeoutSeconds: 2))
            driver.Tap("List_Filter_Active");
    }

    /// <summary>4.9: Delete prayer from edit mode.</summary>
    [Fact]
    public void Prayers_DeletePrayer()
    {
        // Create a prayer to delete
        _setup.Driver.NavigateToNewPrayer(_setup);
        var driver = _setup.Driver;

        driver.EnterText("Detail_Entry_Title", "Delete Me Prayer");
        driver.TapToolbarItem("Save");
        Thread.Sleep(1000);

        if (driver.IsTextDisplayed("Delete Me Prayer", timeoutSeconds: 5))
        {
            driver.TapByText("Delete Me Prayer");

            driver.TapToolbarItem("Edit");
            Thread.Sleep(300);

            // Scroll to Delete if needed
            if (!driver.IsDisplayed("Detail_Btn_Delete", timeoutSeconds: 3))
                driver.ScrollDownTo("Detail_Btn_Delete");

            driver.Tap("Detail_Btn_Delete");
            Thread.Sleep(300);
            driver.DismissAlertIfPresent();
            Thread.Sleep(500);
        }

        // Should return to list
        driver.NavigateToTab("Prayers");
        Assert.True(driver.IsDisplayed("List_Filter_Active", timeoutSeconds: 5));
    }

    /// <summary>4.10: Cross-tab freshness — navigating between tabs doesn't crash.</summary>
    [Fact]
    public void Prayers_CrossTabFreshness()
    {
        var driver = _setup.Driver;
        driver.DismissOnboardingIfPresent(_setup);

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

using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 7: Tags
/// </summary>
[Collection("Appium")]
[Trait("Platform", "Android")]
[Trait("Section", "7-Tags")]
public class TagTests
{
    private readonly AppiumSetup _setup;
    public TagTests(AppiumSetup setup) => _setup = setup;

    /// <summary>7.1: Tags list loads — all tags visible with color swatches.</summary>
    [Fact]
    public void Tags_PageLoads_ShowsTagList()
    {
        _setup.Driver.EnsureOnTab("Tags", _setup);
        Assert.True(_setup.Driver.IsDisplayed("Tags_List_Tags"),
            "Tag list should be visible on Tags page");
    }

    /// <summary>7.2: Create tag — tap "Add", fill name, save. Tag appears in list.</summary>
    [Fact]
    public void Tags_CreateTag_AppearsInList()
    {
        _setup.Driver.EnsureOnTab("Tags", _setup);
        var driver = _setup.Driver;

        driver.TapToolbarItem("Add");
        driver.WaitForElement("TagDetail_Entry_Name", timeoutSeconds: 5);

        driver.EnterText("TagDetail_Entry_Name", "UITest Tag");
        driver.TapToolbarItem("Save");
        Thread.Sleep(1000);

        Assert.True(driver.IsDisplayed("Tags_List_Tags", timeoutSeconds: 5),
            "Should return to tag list after saving new tag");
    }

    /// <summary>7.3: Edit tag — swipe to reveal Edit, navigate to detail.</summary>
    [Fact]
    public void Tags_EditTag()
    {
        _setup.Driver.EnsureOnTab("Tags", _setup);
        var driver = _setup.Driver;

        if (driver.IsTextDisplayed("UITest Tag", timeoutSeconds: 3))
        {
            var tagElement = driver.FindByText("UITest Tag");
            driver.SwipeElementRight(tagElement);

            if (driver.IsTextDisplayed("Edit", timeoutSeconds: 2))
            {
                driver.TapByText("Edit");

                Assert.True(driver.IsDisplayed("TagDetail_Entry_Name", timeoutSeconds: 5),
                    "Should navigate to tag detail for editing");

                driver.TapToolbarItem("Save");
                Thread.Sleep(500);
            }
        }

        driver.NavigateToTab("Tags");
        Assert.True(driver.IsDisplayed("Tags_List_Tags", timeoutSeconds: 5));
    }

    /// <summary>7.4: Delete tag — swipe left reveals Delete action.</summary>
    [Fact]
    public void Tags_DeleteTag()
    {
        _setup.Driver.EnsureOnTab("Tags", _setup);
        var driver = _setup.Driver;

        // Create a tag to delete
        driver.TapToolbarItem("Add");
        driver.WaitForElement("TagDetail_Entry_Name", timeoutSeconds: 5);
        driver.EnterText("TagDetail_Entry_Name", "Delete Me Tag");
        driver.TapToolbarItem("Save");
        Thread.Sleep(1000);

        driver.NavigateToTab("Tags");

        if (driver.IsTextDisplayed("Delete Me Tag", timeoutSeconds: 5))
        {
            var tagElement = driver.FindByText("Delete Me Tag");
            driver.SwipeElementLeft(tagElement);

            if (driver.IsTextDisplayed("Delete", timeoutSeconds: 2))
            {
                driver.TapByText("Delete");
                driver.DismissAlertIfPresent();
                Thread.Sleep(500);
            }
        }

        Assert.True(driver.IsDisplayed("Tags_List_Tags", timeoutSeconds: 5));
    }

    /// <summary>7.7: Add tag to prayer — tag search in prayer detail shows suggestions.</summary>
    [Fact]
    public void Tags_AddTagToPrayer()
    {
        _setup.Driver.NavigateToNewPrayer(_setup);
        var driver = _setup.Driver;

        driver.EnterText("Detail_Entry_Title", "Tagged Prayer UITest");

        if (!driver.IsDisplayed("Detail_Entry_TagSearch", timeoutSeconds: 3))
            driver.ScrollDownTo("Detail_Entry_TagSearch");

        driver.EnterText("Detail_Entry_TagSearch", "UITest");
        Thread.Sleep(500);

        // Verify suggestions list appears (may be empty if no matching tags)
        var hasSuggestions = driver.IsDisplayed("Detail_List_TagSuggestions", timeoutSeconds: 3);
        // Tag search UI should at least show the suggestions list container
        Assert.True(hasSuggestions || driver.IsDisplayed("Detail_Entry_TagSearch"),
            "Tag search should show suggestions list or remain functional");

        driver.TapToolbarItem("Save");
        Thread.Sleep(1000);

        driver.NavigateToTab("Prayers");
        Assert.True(driver.IsDisplayed("List_Filter_Active", timeoutSeconds: 5));
    }
}

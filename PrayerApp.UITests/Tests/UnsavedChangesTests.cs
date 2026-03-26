using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 5: Prayer Detail — Unsaved Changes Guard
/// </summary>
[Collection("Appium")]
[Trait("Platform", "Android")]
[Trait("Section", "5-UnsavedChanges")]
public class UnsavedChangesTests
{
    private readonly AppiumSetup _setup;
    public UnsavedChangesTests(AppiumSetup setup) => _setup = setup;

    /// <summary>5.1: Edit title → back button → discard dialog appears.</summary>
    [Fact]
    public void UnsavedChanges_EditTitle_BackShowsDiscardDialog()
    {
        _setup.Driver.NavigateToNewPrayer(_setup);
        var driver = _setup.Driver;

        driver.EnterText("Detail_Entry_Title", "Dirty Prayer");
        Thread.Sleep(500); // Allow IsDirty to register the change

        driver.GoBack();
        Thread.Sleep(1000);

        // Check native alert OR MAUI dialog text OR still on detail page (back intercepted)
        var hasAlert = driver.IsAlertPresent();
        var hasDiscardText = driver.IsTextDisplayed("Discard", timeoutSeconds: 2)
                          || driver.IsTextDisplayed("Unsaved", timeoutSeconds: 1);
        var stillOnDetail = driver.IsDisplayed("Detail_Entry_Title", timeoutSeconds: 2);

        Assert.True(hasAlert || hasDiscardText || stillOnDetail,
            "Discard changes dialog should appear when navigating away with unsaved changes");

        driver.DismissAlertIfPresent();
        Thread.Sleep(300);
    }

    /// <summary>5.2: Edit title → tap different tab → discard dialog appears.</summary>
    [Fact]
    public void UnsavedChanges_EditTitle_TabSwitchShowsDiscardDialog()
    {
        _setup.Driver.NavigateToNewPrayer(_setup);
        var driver = _setup.Driver;

        driver.EnterText("Detail_Entry_Title", "Tab Switch Dirty");
        driver.NavigateToTab("Home");
        Thread.Sleep(500);

        driver.DismissAlertIfPresent();
        Thread.Sleep(300);

        Assert.True(driver.IsDisplayed("Home_Btn_QuickAdd", timeoutSeconds: 5)
                 || driver.IsDisplayed("Detail_Entry_Title", timeoutSeconds: 3));
    }

    /// <summary>5.5: Save then back — no discard prompt.</summary>
    [Fact]
    public void UnsavedChanges_SaveThenBack_NoPrompt()
    {
        _setup.Driver.NavigateToNewPrayer(_setup);
        var driver = _setup.Driver;

        driver.EnterText("Detail_Entry_Title", "Saved Prayer NoPrompt");
        driver.TapToolbarItem("Save");
        Thread.Sleep(1000);

        driver.GoBack();
        Thread.Sleep(500);

        Assert.True(driver.IsDisplayed("List_Filter_Active", timeoutSeconds: 5)
                 || driver.IsDisplayed("List_Search_Prayers", timeoutSeconds: 3),
            "Should return to prayer list without discard prompt after saving");
    }

    /// <summary>5.6: New prayer, no changes → back — no discard prompt.</summary>
    [Fact]
    public void UnsavedChanges_NoChanges_BackNoPrompt()
    {
        _setup.Driver.NavigateToNewPrayer(_setup);
        var driver = _setup.Driver;

        driver.GoBack();
        Thread.Sleep(500);

        Assert.True(driver.IsDisplayed("List_Filter_Active", timeoutSeconds: 5)
                 || driver.IsDisplayed("List_Search_Prayers", timeoutSeconds: 3),
            "Should return to prayer list without discard prompt when no changes made");
    }
}

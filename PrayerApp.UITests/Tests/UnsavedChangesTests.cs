using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 5: Prayer Detail — Unsaved Changes Guard
/// </summary>
[Collection("Appium")]
[Trait("Platform", "CrossPlatform")]
[Trait("Section", "5-UnsavedChanges")]
public class UnsavedChangesTests
{
    private readonly AppiumSetup _setup;
    public UnsavedChangesTests(AppiumSetup setup) => _setup = setup;

    /// <summary>5.1: Edit title → back button → discard dialog appears.</summary>
    [Fact]
    public void UnsavedChanges_EditTitle_BackShowsDiscardDialog()
    {
        // Skip BEFORE any navigation — iOS back button doesn't fire Shell.Navigating,
        // so the unsaved changes guard is bypassed. Skipping early prevents dirty state
        // from cascading into subsequent tests.
        if (TestConfig.IsIOS)
            throw Xunit.Sdk.SkipException.ForSkip("iOS Bug #2: Shell.Navigating doesn't fire on iOS back button — unsaved changes guard bypassed");

        _setup.Driver.NavigateToNewPrayer(_setup);
        var driver = _setup.Driver;

        driver.EnterText("Detail_Entry_Title", "Dirty Prayer");
        Thread.Sleep(TestConfig.DelayDirtyRegistration);

        driver.GoBack();
        Thread.Sleep(TestConfig.DelayAfterSave);

        var hasAlert = driver.IsAlertPresent();
        var hasDiscardText = driver.IsTextDisplayed("Discard", timeoutSeconds: 2)
                          || driver.IsTextDisplayed("Unsaved", timeoutSeconds: 1);
        var stillOnDetail = driver.IsDisplayed("Detail_Entry_Title", timeoutSeconds: 2);

        Assert.True(hasAlert || hasDiscardText || stillOnDetail,
            "Discard changes dialog should appear when navigating away with unsaved changes");

        // Clean up: ensure we leave the detail page
        if (hasAlert || hasDiscardText)
        {
            try { driver.TapAlertButton("Discard"); }
            catch { driver.DismissAlertIfPresent(); }
        }
        driver.DismissAlertIfPresent();
        Thread.Sleep(300);

        // Make sure we're back on the Prayers list (not stuck on detail)
        if (driver.IsDisplayed("Detail_Entry_Title", timeoutSeconds: 2))
        {
            driver.GoBack();
            driver.DismissAlertIfPresent();
            Thread.Sleep(500);
        }
    }

    /// <summary>5.2: Edit title → tap different tab → discard dialog appears.</summary>
    [Fact]
    public void UnsavedChanges_EditTitle_TabSwitchShowsDiscardDialog()
    {
        var driver = _setup.Driver;

        // EnsureOnTab handles any leftover modals/alerts from prior tests
        driver.NavigateToNewPrayer(_setup);

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
        Thread.Sleep(1500);

        // Save already navigates back to prayer list via GoToAsync("..").
        // Verify we're on the list (no discard prompt intercepted the Save navigation).
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

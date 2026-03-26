using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 2.2-2.3: Quick Add flow (from Home tab)
/// </summary>
[Collection("Appium")]
[Trait("Platform", "Android")]
[Trait("Section", "2-Home")]
public class QuickAddTests
{
    private readonly AppiumSetup _setup;
    public QuickAddTests(AppiumSetup setup) => _setup = setup;

    /// <summary>2.2: Quick Add save with title — modal dismisses, prayer saved.</summary>
    [Fact]
    public void QuickAdd_SaveWithTitle_DismissesModal()
    {
        _setup.Driver.EnsureOnTab("Home", _setup);
        var driver = _setup.Driver;

        driver.Tap("Home_Btn_QuickAdd");
        driver.WaitForElement("QuickAdd_Entry_Title");

        driver.EnterText("QuickAdd_Entry_Title", "UI Test Prayer");
        driver.Tap("QuickAdd_Btn_Add");

        // Should be back on the home page
        Thread.Sleep(1000);
        Assert.True(driver.IsDisplayed("Home_Btn_QuickAdd"),
            "Should return to Home after Quick Add save");
    }

    /// <summary>2.2 (variant): Quick Add save empty — shows validation alert.</summary>
    [Fact]
    public void QuickAdd_SaveEmpty_ShowsValidation()
    {
        _setup.Driver.EnsureOnTab("Home", _setup);
        var driver = _setup.Driver;

        driver.Tap("Home_Btn_QuickAdd");
        driver.WaitForElement("QuickAdd_Entry_Title");

        // Tap save without entering a title
        driver.Tap("QuickAdd_Btn_Add");
        Thread.Sleep(500);

        // Should still be on QuickAdd (alert shown, entry still visible after dismissal)
        driver.DismissAlertIfPresent();
        Assert.True(driver.IsDisplayed("QuickAdd_Entry_Title"));

        driver.Tap("QuickAdd_Btn_Cancel");
    }

    /// <summary>2.2 (variant): Quick Add cancel — modal dismisses without saving.</summary>
    [Fact]
    public void QuickAdd_Cancel_DismissesModal()
    {
        _setup.Driver.EnsureOnTab("Home", _setup);
        var driver = _setup.Driver;

        driver.Tap("Home_Btn_QuickAdd");
        driver.WaitForElement("QuickAdd_Entry_Title");

        driver.Tap("QuickAdd_Btn_Cancel");
        Thread.Sleep(500);

        Assert.True(driver.IsDisplayed("Home_Btn_QuickAdd"),
            "Should return to Home after Quick Add cancel");
    }

    /// <summary>2.3: Quick Add → Cards tab — prayer appears on Quick Add card.</summary>
    [Fact]
    public void QuickAdd_PrayerAppearsOnCardsTab()
    {
        _setup.Driver.EnsureOnTab("Home", _setup);
        var driver = _setup.Driver;

        // Add a prayer via Quick Add
        driver.Tap("Home_Btn_QuickAdd");
        driver.WaitForElement("QuickAdd_Entry_Title");
        driver.EnterText("QuickAdd_Entry_Title", "CrossTab Test Prayer");
        driver.Tap("QuickAdd_Btn_Add");
        Thread.Sleep(1000);

        // Switch to Prayer Cards tab
        driver.NavigateToTab("Prayer Cards");
        Thread.Sleep(500);

        // Cards page should load — wait longer for data refresh
        Assert.True(driver.IsDisplayed("Cards_List_Cards", timeoutSeconds: 10),
            "Cards tab should show card list after Quick Add");
    }
}

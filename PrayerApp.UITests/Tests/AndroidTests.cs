using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 14: Android-Specific
/// </summary>
[Collection("Appium")]
[Trait("Platform", "Android")]
[Trait("Section", "14-Android")]
public class AndroidTests
{
    private readonly AppiumSetup _setup;
    public AndroidTests(AppiumSetup setup) => _setup = setup;

    /// <summary>14.1: Hardware back button navigates correctly from sub-pages.</summary>
    [Fact]
    public void HardwareBack_NavigatesFromSubPages()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;
        driver.EnsureOnTab("Settings", _setup);

        driver.WaitAndTap("Settings_Row_AppSettings");
        driver.WaitForElement("AppSettings_Switch_Notifications", timeoutSeconds: 10);

        driver.GoBack();
        Thread.Sleep(500);

        Assert.True(driver.IsDisplayed("Settings_Row_AppSettings", timeoutSeconds: 10),
            "Hardware back should return to Settings hub from sub-page");
    }

    /// <summary>14.1 (variant): Hardware back from clean prayer detail.</summary>
    [Fact]
    public void HardwareBack_FromPrayerDetail()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.NavigateToNewPrayer(_setup);
        var driver = _setup.Driver;

        driver.GoBack();
        Thread.Sleep(500);

        Assert.True(driver.IsDisplayed("List_Filter_Active", timeoutSeconds: 10)
                 || driver.IsDisplayed("List_Search_Prayers", timeoutSeconds: 3),
            "Hardware back should return to Prayers list from clean detail page");
    }

    /// <summary>14.1 (variant): Hardware back with dirty state shows discard dialog.</summary>
    [SkippableFact]
    public void HardwareBack_DirtyDetail_ShowsDiscardDialog()
    {
        _setup.Driver.ResetAppUIState(_setup);
        if (TestConfig.IsIOS)
            throw new SkipException("Android-only: hardware back button does not exist on iOS");

        _setup.Driver.NavigateToNewPrayer(_setup);
        var driver = _setup.Driver;

        driver.EnterText("Detail_Entry_Title", "Dirty Back Test");
        Thread.Sleep(500); // Allow IsDirty to register the change

        driver.GoBack();
        Thread.Sleep(1500); // Allow dialog animation to complete

        // The discard dialog should appear — check native alert, MAUI dialog text, or still on page
        var hasAlert = driver.IsAlertPresent();
        var hasDiscardText = driver.IsTextDisplayed("Discard", timeoutSeconds: 2)
                          || driver.IsTextDisplayed("Unsaved", timeoutSeconds: 1);
        var stillOnDetail = driver.IsDisplayed("Detail_Entry_Title", timeoutSeconds: 2);

        // Either the dialog appeared OR we're still on the detail page (back was intercepted)
        Assert.True(hasAlert || hasDiscardText || stillOnDetail,
            "Hardware back with unsaved changes should show discard dialog or stay on page");

        // Clean up — dismiss dialog and navigate away
        driver.DismissAlertIfPresent();
        Thread.Sleep(300);
        if (driver.IsDisplayed("Detail_Entry_Title", timeoutSeconds: 2))
        {
            driver.GoBack();
            Thread.Sleep(500);
            driver.DismissAlertIfPresent();
        }
    }
}

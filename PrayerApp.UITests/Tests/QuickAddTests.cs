using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 2.2-2.3: Quick Add flow (from Home tab).
///
/// The bespoke QuickAddPage was retired in issue #43. Home_Btn_QuickAdd now
/// opens ConfirmImportPage in Manual mode: page title "Quick Add",
/// ExistingCard mode preselected, Quick Add card pre-selected (collapsed to
/// summary), one empty prayer row ready to type.
/// </summary>
[Collection("Appium")]
[Trait("Platform", "CrossPlatform")]
[Trait("Section", "2-Home")]
public class QuickAddTests
{
    private readonly AppiumSetup _setup;
    public QuickAddTests(AppiumSetup setup) => _setup = setup;

    // ── Helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Navigate to Home and open the Quick Add flow via Home_Btn_QuickAdd.
    /// Waits for ConfirmImport_Seg_ExistingCard (a body Border element, reliably
    /// located on both iOS and Android) as the page-ready probe.
    /// ToolbarItems are not locatable by AutomationId on Android, so
    /// ConfirmImport_Btn_Save cannot be used as a probe here.
    /// </summary>
    private void OpenQuickAdd()
    {
        var driver = _setup.Driver;
        driver.EnsureOnTab("Home", _setup);
        driver.WaitAndTap("Home_Btn_QuickAdd");
        driver.WaitForElement("ConfirmImport_Seg_ExistingCard", timeoutSeconds: 15);
        Thread.Sleep(TestConfig.DelayModalAnimation);
    }

    /// <summary>
    /// Enter text into the first (and, in Manual mode, only) prayer-title Entry
    /// inside ConfirmImport_List_Prayers. The Entry has no AutomationId; it is
    /// located via XPath on the list container, mirroring ImportFlowTests.
    ///
    /// iOS: XCUIElementTypeTextField; Android: android.widget.EditText.
    /// </summary>
    private static void EnterPrayerTitle(AppiumDriver driver, string text)
    {
        var prayersList = driver.WaitForElement("ConfirmImport_List_Prayers", timeoutSeconds: 10);

        var entryXPath = TestConfig.IsIOS
            ? ".//XCUIElementTypeTextField"
            : ".//android.widget.EditText[@hint='Prayer title' or contains(@content-desc,'Prayer title')]";

        driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
        IReadOnlyCollection<OpenQA.Selenium.IWebElement> entries;
        try
        {
            entries = prayersList.FindElements(By.XPath(entryXPath));
        }
        finally
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
        }

        Assert.True(entries.Count > 0,
            $"Expected at least one prayer-title Entry in ConfirmImport_List_Prayers (XPath: {entryXPath})");

        var entry = entries.First();
        entry.Click();
        Thread.Sleep(TestConfig.DelayAfterTap);
        entry.SendKeys(text);
        driver.DismissKeyboardIfPresent();
    }

    // ── Tests ────────────────────────────────────────────────────

    /// <summary>2.2: Quick Add save with title — prayer saved to Quick Add card.</summary>
    [Fact]
    public void QuickAdd_SaveWithTitle_NavigatesToCardsTab()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;

        OpenQuickAdd();

        // Quick Add card should be preselected — verify "Quick Add" text is visible
        // in the collapsed-to-summary row (SelectedCard.Title label) and a "Change"
        // affordance is present.
        Assert.True(driver.IsTextDisplayed("Quick Add", timeoutSeconds: 5),
            "ConfirmImport in Manual mode should display 'Quick Add' as the selected card");
        Assert.True(driver.IsTextDisplayed("Change", timeoutSeconds: 5),
            "Quick Add summary row should have a 'Change' affordance");

        EnterPrayerTitle(driver, "UITest QuickAdd Save Prayer");
        driver.TapToolbarItem("Save");
        Thread.Sleep(TestConfig.DelayAfterSave);

        // ConfirmImport Save navigates to Prayer Cards tab.
        Assert.True(driver.IsDisplayed("Cards_List_Cards", timeoutSeconds: 10),
            "After Save, ConfirmImport should navigate to the Prayer Cards tab");
    }

    /// <summary>2.2 (variant): Quick Add cancel — dismisses without saving, returns to Home.</summary>
    [Fact]
    public void QuickAdd_Cancel_DismissesModal()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;

        OpenQuickAdd();

        driver.TapToolbarItem("Cancel");
        Thread.Sleep(TestConfig.DelayAfterDismiss);
        driver.DismissAlertIfPresent();
        Thread.Sleep(TestConfig.DelayAfterDismiss);

        Assert.True(driver.IsDisplayed("Home_Btn_QuickAdd", timeoutSeconds: 10),
            "Should return to Home after Quick Add cancel");
    }

    /// <summary>2.3: Quick Add → Cards tab — prayer appears on Quick Add card.</summary>
    [Fact]
    public void QuickAdd_PrayerAppearsOnCardsTab()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;

        OpenQuickAdd();

        var uniqueTitle = $"CrossTab UITest {DateTime.Now:HHmmss}";
        EnterPrayerTitle(driver, uniqueTitle);

        driver.TapToolbarItem("Save");
        Thread.Sleep(TestConfig.DelayAfterSave);

        // After save, ConfirmImport navigates to Prayer Cards tab.
        Assert.True(driver.IsDisplayed("Cards_List_Cards", timeoutSeconds: 10),
            "Cards tab should be visible after Quick Add save");

        // Use EnsureCardVisible to materialize the prayer row: scrolls the list,
        // expands all collapsed sections, and falls back to Cards_Search if the row
        // is still not visible under virtualization. This is the established robust
        // pattern for finding rows on the Cards tab (AppExtensions.cs:244-283).
        Thread.Sleep(TestConfig.DelayCollectionRender);
        driver.EnsureCardVisible(uniqueTitle);

        Assert.True(driver.IsTextDisplayed(uniqueTitle, timeoutSeconds: 10),
            $"Saved prayer '{uniqueTitle}' should appear on the Cards tab after Quick Add save");
    }

    /// <summary>2.2 (variant): Switch to New Card mode — card title field appears; save creates new card.</summary>
    [Fact]
    public void QuickAdd_SwitchToNewCard_SavesNewCard()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;

        OpenQuickAdd();

        // Switch to New Card mode — the segmented toggle.
        driver.WaitAndTap("ConfirmImport_Seg_NewCard");
        Thread.Sleep(TestConfig.DelayAfterTap);

        // Card Title entry should now be visible.
        Assert.True(driver.IsDisplayed("ConfirmImport_Entry_CardTitle", timeoutSeconds: 5),
            "Card Title entry should be visible after switching to New Card mode");

        var uniqueCard = $"UITest NewCard {DateTime.Now:HHmmss}";
        driver.EnterText("ConfirmImport_Entry_CardTitle", uniqueCard);

        EnterPrayerTitle(driver, $"New Card Prayer {DateTime.Now:HHmmss}");

        driver.TapToolbarItem("Save");
        Thread.Sleep(TestConfig.DelayAfterSave);

        // After save, ConfirmImport navigates to Prayer Cards tab.
        Assert.True(driver.IsDisplayed("Cards_List_Cards", timeoutSeconds: 10),
            "Cards tab should be visible after New Card save");

        Thread.Sleep(TestConfig.DelayCollectionRender);

        Assert.True(driver.IsTextDisplayed(uniqueCard, timeoutSeconds: 10),
            $"New card '{uniqueCard}' should appear on the Prayer Cards tab after save");
    }

    /// <summary>
    /// 2.4: Quick Add screenshot capture — navigates to the Quick Add screen,
    /// captures a diagnostic screenshot, and echoes the saved file path.
    ///
    /// Dark-mode in-session toggle is not supported by the Android test infrastructure
    /// (requires adb + cold-launch — see DarkModeRenderingTests). This test captures
    /// the light screenshot only and records the path for orchestrator collection.
    /// </summary>
    [Fact]
    public void QuickAdd_Capture_Screenshots()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;

        OpenQuickAdd();

        // Ensure the Quick Add UI is fully rendered before capture.
        // Use ConfirmImport_Seg_ExistingCard (a body element) — ToolbarItems
        // are not locatable by AutomationId on Android.
        driver.WaitForElement("ConfirmImport_Seg_ExistingCard", timeoutSeconds: 10);
        Thread.Sleep(TestConfig.DelayModalAnimation);

        // Capture via the existing CaptureDiagnostics helper (saves to
        // %TEMP%\prayerapp-uitest-diag\<timestamp>-<reason>.png).
        var diagInfo = driver.CaptureDiagnostics("QuickAdd_light");

        // CaptureDiagnostics never throws; confirm it succeeded by checking
        // the message doesn't start with the failure prefix.
        Assert.False(diagInfo.Contains("diagnostic capture failed"),
            $"Screenshot capture failed: {diagInfo}");

        // Echo the path so the orchestrator can collect it.
        Console.WriteLine($"[QuickAdd_Capture_Screenshots] {diagInfo}");

        // Cancel cleanly so subsequent tests start from Home.
        driver.TapToolbarItem("Cancel");
        Thread.Sleep(TestConfig.DelayAfterDismiss);
        driver.DismissAlertIfPresent();
        Thread.Sleep(TestConfig.DelayAfterDismiss);
    }
}

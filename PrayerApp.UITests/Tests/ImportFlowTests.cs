using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;
using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// End-to-end coverage for the share-import / context-menu-import pipeline that
/// stops at the ConfirmImport modal. Asserts that parser changes flow through
/// the runtime path (orchestrator → ConfirmImportViewModel → rendered list),
/// not just unit-level behavior.
/// </summary>
[Collection("Appium")]
[Trait("Platform", "iOS")]
[Trait("Section", "Parser-Regression")]
public class ImportFlowTests
{
    private readonly AppiumSetup _setup;
    public ImportFlowTests(AppiumSetup setup) => _setup = setup;

    /// <summary>
    /// Regression for the blank-line block-splitting parser fix
    /// (TextSelectionParser, 2026-05-10). Pre-fix the Jim/Frank/John payload
    /// produced 6 prayers (one per non-empty line). Post-fix it produces 3
    /// prayers (one per blank-line-delimited block, name-as-title +
    /// remaining lines as details).
    ///
    /// Path exercised: AppSettings debug button →
    /// <c>IImportPayloadService.StagePayload</c> → ConfirmImport modal push
    /// → <c>ConfirmImportViewModel</c> consumes raw text and parses →
    /// rendered prayer rows.
    ///
    /// Iterates the staged input list to find the *folded-name* fragments
    /// ("Jim", "Frank", "John") so the test is structurally insensitive to
    /// `BindableLayout` ordering quirks but sensitive to the count.
    /// </summary>
    [SkippableFact]
    public void Import_BlankLineBlocks_StagedPayload_ProducesThreePrayers()
    {
        if (!TestConfig.IsIOS)
            throw new SkipException("iOS-only: AppSettings debug button hosted in MAUI iOS build under test");

        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;

        // 1. Settings → App Settings → scroll to + tap the regression button.
        driver.NavigateToTabRoot("Settings", "Settings_Row_AppSettings", _setup);
        driver.WaitAndTap("Settings_Row_AppSettings");
        driver.WaitForElement("AppSettings_Switch_Notifications", timeoutSeconds: 10);

        driver.ScrollDownTo("AppSettings_Btn_StageParserRegressionPayload", maxScrolls: 8);
        driver.WaitAndTap("AppSettings_Btn_StageParserRegressionPayload");

        // 2. ConfirmImport modal opens. Card-title entry is the modal-open
        // probe (same probe used by ContextMenuImportTests + the Slice 1
        // diagnostic).
        driver.WaitForElement("ConfirmImport_Entry_CardTitle", timeoutSeconds: 10);
        Thread.Sleep(TestConfig.DelayModalAnimation);

        Assert.True(
            driver.IsDisplayed("ConfirmImport_List_Prayers", timeoutSeconds: 5),
            "ConfirmImport modal should render the prayer list when opened via staged payload");

        // 3. Count prayer Entry rows. Each EditablePrayer renders one Entry
        // (Title) + one Editor (Details). Counting Entry rows under the
        // prayers list filters out the CardTitle entry that lives in the
        // collection-and-card section above.
        var prayersList = driver.WaitForElement("ConfirmImport_List_Prayers", timeoutSeconds: 5);

        // XCUIElementTypeTextField is the platform mapping for MAUI Entry on
        // iOS. The CardTitle entry is OUTSIDE the prayers list container,
        // so a descendant XPath rooted at ConfirmImport_List_Prayers gives a
        // clean count of prayer-row title entries.
        var titleEntries = prayersList.FindElements(By.XPath(".//XCUIElementTypeTextField"));

        Assert.Equal(3, titleEntries.Count);

        // 4. Assert the three folded titles appear in the rendered values.
        // iOS exposes the entry's bound Text via the @value attribute on
        // XCUIElementTypeTextField. Order is preserved by BindableLayout
        // against the source ObservableCollection.
        var titles = titleEntries.Select(e => e.GetAttribute("value") ?? string.Empty).ToList();

        Assert.Contains(titles, t => t == "Jim");
        Assert.Contains(titles, t => t == "Frank");
        Assert.Contains(titles, t => t == "John");

        // 5. Spot-check that details bodies contain the expected substrings —
        // proves the block-fold packed lines 2..N into the Details editor
        // rather than leaking them as separate prayers. Editor maps to
        // XCUIElementTypeTextView on iOS.
        var detailEditors = prayersList.FindElements(By.XPath(".//XCUIElementTypeTextView"));
        var details = detailEditors.Select(e => e.GetAttribute("value") ?? string.Empty).ToList();

        Assert.Contains(details, d => d.Contains("Looking for a new job"));
        Assert.Contains(details, d => d.Contains("Wife is due") && d.Contains("third child"));
        Assert.Contains(details, d => d.Contains("Work has been so busy"));

        // 6. Cancel out so the next test doesn't see a half-imported card.
        driver.WaitAndTap("ConfirmImport_Btn_Cancel");
        Thread.Sleep(TestConfig.DelayAfterDismiss);
        driver.DismissAlertIfPresent();
        Thread.Sleep(TestConfig.DelayAfterDismiss);
    }
}

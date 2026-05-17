using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 14 (Android-Specific): Selection-toolbar import via <c>ACTION_PROCESS_TEXT</c>.
/// Slice 2 of the Context Menu Prayer Card feature.
/// </summary>
[Collection("Appium")]
[Trait("Platform", "Android")]
[Trait("Section", "14-Android")]
public class ContextMenuImportTests
{
    private readonly AppiumSetup _setup;
    public ContextMenuImportTests(AppiumSetup setup) => _setup = setup;

    /// <summary>
    /// PROCESS_TEXT intent reaches MainActivity, HandleSelectionImport stages the
    /// payload, and the ConfirmImport modal opens. Cancels out so other tests
    /// don't see a half-imported card. Multi-line parsing has unit-test coverage;
    /// this test covers the platform-layer wireup only.
    /// </summary>
    [SkippableFact]
    public void ContextMenu_ProcessTextIntent_OpensConfirmImportModal()
    {
        if (TestConfig.IsIOS)
            throw new SkipException("Android-only: PROCESS_TEXT is the Android selection-toolbar entry point");

        var driver = _setup.Driver;
        driver.ResetAppUIState(_setup);

        // Retries absorb Appium startActivity flake; app reads CharSequence first,
        // then String extra (am start --es) via ProcessTextIntentReader.
        driver.OpenConfirmImportViaProcessText("Pray for Mom");
        Thread.Sleep(TestConfig.DelayModalAnimation);

        Assert.True(driver.IsDisplayed(
            "ConfirmImport_List_Prayers", timeoutSeconds: 5),
            "ConfirmImport modal should render the prayer list when opened via PROCESS_TEXT");

        driver.WaitAndTap("ConfirmImport_Btn_Cancel");
        Thread.Sleep(TestConfig.DelayAfterDismiss);
    }
}

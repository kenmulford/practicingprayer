using OpenQA.Selenium;
using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// One-off diagnostic for the ?imported=true refresh bug (Slice 1 closeout).
/// Drives Settings → App Settings → Stage sample payload → Save → tap the new
/// "Imported {date}" card. Paired with PerfLog instrumentation; capture
/// `adb logcat | grep PERF:` while this test runs, then correlate.
///
/// DELETE THIS FILE once the failure mode is identified and the fix lands.
/// </summary>
[Collection("Appium")]
[Trait("Diagnostic", "ImportFlow")]
public class _ImportFlowDiagnostic
{
    private readonly AppiumSetup _setup;
    private readonly ITestOutputHelper _output;

    public _ImportFlowDiagnostic(AppiumSetup setup, ITestOutputHelper output)
    {
        _setup = setup;
        _output = output;
    }

    [Fact]
    public void ImportFlow_StagedPayload_NewCardExpands()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;

        // 1. Settings hub → App Settings
        driver.NavigateToTabRoot("Settings", "Settings_Row_AppSettings", _setup);
        driver.WaitAndTap("Settings_Row_AppSettings");
        driver.WaitForElement("AppSettings_Switch_Notifications", timeoutSeconds: 10);

        // 2. Tap the debug-only "Stage sample payload" button (scroll into view first)
        driver.ScrollDownTo("AppSettings_Btn_StageSamplePayload", maxScrolls: 8);
        driver.WaitAndTap("AppSettings_Btn_StageSamplePayload");

        // 3. ConfirmImport modal — wait for the title entry (proves modal is open),
        // scroll Save into view, then tap.
        driver.WaitForElement("ConfirmImport_Entry_CardTitle", timeoutSeconds: 10);
        Thread.Sleep(TestConfig.DelayModalAnimation);
        driver.ScrollDownTo("ConfirmImport_Btn_Save", maxScrolls: 6);
        driver.WaitAndTap("ConfirmImport_Btn_Save");

        // 4. Modal dismisses, navigates to Cards via ?imported=true.
        // New card title is "Imported {MMM d}" e.g., "Imported May 1".
        Thread.Sleep(TestConfig.DelayAfterSave);
        var importedTitle = $"Imported {DateTime.Now:MMM d}";

        // 5. Tap the new card to expand. PerfLog will capture
        // ToggleExpanded.entry, LoadPrayers.entry/.exit, ExpandedSubtree.realized.
        driver.ScrollDownToText(importedTitle, maxScrolls: 4);
        driver.TapByText(importedTitle);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // 6. Probe whether prayer rows rendered (best-effort observation —
        // the PerfLog output is the authoritative artifact).
        var sawMom = TryFindByText(driver, "Pray for Mom");
        var sawDad = TryFindByText(driver, "Pray for Dad");
        var sawSis = TryFindByText(driver, "Pray for Sis");
        _output.WriteLine($"DIAG: prayers visible? Mom={sawMom} Dad={sawDad} Sis={sawSis}");

        // No assertion — test always passes; the PerfLog capture is the artifact.
    }

    private static bool TryFindByText(OpenQA.Selenium.Appium.AppiumDriver driver, string text)
    {
        try
        {
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);
            return driver.FindByText(text, timeoutSeconds: 2) is not null;
        }
        catch (WebDriverException)
        {
            return false;
        }
        finally
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
        }
    }
}

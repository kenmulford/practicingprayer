using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// Regression for <see cref="PrayerApp.Behaviors.KeyboardAvoidanceBehavior"/> on
/// <c>ConfirmImportPage</c> — the only page that attaches the behavior.
/// </summary>
[Collection("Appium")]
[Trait("Platform", "Android")]
[Trait("Section", "KeyboardAvoidance")]
public class ConfirmImportKeyboardAvoidanceTests
{
    private readonly AppiumSetup _setup;

    public ConfirmImportKeyboardAvoidanceTests(AppiumSetup setup) => _setup = setup;

    /// <summary>
    /// PROCESS_TEXT opens ConfirmImport; focusing the Card Title entry should scroll it
    /// above the soft keyboard using KeyboardAvoidanceBehavior (not just adjustResize layout).
    /// </summary>
    [SkippableFact]
    public void ConfirmImport_CardTitleEntry_StaysAboveKeyboard_WhenFocused()
    {
        if (TestConfig.IsIOS)
            throw new SkipException("Android-only: keyboard occlusion uses dumpsys + UiAutomator2 rects");

        var driver = _setup.Driver;
        driver.ResetAppUIState(_setup);

        driver.LaunchProcessTextIntent("Pray for Mom");
        var entry = driver.WaitForElement("ConfirmImport_Entry_CardTitle", timeoutSeconds: 10);
        Thread.Sleep(TestConfig.DelayModalAnimation);

        entry.Click();
        driver.WaitForAndroidKeyboard(timeoutSeconds: 8);

        // KeyboardAvoidanceBehavior yields two frames then ScrollToAsync — allow settle.
        Thread.Sleep(600);

        try
        {
            driver.AssertBottomEdgeAboveKeyboard(entry);
        }
        catch (Xunit.Sdk.XunitException ex)
        {
            throw new Xunit.Sdk.XunitException(ex.Message + driver.CaptureDiagnostics("ConfirmImportKeyboard"));
        }
    }
}

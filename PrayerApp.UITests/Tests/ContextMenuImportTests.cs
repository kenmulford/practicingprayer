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
    /// <remarks>
    /// <para>
    /// <b>Coverage scope:</b> this test uses <c>am start --es</c>, which puts a plain
    /// <c>String</c> extra. It exercises the String boundary of <c>HandleAndroidIntent</c>
    /// only. It does NOT defend the <c>SpannableString</c> boundary — a regression from
    /// <c>GetCharSequenceExtra</c> to <c>GetStringExtra</c> would NOT be caught here
    /// because <c>am start --es</c> never delivers a Spannable.
    /// </para>
    /// <para>
    /// See <see cref="ContextMenu_RichTextSpannablePayload_StagesPlainText"/> for the
    /// Spannable-boundary defense (uses a host-side debug broadcast receiver to inject
    /// a real <c>SpannableString</c> through the production pipeline).
    /// </para>
    /// </remarks>
    [SkippableFact]
    public void ContextMenu_ProcessTextIntent_OpensConfirmImportModal()
    {
        if (TestConfig.IsIOS)
            throw new SkipException("Android-only: PROCESS_TEXT is the Android selection-toolbar entry point");

        // Force-stop guarantees a fresh MainActivity OnCreate path. Without this, a prior
        // test can leave MainActivity backgrounded, and LaunchMode.SingleTop reuse routes
        // the new intent through OnNewIntent on the existing instance — a different timing
        // surface than what manual emulator smoke / production hits. Eliminates a class
        // of flakes observed on this test.
        _setup.Driver.ForceStopApp();

        // LaunchProcessTextIntent foregrounds MainActivity (via `am start`) and runs
        // its own onboarding-dismissal step after foregrounding — the cached
        // OnboardingHandled flag is invalid after force-stop, so a direct test-level
        // DismissOnboardingIfPresent call here would short-circuit against a dead app
        // and leave the post-launch popup undismissed.
        _setup.Driver.LaunchProcessTextIntent(_setup, "Pray for Mom");

        // Modal-open detector: the Card Title entry — same probe used by
        // _ImportFlowDiagnostic to confirm the modal actually rendered. WaitForElement
        // polls until found or timeout, replacing prior bare Thread.Sleep before the
        // probe (was a source of flakes when modal animation ran long).
        Assert.NotNull(_setup.Driver.WaitForElement(
            "ConfirmImport_Entry_CardTitle", timeoutSeconds: 10));

        Assert.True(_setup.Driver.IsDisplayed(
            "ConfirmImport_List_Prayers", timeoutSeconds: 5),
            "ConfirmImport modal should render the prayer list when opened via PROCESS_TEXT");

        _setup.Driver.WaitAndTap("ConfirmImport_Btn_Cancel");
        Thread.Sleep(TestConfig.DelayAfterDismiss);
    }

    /// <summary>
    /// Defends the <c>SpannableString</c> boundary of the PROCESS_TEXT pipeline.
    /// Invokes the debug-build-only host-side <c>DebugProcessTextShim</c> broadcast
    /// receiver, which constructs a real <c>SpannableString</c> (with a markup span
    /// attached, mirroring Chrome / Gmail rich-text payloads) and re-dispatches via
    /// the real <c>ACTION_PROCESS_TEXT</c> pipeline.
    /// </summary>
    /// <remarks>
    /// Goes RED if <c>MauiProgram.HandleAndroidIntent</c> regresses from
    /// <c>GetCharSequenceExtra</c> to <c>GetStringExtra</c>: <c>GetStringExtra</c>
    /// returns <c>null</c> for a <c>SpannableString</c> extra, so the modal would
    /// never open and <c>WaitForElement</c> would time out.
    /// </remarks>
    [SkippableFact]
    public void ContextMenu_RichTextSpannablePayload_StagesPlainText()
    {
        if (TestConfig.IsIOS)
            throw new SkipException("Android-only: PROCESS_TEXT is the Android selection-toolbar entry point");

        _setup.Driver.ForceStopApp();

        // LaunchProcessTextIntentSpannable foregrounds MainActivity (via `am start`)
        // and runs its own onboarding-dismissal step between the foreground call and
        // the broadcast — the cached OnboardingHandled flag is invalid after force-stop.
        _setup.Driver.LaunchProcessTextIntentSpannable(_setup, "Pray for Mom");

        var entry = _setup.Driver.WaitForElement(
            "ConfirmImport_Entry_CardTitle", timeoutSeconds: 10);
        Assert.NotNull(entry);

        Assert.True(_setup.Driver.IsDisplayed(
            "ConfirmImport_List_Prayers", timeoutSeconds: 5),
            "ConfirmImport modal should render the prayer list when opened via PROCESS_TEXT with a SpannableString payload");

        _setup.Driver.WaitAndTap("ConfirmImport_Btn_Cancel");
        Thread.Sleep(TestConfig.DelayAfterDismiss);
    }
}

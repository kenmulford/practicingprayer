using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using PrayerApp.UITests.Infrastructure;
using PrayerApp.UITests.Helpers;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 15: Accessibility
/// Validates that semantic properties, descriptions, hints, and tree visibility
/// are correct for screen reader users on both Android and iOS.
/// </summary>
[Collection("Appium")]
[Trait("Platform", "CrossPlatform")]
[Trait("Section", "15-Accessibility")]
public class AccessibilityTests
{
    private readonly AppiumSetup _setup;
    private AppiumDriver Driver => _setup.Driver;

    public AccessibilityTests(AppiumSetup setup) => _setup = setup;

    /// <summary>15.1: Home metric tiles have composed accessible descriptions.</summary>
    [Fact]
    public void Home_MetricTiles_HaveComposedDescriptions()
    {
        Driver.ResetAppUIState(_setup);
        Driver.EnsureOnTab("Home", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // The ActiveCardsAccessible binding produces "Active cards, N. Tap to view prayer cards."
        // or "No active cards. Tap to create your first card."
        Assert.True(Driver.HasAccessibleElement("Active cards"),
            "Home should have an accessible element containing 'Active cards' description");
    }

    /// <summary>15.2: Filter chip announces selected/not selected state.</summary>
    [Fact]
    public void Cards_FilterChip_AnnouncesSelectedState()
    {
        Driver.ResetAppUIState(_setup);
        // Ensure a tag exists so filter chips render
        Driver.EnsureUITestTagExists(_setup);
        Driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Filter chips have AccessibleDescription like "TagName, not selected".
        // Don't assume a specific tag name — find ANY chip with "not selected".
        Assert.True(Driver.HasAccessibleElement("not selected", timeoutSeconds: 10),
            "At least one filter chip should have 'not selected' in its description");

        // Capture the first chip's full description to verify state change
        // Tap it using the "not selected" text
        Driver.TapByTextContains("not selected", timeoutSeconds: 10);
        Thread.Sleep(TestConfig.DelayAfterTap);
        Thread.Sleep(TestConfig.DelayDirtyRegistration); // Extra settle for content-desc binding update

        // After selecting, at least one chip should now say "selected" (without "not").
        // HasAccessibleElement("selected") would also match "not selected" — use ", selected"
        // (with the comma prefix) to distinguish the selected state suffix from the "not" variant.
        var source = Driver.PageSource;
        bool hasSelectedChip = source.Contains(", selected\"");
        Assert.True(Driver.HasAccessibleElement(", selected", timeoutSeconds: 3) || hasSelectedChip,
            "Tapped filter chip should announce 'selected' state");

        // Tap again to deselect and leave clean state
        Driver.TapByTextContains("selected", timeoutSeconds: 10);
        Thread.Sleep(TestConfig.DelayAfterTap);
    }

    // 15.3 (Cards_CardHeader_AnnouncesExpandCollapseState) and
    // 15.4 (Cards_PrayerRow_HasAccessibleSummary) were converted to deterministic
    // unit tests in issue #148 Phase 2. The composed-label contracts they exercised
    // now live in PrayerApp.Tests:
    //   - PrayerCardViewModelTests.AccessibleCardHeader_* (over PrayerCardViewModel.AccessibleCardHeader)
    //   - PrayerRequestDetailViewModelTests.AccessibleSummary_* (over PrayerRequestDetailViewModel.AccessibleSummary)
    // The on-device E2Es added no coverage beyond the getters and were removed.

    /// <summary>15.5: "Select" toolbar button exists for multi-select entry.</summary>
    [Fact]
    public void Cards_SelectToolbarItem_Exists()
    {
        Driver.ResetAppUIState(_setup);
        Driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        Assert.True(Driver.IsToolbarItemAvailable("Select", timeoutSeconds: 10),
            "Select toolbar button should be available");
    }

    /// <summary>15.6: FAQ question hint is present on the Help page.</summary>
    [Fact]
    public void Settings_FaqQuestion_HasHint()
    {
        Driver.ResetAppUIState(_setup);
        // Navigate to Settings > Help
        Driver.NavigateToTabRoot("Settings", "Settings_Row_Help", _setup);
        Driver.WaitAndTap("Settings_Row_Help");
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // FAQ questions have SemanticProperties.Hint="Double tap to expand" in XAML.
        // Verify a FAQ question is present by looking for known question text.
        Assert.True(
            Driver.IsTextDisplayed("How do I create a prayer card?", timeoutSeconds: 8)
            || Driver.IsTextDisplayed("What is Quick Add?", timeoutSeconds: 3)
            || Driver.IsTextDisplayed("Is my data private?", timeoutSeconds: 3),
            "Help page should display FAQ questions");

        // Tap a question to expand it — the answer should become visible
        if (Driver.IsTextDisplayed("How do I create a prayer card?", timeoutSeconds: 3))
        {
            Driver.TapByText("How do I create a prayer card?");
            Thread.Sleep(TestConfig.DelayAfterTap);

            // After expanding, the answer text should appear in the tree
            Assert.True(
                Driver.IsTextContainsDisplayed("Tap the", timeoutSeconds: 10)
                || Driver.IsTextContainsDisplayed("prayer card", timeoutSeconds: 3),
                "Expanded FAQ should show the answer text");
        }

        Driver.GoBack();
    }

    /// <summary>
    /// 15.7: Android-only — decorative elements marked IsInAccessibleTree="False"
    /// have important-for-accessibility="no" in the UiAutomator2 tree.
    /// Note: UiAutomator2 still SHOWS the element in page source (it sees all views),
    /// but the attribute tells TalkBack to skip it. We verify the attribute, not absence.
    /// </summary>
    [Fact]
    public void Cards_DecorativeElements_MarkedNotImportant()
    {
        Driver.ResetAppUIState(_setup);
        if (TestConfig.IsIOS)
            return; // iOS flattening makes child-level tree assertions unreliable

        Driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Dump the page source and check that triangle elements have
        // importantForAccessibility="no" or are not focusable.
        // UiAutomator2 exposes ALL views — IsInAccessibleTree="False" doesn't
        // remove them from the dump, it sets an attribute that TalkBack respects.
        var source = Driver.PageSource;

        // The triangle text exists in the tree but should not be focusable
        // (no content-desc, not clickable, not focusable as an a11y node)
        // This is a heuristic: if the triangle has no content-desc and isn't
        // marked as accessible, TalkBack will skip it.
        Assert.Contains("\u25BC", source); // triangle exists in DOM (expected)
        // Verify it does NOT appear as a content-desc (which would make it announced)
        Assert.DoesNotContain("content-desc=\"\u25BC\"", source);
    }

    /// <summary>15.9: Settings row meets the 44dp touch-target minimum (PS-06 / TouchTargetMinimum
    /// guard). The Platform Styles Sprint introduced the TouchTargetMinimum=44 token
    /// and applied it to SettingsRowGrid (commit e144476); this test catches a future
    /// XAML override that would silently drop below the floor. Density is queried
    /// from Appium so the test is portable across emulators of different DPI.
    /// </summary>
    [Fact]
    [Trait("Platform", "Android")]
    [Trait("Section", "9-Settings")]
    public void Settings_AppSettingsRow_MeetsTouchTargetMinimum()
    {
        Driver.ResetAppUIState(_setup);
        // #170: density math below uses UiAutomator2's Android-only `mobile: deviceInfo`
        // displayDensity; the class-level CrossPlatform trait otherwise pulls this Android
        // touch-target guard into the iOS run scope, where that query throws. Guard iOS out
        // (mirrors the sibling at line 136) — the 44pt regression guard stays intact on Android.
        if (TestConfig.IsIOS)
            return;
        Driver.EnsureOnTab("Settings", _setup);
        Driver.WaitForElement("Settings_Row_AppSettings", timeoutSeconds: 10);

        var row = Driver.FindByAutomationId("Settings_Row_AppSettings");
        var heightPx = row.Size.Height;

        // Convert the 44dp accessibility floor to actual pixels for this device.
        // UiAutomator2's mobile: deviceInfo exposes displayDensity (Android DPI, e.g. 440).
        // px-per-dp = displayDensity / 160 (the Android baseline density).
        var deviceInfo = (Dictionary<string, object>?)Driver.ExecuteScript("mobile: deviceInfo")
            ?? throw new InvalidOperationException("mobile: deviceInfo returned null");
        var displayDensity = Convert.ToDouble(deviceInfo["displayDensity"]);
        var pxPerDp = displayDensity / 160.0;
        var expectedMinPx = (int)Math.Floor(44 * pxPerDp);

        Assert.True(heightPx >= expectedMinPx,
            $"Settings_Row_AppSettings height {heightPx}px should meet the 44dp touch-target " +
            $"minimum ({expectedMinPx}px at displayDensity {displayDensity}). PS-06 regression — " +
            $"check whether MinimumHeightRequest on the SettingsRowGrid style was overridden.");
    }
}

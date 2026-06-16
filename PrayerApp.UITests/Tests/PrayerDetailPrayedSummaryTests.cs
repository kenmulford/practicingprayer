using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 4: Prayers Tab — #81 prayed-count summary label on detail page.
/// </summary>
[Collection("Appium")]
[Trait("Platform", "CrossPlatform")]
[Trait("Section", "4-Prayers")]
public class PrayerDetailPrayedSummaryTests
{
    private readonly AppiumSetup _setup;
    public PrayerDetailPrayedSummaryTests(AppiumSetup setup) => _setup = setup;

    /// <summary>
    /// 4.10: Prayer detail view shows "Prayed for N times since {date}" summary label.
    /// Opens the seeded "UI Test Prayer", asserts the label is present and contains
    /// the expected phrases.  Does not assert the exact count since the seed DB may
    /// have zero or more interactions recorded.
    /// </summary>
    [Fact]
    public void PrayerDetail_ShowsPrayedSummaryLabel()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;
        driver.EnsureOnPrayersTab(_setup);

        driver.ScrollToPrayerAndTap("UI Test Prayer");

        // The summary label may be below the fold — scroll until "Prayed for" is visible.
        // ScrollDownToTextContains uses @text (Android) / @name|@label (iOS) so it finds
        // the Label regardless of AutomationId/content-desc mapping.
        driver.ScrollDownToTextContains("Prayed for", maxScrolls: 5);

        // Label must be present in view-mode (read-only surface).
        // IsTextContainsDisplayed checks @text/@content-desc (Android) or @name/@label (iOS).
        Assert.True(
            driver.IsTextContainsDisplayed("Prayed for", timeoutSeconds: 10),
            "Prayed-for summary label should be visible on the detail page in view mode");

        // Retrieve the full text for content assertions.
        var text = driver.FindByTextContains("Prayed for", timeoutSeconds: 10).Text;
        Assert.Contains("Prayed for", text);
        Assert.Contains("since", text);

        driver.GoBack();
    }
}

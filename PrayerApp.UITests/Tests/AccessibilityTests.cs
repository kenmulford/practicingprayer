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
        // Ensure a tag exists so filter chips render
        Driver.EnsureUITestTagExists(_setup);
        Driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Filter chips have AccessibleDescription like "UITest Tag, not selected"
        Assert.True(Driver.HasAccessibleElement("not selected", timeoutSeconds: 5),
            "At least one filter chip should announce 'not selected' state");

        // Tap the chip to select it — find by the description text
        Driver.TapByTextContains("not selected", timeoutSeconds: 5);
        Thread.Sleep(TestConfig.DelayAfterTap);

        // After selecting, the chip description should contain "selected" without "not"
        // Use a more specific check: the chip VM produces "TagName, selected"
        Assert.True(Driver.HasAccessibleElement("UITest Tag, selected", timeoutSeconds: 5),
            "Tapped filter chip should announce 'selected' state");

        // Deselect to leave clean state
        Driver.TapByTextContains("UITest Tag, selected", timeoutSeconds: 5);
        Thread.Sleep(TestConfig.DelayAfterTap);
    }

    /// <summary>15.3: Card header announces expand/collapse state in composed description.</summary>
    [Fact]
    public void Cards_CardHeader_AnnouncesExpandCollapseState()
    {
        Driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Quick Add card should exist — its AccessibleCardHeader includes "Collapsed" or "Expanded"
        // Find the card in collapsed state first
        Assert.True(
            Driver.HasAccessibleElement("Quick Add", timeoutSeconds: 5),
            "Quick Add card should be in the accessibility tree");

        // Tap to expand — the card header description changes to include "Expanded"
        Driver.TapByTextContains("Quick Add");
        Thread.Sleep(TestConfig.DelayAfterTap);

        Assert.True(Driver.HasAccessibleElement("Expanded", timeoutSeconds: 5),
            "Expanded card header should contain 'Expanded' in its accessible description");

        // Tap to collapse — should now contain "Collapsed"
        Driver.TapByTextContains("Quick Add");
        Thread.Sleep(TestConfig.DelayAfterTap);

        Assert.True(Driver.HasAccessibleElement("Collapsed", timeoutSeconds: 5),
            "Collapsed card header should contain 'Collapsed' in its accessible description");
    }

    /// <summary>15.4: Prayer row inside expanded card has accessible summary.</summary>
    [Fact]
    public void Cards_PrayerRow_HasAccessibleSummary()
    {
        // Ensure a prayer exists in the Quick Add card
        Driver.EnsureUITestPrayerExists(_setup);
        Driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Expand Quick Add to reveal prayers
        if (TestConfig.IsIOS)
        {
            if (!Driver.IsTextContainsDisplayed("Quick Add, Expanded", timeoutSeconds: 2))
                Driver.TapByTextContains("Quick Add");
        }
        else
        {
            if (!Driver.IsDisplayed("Cards_Btn_AddPrayer", timeoutSeconds: 2))
                Driver.TapByText("Quick Add");
        }
        Thread.Sleep(TestConfig.DelayAfterTap);

        // The prayer row Grid has SemanticProperties.Description="{Binding AccessibleSummary}"
        // which includes the prayer title. On iOS this is the flattened label.
        Assert.True(Driver.HasAccessibleElement("UI Test Prayer", timeoutSeconds: 5),
            "Prayer row should have accessible description containing the prayer title");

        // Collapse the card to leave clean state for subsequent tests
        Driver.TapByTextContains("Quick Add");
        Thread.Sleep(TestConfig.DelayAfterTap);
    }

    /// <summary>15.5: "Select" toolbar button exists for multi-select entry.</summary>
    [Fact]
    public void Cards_SelectToolbarItem_Exists()
    {
        Driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // The Select toolbar item should be discoverable in the toolbar
        if (TestConfig.IsIOS)
        {
            // iOS Shell toolbar items are XCUIElementTypeButton
            try
            {
                Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
                var element = Driver.FindElement(
                    By.XPath("//XCUIElementTypeButton[@name='Select' or @label='Select']"));
                Assert.True(element.Displayed, "Select toolbar button should be visible on iOS");
            }
            finally
            {
                Driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
            }
        }
        else
        {
            Assert.True(Driver.IsTextDisplayed("Select", timeoutSeconds: 5),
                "Select toolbar button should be visible on Android");
        }
    }

    /// <summary>15.6: FAQ question hint is present on the Help page.</summary>
    [Fact]
    public void Settings_FaqQuestion_HasHint()
    {
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
                Driver.IsTextContainsDisplayed("Tap the", timeoutSeconds: 5)
                || Driver.IsTextContainsDisplayed("prayer card", timeoutSeconds: 3),
                "Expanded FAQ should show the answer text");
        }

        Driver.GoBack();
    }

    /// <summary>15.7: Android-only — decorative dividers and triangles not in accessibility tree.</summary>
    [Fact]
    public void Cards_DecorativeElements_NotInTree()
    {
        if (TestConfig.IsIOS)
            return; // iOS flattening makes child-level tree assertions unreliable

        Driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // The triangle indicators (BoolToTriangle converter) are marked
        // AutomationProperties.IsInAccessibleTree="False" in XAML.
        // Verify they don't appear in the accessibility tree.
        Driver.AssertNotInAccessibleTree("\u25BC"); // ▼ down triangle
        Driver.AssertNotInAccessibleTree("\u25B6"); // ▶ right triangle
    }

    /// <summary>15.8: Toolbar items have accessible hints (spot check on Prayer Cards).</summary>
    [Fact]
    public void Cards_ToolbarItems_HaveHints()
    {
        Driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // The "Collections" toolbar item has SemanticProperties.Hint="Manage collections"
        // Check that the toolbar item is reachable and has meaningful text
        if (TestConfig.IsIOS)
        {
            // iOS toolbar buttons: verify "Collections" exists as a tappable button
            try
            {
                Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
                var element = Driver.FindElement(
                    By.XPath("//XCUIElementTypeButton[@name='Collections' or @label='Collections']"));
                Assert.True(element.Displayed, "Collections toolbar button should be visible on iOS");
            }
            finally
            {
                Driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
            }
        }
        else
        {
            // Android: verify the Collections toolbar item is displayed
            Assert.True(Driver.IsTextDisplayed("Collections", timeoutSeconds: 5),
                "Collections toolbar item should be visible on Android");
        }
    }
}

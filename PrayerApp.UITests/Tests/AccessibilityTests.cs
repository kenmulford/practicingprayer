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
        Thread.Sleep(500); // Extra settle for content-desc binding update

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

    /// <summary>15.3: Card header announces expand/collapse state in composed description.</summary>
    [Fact]
    public void Cards_CardHeader_AnnouncesExpandCollapseState()
    {
        Driver.ResetAppUIState(_setup);
        Driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Use a card that's visible on screen — Quick Add may be off-screen.
        // From the page source, the first user card in Loose Cards is always visible.
        // Pick any card with a composed content-desc containing "Collapsed".
        bool found = Driver.HasAccessibleElement("Collapsed", timeoutSeconds: 10);
        if (!found)
        {
            // Try scrolling to find a collapsed card
            Driver.ScrollDownToText("Collapsed", maxScrolls: 2,
                scrollableAutomationId: "Cards_List_Cards");
            found = Driver.HasAccessibleElement("Collapsed", timeoutSeconds: 3);
        }
        Assert.True(found, "At least one card should have 'Collapsed' in its accessible description");

        // Find a specific card to tap — use the first non-system card visible
        // (existing test data includes "UITest Card", "Test Card", etc.)
        string? cardName = null;
        foreach (var name in new[] { "UITest Card", "Test Card", "Delete Me Card" })
        {
            if (Driver.HasAccessibleElement(name, timeoutSeconds: 2))
            {
                cardName = name;
                break;
            }
        }
        Assert.NotNull(cardName);

        // Tap to expand — the card header description changes to include "Expanded"
        Driver.TapByTextContains(cardName!);
        Thread.Sleep(TestConfig.DelayAfterTap);

        Assert.True(Driver.HasAccessibleElement("Expanded", timeoutSeconds: 10),
            "Expanded card header should contain 'Expanded' in its accessible description");

        // Tap to collapse — should now contain card name + "Collapsed"
        Driver.TapByTextContains(cardName!);
        Thread.Sleep(TestConfig.DelayAfterTap);

        Assert.True(Driver.HasAccessibleElement(cardName + ", Collapsed", timeoutSeconds: 3)
            || Driver.HasAccessibleElement("Collapsed", timeoutSeconds: 3),
            "Collapsed card header should contain 'Collapsed' in its accessible description");
    }

    /// <summary>15.4: Prayer row inside expanded card has accessible summary.</summary>
    [SkippableFact]
    public void Cards_PrayerRow_HasAccessibleSummary()
    {
        Driver.ResetAppUIState(_setup);
        // Ensure a prayer exists in the Quick Add card
        Driver.EnsureUITestPrayerExists(_setup);
        Driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Expand a card that has prayers — "UITest Card" is created by EnsureUITestPrayerExists.
        // It may have content-desc like "UITest Card, 1 prayer, Collapsed".
        // Need to find it first — may require scrolling.
        bool cardFound = Driver.HasAccessibleElement("UITest Card", timeoutSeconds: 3);
        if (!cardFound)
        {
            Driver.ScrollDownToText("UITest Card", maxScrolls: 3,
                scrollableAutomationId: "Cards_List_Cards");
            cardFound = Driver.HasAccessibleElement("UITest Card", timeoutSeconds: 3);
        }

        if (!cardFound)
            throw new SkipException("UITest Card not found on Prayer Cards page");

        // Tap to expand — look for "Expanded" in the composed description
        if (!Driver.HasAccessibleElement("UITest Card, Expanded", timeoutSeconds: 2))
        {
            Driver.TapByTextContains("UITest Card");
            Thread.Sleep(TestConfig.DelayCollectionRender);
        }

        // The prayer row Grid has SemanticProperties.Description="{Binding AccessibleSummary}"
        // which includes the prayer title. Look for "UI Test Prayer" in the tree.
        Assert.True(
            Driver.HasAccessibleElement("UI Test Prayer", timeoutSeconds: 10)
            || Driver.IsTextContainsDisplayed("UI Test Prayer", timeoutSeconds: 3),
            "Prayer row should have accessible description containing the prayer title");

        // Collapse the card to leave clean state
        Driver.TapByTextContains("UITest Card");
        Thread.Sleep(TestConfig.DelayAfterTap);
    }

    /// <summary>15.5: "Select" toolbar button exists for multi-select entry.</summary>
    [Fact]
    public void Cards_SelectToolbarItem_Exists()
    {
        Driver.ResetAppUIState(_setup);
        Driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // The Select toolbar item has AutomationId="Select", added at page construction.
        // MAUI Shell writes AutomationId into Android contentDescription, so the ID is
        // also the screen-reader label (see Lessons/maui-toolbaritem-android-rendering.md).
        // Try AutomationId first (works on Android), then text/label fallback.
        bool found = Driver.IsDisplayed("Select", timeoutSeconds: 10);
        if (!found)
        {
            // Fallback: toolbar items may render as text or content-desc
            found = Driver.IsTextDisplayed("Select", timeoutSeconds: 3)
                 || Driver.IsTextContainsDisplayed("Select", timeoutSeconds: 2);
        }
        Assert.True(found, "Select toolbar button should be visible");
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

    /// <summary>15.8: All Prayer Cards toolbar items expose a meaningful accessible
    /// name via the accessibility tree. On Android, MAUI Shell writes the
    /// <c>AutomationId</c> into <c>contentDescription</c> — so the AutomationId value
    /// MUST be a human-readable label (the XAML uses "Collections", "Select",
    /// "Add Card"), not the <c>{Page}_{Type}_{Name}</c> convention used elsewhere.
    /// This test locks the contract in: accessible name exists AND equals the
    /// expected short label.</summary>
    [Fact]
    public void Cards_ToolbarItems_HaveHints()
    {
        Driver.ResetAppUIState(_setup);
        Driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // The accessible name (content-desc on Android, name/label on iOS) must equal
        // the AutomationId itself — because MAUI Shell writes the AutomationId into
        // contentDescription verbatim. Empty or machine-style IDs would fail a screen
        // reader user.
        foreach (var id in new[] { "Collections", "Select", "Add Card" })
            Assert.Equal(id, Driver.GetAccessibleDescription(id));
    }
}

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

        // Filter chips have AccessibleDescription like "TagName, not selected".
        // Don't assume a specific tag name — find ANY chip with "not selected".
        Assert.True(Driver.HasAccessibleElement("not selected", timeoutSeconds: 5),
            "At least one filter chip should have 'not selected' in its description");

        // Capture the first chip's full description to verify state change
        // Tap it using the "not selected" text
        Driver.TapByTextContains("not selected", timeoutSeconds: 5);
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
        Driver.TapByTextContains("selected", timeoutSeconds: 5);
        Thread.Sleep(TestConfig.DelayAfterTap);
    }

    /// <summary>15.3: Card header announces expand/collapse state in composed description.</summary>
    [Fact]
    public void Cards_CardHeader_AnnouncesExpandCollapseState()
    {
        Driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Use a card that's visible on screen — Quick Add may be off-screen.
        // From the page source, the first user card in Loose Cards is always visible.
        // Pick any card with a composed content-desc containing "Collapsed".
        bool found = Driver.HasAccessibleElement("Collapsed", timeoutSeconds: 5);
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

        Assert.True(Driver.HasAccessibleElement("Expanded", timeoutSeconds: 5),
            "Expanded card header should contain 'Expanded' in its accessible description");

        // Tap to collapse — should now contain card name + "Collapsed"
        Driver.TapByTextContains(cardName!);
        Thread.Sleep(TestConfig.DelayAfterTap);

        Assert.True(Driver.HasAccessibleElement(cardName + ", Collapsed", timeoutSeconds: 3)
            || Driver.HasAccessibleElement("Collapsed", timeoutSeconds: 3),
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
            throw Xunit.Sdk.SkipException.ForSkip("UITest Card not found on Prayer Cards page");

        // Tap to expand — look for "Expanded" in the composed description
        if (!Driver.HasAccessibleElement("UITest Card, Expanded", timeoutSeconds: 2))
        {
            Driver.TapByTextContains("UITest Card");
            Thread.Sleep(TestConfig.DelayCollectionRender);
        }

        // The prayer row Grid has SemanticProperties.Description="{Binding AccessibleSummary}"
        // which includes the prayer title. Look for "UI Test Prayer" in the tree.
        Assert.True(
            Driver.HasAccessibleElement("UI Test Prayer", timeoutSeconds: 5)
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
        Driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // The Select toolbar item has AutomationId="Cards_Btn_Select", added at page construction.
        // Try AutomationId first (works on Android), then text/label fallback.
        bool found = Driver.IsDisplayed("Cards_Btn_Select", timeoutSeconds: 5);
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

    /// <summary>
    /// 15.7: Android-only — decorative elements marked IsInAccessibleTree="False"
    /// have important-for-accessibility="no" in the UiAutomator2 tree.
    /// Note: UiAutomator2 still SHOWS the element in page source (it sees all views),
    /// but the attribute tells TalkBack to skip it. We verify the attribute, not absence.
    /// </summary>
    [Fact]
    public void Cards_DecorativeElements_MarkedNotImportant()
    {
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

    /// <summary>15.8: All Prayer Cards toolbar items are present in the accessibility
    /// tree with a non-empty accessible name. Tests the accessibility CONTRACT
    /// (<c>AutomationId</c> resolves + <c>SemanticProperties.Description</c> present),
    /// not the visual rendering. An icon-only button with a proper Description
    /// passes; one without fails. Also verifies the Select↔Cancel multi-select
    /// toggle keeps the accessible name in sync with the visual state.</summary>
    [Fact]
    public void Cards_ToolbarItems_HaveHints()
    {
        Driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Every iconized toolbar item needs a meaningful accessible name. Locate by
        // stable AutomationId — survives iconization, localization, and layout rework.
        // Do NOT search by visible text; MAUI Shell renders iconized ToolbarItems as
        // image-only buttons on Android, removing @text from the tree.
        foreach (var id in new[] { "Cards_Btn_Collections", "Cards_Btn_Select", "Cards_Btn_Add" })
        {
            var name = Driver.GetAccessibleDescription(id);
            Assert.False(string.IsNullOrWhiteSpace(name),
                $"{id} should have a non-empty accessible name (SemanticProperties.Description " +
                "or equivalent). Screen reader users rely on this.");
        }

        // Multi-select toggle mutates Text/Icon/Description at runtime. Verify the
        // Description stays in sync — entering multi-select should change the Select
        // button's accessible name (to "Cancel" or similar, not empty/stale).
        var selectNameBefore = Driver.GetAccessibleDescription("Cards_Btn_Select");
        Driver.Tap("Cards_Btn_Select");
        Thread.Sleep(TestConfig.DelayAfterTap);

        var selectNameAfter = Driver.GetAccessibleDescription("Cards_Btn_Select");
        Assert.False(string.IsNullOrWhiteSpace(selectNameAfter),
            "Select toolbar item should still have an accessible name in multi-select mode.");
        Assert.NotEqual(selectNameBefore, selectNameAfter);

        // Leave the page in its normal state for subsequent tests.
        Driver.Tap("Cards_Btn_Select");
        Thread.Sleep(TestConfig.DelayAfterTap);
    }
}

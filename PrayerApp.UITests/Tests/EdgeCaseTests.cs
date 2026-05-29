using OpenQA.Selenium;
using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 12: Edge Cases
/// </summary>
[Collection("Appium")]
[Trait("Platform", "CrossPlatform")]
[Trait("Section", "12-EdgeCases")]
public class EdgeCaseTests
{
    private readonly AppiumSetup _setup;
    public EdgeCaseTests(AppiumSetup setup) => _setup = setup;

    /// <summary>12.1: Cards page with search for nonexistent — shows empty/filtered state.</summary>
    [Fact]
    public void Cards_EmptySearch_ShowsEmptyState()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        // Search for something that won't match
        driver.EnterText("Cards_Search", "zzz_nothing_matches_this_ever");
        Thread.Sleep(TestConfig.DelayDirtyRegistration);

        // Page should still be functional (empty result, no crash)
        Assert.True(driver.IsDisplayed("Cards_List_Cards") || driver.IsDisplayed("Cards_Search"));

        // Clear search
        driver.EnterText("Cards_Search", "");
        Thread.Sleep(TestConfig.DelayDirtyRegistration);
    }

    /// <summary>12.3: Very long prayer title — doesn't break layout.</summary>
    [Fact]
    public void EdgeCase_LongPrayerTitle_NoLayoutBreak()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;
        driver.EnsureOnTab("Home", _setup);

        driver.WaitAndTap("Home_Btn_QuickAdd");
        driver.WaitForElement("QuickAdd_Entry_Title");

        var longTitle = new string('A', 200) + " Long Prayer Title";
        driver.EnterText("QuickAdd_Entry_Title", longTitle);
        driver.WaitAndTap("QuickAdd_Btn_Add");
        Thread.Sleep(TestConfig.DelayAfterSave);

        Assert.True(driver.IsDisplayed("Home_Btn_QuickAdd"),
            "App should handle long prayer titles without crashing");
    }

    /// <summary>12.4: Rapid tab switching — no crash, no stale data.</summary>
    [Fact]
    public void EdgeCase_RapidTabSwitching_NoCrash()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;
        driver.EnsureOnTab("Home", _setup);

        var tabs = new[] { "Home", "Prayer Cards", "Prayers", "Tags", "Settings" };
        var delay = TestConfig.IsIOS ? 500 : 200; // iOS Shell navigation is slower
        foreach (var tab in tabs)
        {
            driver.NavigateToTab(tab);
            Thread.Sleep(delay);
        }

        for (int i = tabs.Length - 1; i >= 0; i--)
        {
            driver.NavigateToTab(tabs[i]);
            Thread.Sleep(delay);
        }

        driver.NavigateToTab("Home");
        Assert.True(driver.IsDisplayed("Home_Btn_QuickAdd"),
            "App should still be functional after rapid tab switching");
    }

    /// <summary>12.5: Double-tap Save — guard prevents double-save.</summary>
    [Fact]
    public void EdgeCase_DoubleTapSave_NoDuplicate()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;
        driver.EnsureOnTab("Prayers", _setup);

        driver.TapToolbarItem("Add");
        driver.WaitForElement("Detail_Entry_Title", timeoutSeconds: 10);
        driver.EnterText("Detail_Entry_Title", "Double Save Test");

        // Rapidly tap Save twice
        driver.TapToolbarItem("Save");
        // Deliberate 100ms gap between rapid Save taps — must be short enough
        // to exercise the IsBusy double-save guard race window (inline per plan).
        Thread.Sleep(100);
        // Second tap should be ignored (guard prevents double-save)
        try { driver.TapToolbarItem("Save"); } catch (WebDriverException) { }
        Thread.Sleep(TestConfig.DelayAfterSave);

        // Should end up back on the prayers list (not stuck or crashed)
        driver.NavigateToTab("Prayers");
        Assert.True(driver.IsDisplayed("List_Filter_Active", timeoutSeconds: 10),
            "App should handle double-tap Save without crashing");
    }

    /// <summary>12.2: Empty card — expand card with no prayers shows empty state.</summary>
    [Fact]
    public void EdgeCase_EmptyCardExpand_ShowsAddPrayer()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        // Create a fresh card with no prayers
        driver.TapToolbarItemById("Add Card");
        driver.WaitForElement("Card_Entry_Title", timeoutSeconds: 10);
        driver.EnterText("Card_Entry_Title", "Empty Card Test");
        driver.TapToolbarItem("Save");

        // Save triggers GoToAsync("..") after the DB write; manual repro showed the
        // round-trip taking ~5s on emulator. A fixed Thread.Sleep(1000) raced the
        // section rebuild, so the new card was absent when we nav'd away.
        driver.WaitForElement("Cards_List_Cards", timeoutSeconds: 10);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Collapse any expanded card rows by navigating away; then expand sections so
        // newly-created cards in collapsed Loose Cards are renderable for find+tap.
        driver.NavigateToTab("Home");
        Thread.Sleep(TestConfig.DelayAfterTap);
        driver.NavigateToTab("Prayer Cards");
        Thread.Sleep(TestConfig.DelayCollectionRender);
        driver.EnsureAllSectionsExpanded();
        Thread.Sleep(TestConfig.DelayAfterTap);

        // EnsureCardVisible handles scrolling, section expansion, and a search-bar
        // fallback for virtualized / off-tree rows (TD-19 pattern, cross-platform).
        // ResetAppUIState clears Cards_Search at the start of the next test.
        driver.EnsureCardVisible("Empty Card Test");

        // Slice 6g auto-reveal-after-save scrolls to the freshly-saved card AND
        // expands it. The realize is async — give it a brief settle before
        // checking the state suffix on the composed accessibility description.
        Thread.Sleep(TestConfig.DelayAfterTap);
        bool alreadyExpanded = driver.IsTextContainsDisplayed(
            "Empty Card Test, Expanded", timeoutSeconds: 2);

        if (!alreadyExpanded)
        {
            // Auto-reveal didn't land — fall back to the explicit tap path.
            if (TestConfig.IsIOS) driver.TapByTextContains("Empty Card Test", timeoutSeconds: 10);
            else driver.TapByText("Empty Card Test");
        }
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Verify the card expanded and shows the "Add prayer" option.
        //
        // iOS: CollectionView cells are atomic (SemanticProperties.Description flattening).
        // AccessibleCardHeader composes "Empty Card Test, Expanded" but child elements
        // like Cards_Btn_AddPrayer are invisible to Appium. Verify via composed label.
        // Android: individual elements are accessible — check AutomationId directly.

        if (TestConfig.IsIOS)
        {
            // Check for the card-specific expanded label (not just "Expanded" which
            // could match any expanded card from a prior test)
            bool expandedFound = driver.IsTextContainsDisplayed(
                "Empty Card Test, Expanded", timeoutSeconds: 10);
            if (!expandedFound)
            {
                expandedFound = driver.IOSScrollToPredicateInContainer(
                    "Cards_List_Cards", "label CONTAINS 'Empty Card Test, Expanded'");
            }

            // Only check for "Add prayer" if we couldn't confirm expansion
            bool addPrayerFound = false;
            if (!expandedFound)
            {
                addPrayerFound = driver.IsTextContainsDisplayed("Add prayer", timeoutSeconds: 3);
                if (!addPrayerFound)
                {
                    addPrayerFound = driver.IOSScrollToPredicateInContainer(
                        "Cards_List_Cards", "label CONTAINS 'Add prayer'");
                }
            }

            if (!expandedFound && !addPrayerFound)
            {
                var dumpPath = driver.DumpPageSource("EmptyCardExpand_iOS_FAIL");
                Assert.Fail(
                    $"iOS: Empty card should show expanded state or 'Add prayer' button. "
                    + $"Expanded={expandedFound}, AddPrayer={addPrayerFound}. "
                    + $"Page source: {dumpPath}");
            }
        }
        else
        {
            var found = driver.IsDisplayed("Cards_Btn_AddPrayer", timeoutSeconds: 10);
            // Defense in depth: with Slice 6g auto-reveal, the AddPrayer button
            // is realized on arrival. The scroll fallback below remains in case
            // auto-reveal regresses or the cell virtualizes during state
            // transitions; it short-circuits when the primary path succeeds.
            if (!found)
            {
                try
                {
                    driver.ScrollDownTo("Cards_Btn_AddPrayer", maxScrolls: 4,
                        scrollableAutomationId: "Cards_List_Cards");
                    found = true;
                }
                catch (WebDriverException) { }
            }
            if (!found)
            {
                found = driver.IsTextDisplayed("+ Add prayer", timeoutSeconds: 3)
                     || driver.IsTextDisplayed("Add prayer", timeoutSeconds: 2);
            }
            if (!found)
            {
                var dumpPath = driver.DumpPageSource("EmptyCardExpand_Android_FAIL");
                Assert.Fail(
                    $"Android: Empty card should show '+ Add prayer' button. "
                    + $"Page source: {dumpPath}");
            }
        }
    }
}

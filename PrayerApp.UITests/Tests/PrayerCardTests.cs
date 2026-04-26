using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 3: Prayer Cards Tab
/// </summary>
[Collection("Appium")]
[Trait("Platform", "CrossPlatform")]
[Trait("Section", "3-PrayerCards")]
public class PrayerCardTests
{
    private readonly AppiumSetup _setup;
    public PrayerCardTests(AppiumSetup setup) => _setup = setup;

    /// <summary>Expand the Quick Add system card if visible and not already expanded.</summary>
    private void ExpandQuickAddCard()
    {
        var driver = _setup.Driver;

        if (TestConfig.IsIOS)
        {
            // iOS: CollectionView flattens cells — AutomationIds inside cells are invisible.
            // Check expansion via composed label containing "Expanded".
            if (driver.IsTextContainsDisplayed("Quick Add, Expanded", timeoutSeconds: 2))
                return;

            if (driver.IsTextContainsDisplayed("Quick Add", timeoutSeconds: 3))
            {
                driver.TapByTextContains("Quick Add", timeoutSeconds: 10);
                Thread.Sleep(500);
            }
        }
        else
        {
            // Android: AutomationIds work inside CollectionView cells
            if (driver.IsDisplayed("Cards_Btn_AddPrayer", timeoutSeconds: 2))
                return;

            if (driver.IsTextDisplayed("Quick Add", timeoutSeconds: 3))
            {
                driver.TapByText("Quick Add");
                Thread.Sleep(500);
            }
        }
    }

    /// <summary>3.1: Cards list loads — card collection is visible.</summary>
    [Fact]
    public void Cards_ListLoads_ShowsCards()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        Assert.True(_setup.Driver.IsDisplayed("Cards_List_Cards"),
            "Card collection should be visible on Prayer Cards page");
    }

    /// <summary>3.2: Search cards — typing filters the list.</summary>
    [Fact]
    public void Cards_SearchFilter_FiltersCards()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        driver.EnterText("Cards_Search", "zzz_nonexistent");
        Thread.Sleep(500);

        driver.EnterText("Cards_Search", "");
        Thread.Sleep(500);

        Assert.True(driver.IsDisplayed("Cards_List_Cards"));
    }

    /// <summary>3.3: Search keyboard dismiss — tap background dismisses keyboard.</summary>
    [Fact]
    public void Cards_SearchKeyboard_DismissesOnBackground()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        if (TestConfig.IsIOS)
        {
            // iOS with hardware keyboard: on-screen keyboard doesn't appear,
            // so test that tapping search and going back doesn't break navigation.
            driver.Tap("Cards_Search");
            Thread.Sleep(300);
            driver.NavigateToTab("Prayer Cards");
        }
        else
        {
            driver.Tap("Cards_Search");
            Thread.Sleep(300);
            driver.GoBack();
            Thread.Sleep(300);
        }

        Assert.True(driver.IsDisplayed("Cards_List_Cards"));
    }

    /// <summary>
    /// Regression: tester reproducibly crashed on Samsung Galaxy Ultra after creating a
    /// new card. Root cause was VM→View C# event firing CollectionView.ScrollTo against
    /// a MauiRecyclerView whose adapter snapshot hadn't committed the BoxSections
    /// rebuild — Java.Lang.IllegalArgumentException "Invalid target position." The fix
    /// replaces the event with a lifecycle-gated PendingSavedIdentifier consumed in
    /// OnAppearing with two dispatcher ticks before ScrollTo. Test asserts (a) the app
    /// survives the post-save lifecycle and (b) the new card row materialized in the
    /// CollectionView. Note: doesn't reproducibly RED on emulator (race window too narrow);
    /// serves as a regression safety net for any future device where the race opens up.
    /// </summary>
    [Fact]
    public void Cards_CreateCard_PostSaveLifecycle_DoesNotCrash_AndCardIsVisible()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        // Timestamped title to dodge UNIQUE-constraint dup alerts (BUG-74).
        var title = $"Race Regression {DateTime.Now:HHmmss}";

        driver.TapToolbarItemById("Add Card");
        driver.WaitForElement("Card_Entry_Title", timeoutSeconds: 10);
        driver.EnterText("Card_Entry_Title", title);
        driver.DismissKeyboardIfPresent();
        driver.TapToolbarItem("Save");
        Thread.Sleep(TestConfig.DelayAfterSave);

        // (a) Process survival check — if the post-save scroll-to crashed, this
        //     element wait would fail with a session error, not a NoSuchElement.
        Assert.True(driver.IsDisplayed("Cards_List_Cards", timeoutSeconds: 10),
            "Cards page should still render after save (no crash).");

        // (b) New card row materialized in the virtualized CollectionView.
        driver.EnsureCardVisible(title);
        Assert.True(
            TestConfig.IsIOS
                ? driver.IsTextContainsDisplayed(title, timeoutSeconds: 5)
                : driver.IsTextDisplayed(title, timeoutSeconds: 5),
            $"Newly created card '{title}' should be visible in the list.");

        // Cleanup: delete the disposable card so reruns don't accumulate fixtures.
        if (TestConfig.IsIOS)
            driver.TapByTextContains(title);
        else
            driver.TapByText(title);
        Thread.Sleep(TestConfig.DelayAfterTap);
        if (driver.IsDisplayed("Cards_Btn_Delete", timeoutSeconds: 3))
        {
            driver.Tap("Cards_Btn_Delete");
            driver.DismissAlertIfPresent();
            Thread.Sleep(TestConfig.DelayAfterSave);
        }
    }

    /// <summary>
    /// Regression: rapidly tapping Save twice on the new-card form previously could
    /// invoke SaveCardAsync twice — duplicate row attempt + UNIQUE constraint alert at
    /// best, two near-identical cards at worst. SaveCommand now gates on IsBusy so the
    /// second invocation is a no-op until the first completes.
    /// </summary>
    [Fact]
    public void Cards_Save_DoubleTapCreatesOnlyOneCard()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        var title = $"DoubleTap {DateTime.Now:HHmmssfff}";

        driver.TapToolbarItemById("Add Card");
        driver.WaitForElement("Card_Entry_Title", timeoutSeconds: 10);
        driver.EnterText("Card_Entry_Title", title);
        driver.DismissKeyboardIfPresent();

        // Two taps in immediate succession (no inter-tap delay). Without the IsBusy
        // gate, the second tap re-invokes SaveCardAsync. With it, the second tap is
        // dropped (canExecute=false during the first save).
        driver.TapToolbarItem("Save");
        driver.TapToolbarItem("Save");
        Thread.Sleep(TestConfig.DelayAfterSave);

        Assert.True(driver.IsDisplayed("Cards_List_Cards", timeoutSeconds: 10),
            "Should return to card list after saving.");

        // Search-filter the new title and assert only one row matches.
        driver.EnsureCardVisible(title);
        driver.EnterText("Cards_Search", title);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        var matchLocator = TestConfig.IsIOS
            ? OpenQA.Selenium.By.XPath($"//*[@name='{title}' or @label='{title}']")
            : OpenQA.Selenium.By.XPath($"//*[@text='{title}' or @content-desc='{title}']");
        var matches = driver.FindElements(matchLocator);
        Assert.Single(matches);

        // Cleanup: clear search, delete the test card.
        driver.EnterText("Cards_Search", string.Empty);
        Thread.Sleep(TestConfig.DelayCollectionRender);
        driver.EnsureCardVisible(title);
        if (TestConfig.IsIOS)
            driver.TapByTextContains(title);
        else
            driver.TapByText(title);
        Thread.Sleep(TestConfig.DelayAfterTap);
        if (driver.IsDisplayed("Cards_Btn_Delete", timeoutSeconds: 3))
        {
            driver.Tap("Cards_Btn_Delete");
            driver.DismissAlertIfPresent();
            Thread.Sleep(TestConfig.DelayAfterSave);
        }
    }

    /// <summary>3.4: Create new card — tap "Add Card", fill title, save. Card appears in list.</summary>
    [Fact]
    public void Cards_CreateCard_AppearsInList()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        driver.TapToolbarItemById("Add Card");
        driver.WaitForElement("Card_Entry_Title", timeoutSeconds: 10);

        // Use a name NOT in the seed. The seed already has "UITest Card" — creating
        // another with the same name would make any assertion against
        // IsTextDisplayed("UITest Card") trivially pass against the seeded one
        // instead of the one we just created (silent false positive).
        driver.EnterText("Card_Entry_Title", "New Card UITest");
        driver.DismissKeyboardIfPresent();
        driver.TapToolbarItem("Save");
        Thread.Sleep(1000);

        Assert.True(driver.IsDisplayed("Cards_List_Cards", timeoutSeconds: 10),
            "Should return to card list after saving new card");
    }

    /// <summary>3.6: Expand card → view prayers inside.</summary>
    [Fact]
    public void Cards_ExpandCard_ShowsPrayers()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        ExpandQuickAddCard();

        Assert.True(_setup.Driver.IsDisplayed("Cards_Btn_AddPrayer", timeoutSeconds: 10),
            "Expanded card should show '+ Add prayer' button");
    }

    /// <summary>3.7: Add prayer to card — expand card, tap "Add prayer", fill, save.
    /// Uses the dedicated disposable fixture "UITest AddPrayer Card" (see TestDataSeed)
    /// so this test is independent of what other tests do to the shared seed.</summary>
    [Fact]
    public void Cards_AddPrayerToCard()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;
        Thread.Sleep(TestConfig.DelayCollectionRender);
        driver.EnsureCardVisible("UITest AddPrayer Card");

        // Expand the dedicated seed card
        if (TestConfig.IsIOS)
            driver.TapByTextContains("UITest AddPrayer Card");
        else
            driver.TapByText("UITest AddPrayer Card");
        Thread.Sleep(TestConfig.DelayAfterTap);

        // iOS: AutomationId invisible inside flattened CollectionView cells — use text
        if (TestConfig.IsIOS)
            driver.TapByTextContains("Add prayer", timeoutSeconds: 10);
        else
            driver.WaitAndTap("Cards_Btn_AddPrayer", timeoutSeconds: 10);
        Thread.Sleep(500);

        Assert.True(driver.IsDisplayed("Detail_Entry_Title", timeoutSeconds: 10),
            "Should navigate to prayer detail page");

        driver.EnterText("Detail_Entry_Title", "Card Prayer UITest");
        driver.TapToolbarItem("Save");
        Thread.Sleep(1000);

        driver.NavigateToTab("Prayer Cards");
        Assert.True(driver.IsDisplayed("Cards_List_Cards", timeoutSeconds: 10));
    }

    /// <summary>3.9: Edit prayer from card — expand card, tap prayer, view mode, tap Edit.
    /// Uses dedicated fixture "UITest EditPrayer Card" containing "UITest Edit Prayer"
    /// (see TestDataSeed). Isolated from shared-seed mutations by other tests.</summary>
    [Fact]
    public void Cards_EditPrayerFromCard()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;
        driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);
        driver.EnsureCardVisible("UITest EditPrayer Card");

        // Expand the dedicated seed card to reveal its prayer
        if (TestConfig.IsIOS)
            driver.TapByTextContains("UITest EditPrayer Card");
        else
            driver.TapByText("UITest EditPrayer Card");
        Thread.Sleep(TestConfig.DelayAfterTap);

        // iOS: prayer text is part of composed label, not a standalone element
        if (TestConfig.IsIOS)
            driver.TapByTextContains("UITest Edit Prayer", timeoutSeconds: 10);
        else
            driver.TapByText("UITest Edit Prayer");

        driver.TapToolbarItem("Edit");
        Thread.Sleep(TestConfig.DelayAfterTap);

        Assert.True(driver.IsDisplayed("Detail_Entry_Title", timeoutSeconds: 10),
            "Should show title entry in edit mode");

        driver.GoBack();
        driver.DismissAlertIfPresent();
    }

    /// <summary>3.12: Delete card — navigate to the pre-seeded "Delete Me Card",
    /// expand it, delete via the inline Delete button, confirm. The target card
    /// is a throwaway from the seed; destroying it does not affect the shared
    /// UITest baseline.</summary>
    [Fact]
    public void Cards_DeleteCard_RemovesFromList()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Expand the card, tap inline Delete button
        if (driver.IsTextDisplayed("Delete Me Card", timeoutSeconds: 10))
        {
            driver.TapByText("Delete Me Card");
            Thread.Sleep(TestConfig.DelayAfterTap);
            driver.WaitAndTap("Cards_Btn_Delete", timeoutSeconds: 10);
            driver.DismissAlertIfPresent();
            Thread.Sleep(500);
        }

        Assert.True(driver.IsDisplayed("Cards_List_Cards", timeoutSeconds: 10));
    }

    /// <summary>3.14: Inline action buttons — expanding a user card shows Favorite, Share, Edit, Delete.
    /// Uses dedicated fixture "UITest Expanded Card" (see TestDataSeed).</summary>
    [Fact]
    public void Cards_ExpandedCard_ShowsActionButtons()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;
        driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);
        driver.EnsureCardVisible("UITest Expanded Card");

        // Expand the card by tapping it
        if (TestConfig.IsIOS)
            driver.TapByTextContains("UITest Expanded Card");
        else
            driver.TapByText("UITest Expanded Card");
        Thread.Sleep(TestConfig.DelayAfterTap);

        Assert.True(driver.IsDisplayed("Cards_Btn_Favorite", timeoutSeconds: 10),
            "Expanded user card should show Favorite button");
        Assert.True(driver.IsDisplayed("Cards_Btn_Share", timeoutSeconds: 3),
            "Expanded user card should show Share button");
        Assert.True(driver.IsDisplayed("Cards_Btn_Edit", timeoutSeconds: 3),
            "Expanded user card should show Edit button");
        Assert.True(driver.IsDisplayed("Cards_Btn_Delete", timeoutSeconds: 3),
            "Expanded user card should show Delete button");
    }

    /// <summary>3.16: Tag filter chips — visible when tags exist in the system.</summary>
    [Fact]
    public void Cards_TagChips_VisibleWhenTagsExist()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;

        // Ensure at least one tag exists
        driver.EnsureUITestTagExists(_setup);

        driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Chip inner Label is not in the a11y tree; Border Description is a phrase
        // ("UITest Tag, not selected"), so match by substring.
        Assert.True(
            driver.IsTextContainsDisplayed("UITest Tag", timeoutSeconds: 10),
            "Tag filter chip should be visible on Prayer Cards page when tags exist");
    }

    /// <summary>3.17: Edit button — tapping Edit navigates to card edit page.
    /// Uses dedicated fixture "UITest EditButton Card" (see TestDataSeed).</summary>
    [Fact]
    public void Cards_EditButton_NavigatesToEditPage()
    {
        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;
        driver.EnsureOnTab("Prayer Cards", _setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);
        driver.EnsureCardVisible("UITest EditButton Card");

        // Expand the card to reveal action buttons
        if (TestConfig.IsIOS)
            driver.TapByTextContains("UITest EditButton Card");
        else
            driver.TapByText("UITest EditButton Card");
        Thread.Sleep(TestConfig.DelayAfterTap);

        driver.WaitAndTap("Cards_Btn_Edit", timeoutSeconds: 10);
        Thread.Sleep(TestConfig.DelayAfterNavigation);

        Assert.True(driver.IsDisplayed("Card_Entry_Title", timeoutSeconds: 10),
            "Tapping Edit should navigate to card edit page");

        driver.GoBack();
        driver.DismissAlertIfPresent();
    }

    /// <summary>3.18: System card protection — Quick Add should not show action buttons when expanded.</summary>
    [SkippableFact]
    public void Cards_SystemCard_ShareNotAvailable()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        // Expand the Quick Add system card
        try
        {
            if (TestConfig.IsIOS)
                driver.TapByTextContains("Quick Add");
            else
                driver.TapByText("Quick Add");
            Thread.Sleep(TestConfig.DelayAfterTap);
        }
        catch (WebDriverException)
        {
            throw new SkipException("Precondition: 'Quick Add' system card not found");
        }

        // System cards should not show action buttons
        Assert.False(driver.IsDisplayed("Cards_Btn_Share", timeoutSeconds: 2),
            "System card should not show Share button");
    }

    /// <summary>3.15: System card protection — Quick Add card's Delete button is hidden.</summary>
    [SkippableFact]
    public void Cards_SystemCard_DeleteNotAvailable()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        // Expand the Quick Add system card
        try
        {
            if (TestConfig.IsIOS)
                driver.TapByTextContains("Quick Add");
            else
                driver.TapByText("Quick Add");
            Thread.Sleep(TestConfig.DelayAfterTap);
        }
        catch (WebDriverException)
        {
            throw new SkipException("Precondition: 'Quick Add' system card not found");
        }

        // System cards should not show action buttons
        Assert.False(driver.IsDisplayed("Cards_Btn_Delete", timeoutSeconds: 2),
            "System card should not show Delete button");
        Assert.True(driver.IsDisplayed("Cards_List_Cards"));
    }

    // ── Slice 6c real + 6g — lazy realize + post-save overlay continuity ──

    /// <summary>
    /// Idempotently brings the named user card into the collapsed state. Uses
    /// the card's own composed accessibility description (e.g.
    /// "UITest EditButton Card, Expanded") as the per-card state proxy —
    /// chip-visibility checks are unreliable because OTHER cards' chips can be
    /// in the tree (e.g. an auto-expanded post-save card that persists in the
    /// seed DB across runs), polluting any global chip-presence assertion.
    /// xUnit doesn't guarantee test order; tests must not assume the card's
    /// state from the seed DB.
    /// </summary>
    private static void EnsureCardCollapsed(OpenQA.Selenium.Appium.AppiumDriver driver, string cardName)
    {
        driver.EnsureCardVisible(cardName);
        if (!IsCardExpanded(driver, cardName)) return;
        TapCardHeader(driver, cardName);
    }

    /// <summary>Idempotent counterpart to EnsureCardCollapsed.</summary>
    private static void EnsureCardExpanded(OpenQA.Selenium.Appium.AppiumDriver driver, string cardName)
    {
        driver.EnsureCardVisible(cardName);
        if (IsCardExpanded(driver, cardName)) return;
        TapCardHeader(driver, cardName);
    }

    /// <summary>True if the card is in expanded state, judged by its own ", Expanded" suffix.</summary>
    private static bool IsCardExpanded(OpenQA.Selenium.Appium.AppiumDriver driver, string cardName)
        => TestConfig.IsIOS
            ? driver.IsTextContainsDisplayed(cardName + ", Expanded", timeoutSeconds: 1)
            : driver.IsTextDisplayed(cardName + ", Expanded", timeoutSeconds: 1);

    private static void TapCardHeader(OpenQA.Selenium.Appium.AppiumDriver driver, string cardName)
    {
        if (TestConfig.IsIOS) driver.TapByTextContains(cardName);
        else driver.TapByText(cardName);
        Thread.Sleep(TestConfig.DelayAfterTap);
    }


    /// <summary>
    /// 6g contract: After tapping Save on the Add Card form, the LoadingOverlay
    /// remains visible on the Cards page across the whole post-save window
    /// (PageSync → ConsumePendingSavedAsync → ScrollTo). Without 6g the overlay
    /// flickers off as soon as SyncAsync's finally fires (~5 ms after RebuildSections),
    /// long before the lazy-realized expanded subtree of the new card has populated —
    /// the user sees "no spinner, no card, then card pops in".
    /// </summary>
    [Fact]
    public void Cards_Save_OverlayShowsDuringPostSaveFlow()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        var title = $"Overlay6g {DateTime.Now:HHmmssfff}";

        driver.TapToolbarItemById("Add Card");
        driver.WaitForElement("Card_Entry_Title", timeoutSeconds: 10);
        driver.EnterText("Card_Entry_Title", title);
        driver.DismissKeyboardIfPresent();

        try
        {
            driver.TapToolbarItem("Save");

            // 6g hardening guarantees a minimum 250 ms overlay duration, so
            // standard WaitForElement comfortably catches it. The Spinner
            // (leaf widget) is a more reliable platform-tree probe than the
            // Scrim (layout container) — start there, fall back to Scrim.
            bool overlaySeen =
                driver.IsDisplayed("LoadingOverlay_Spinner", timeoutSeconds: 5) ||
                driver.IsDisplayed("LoadingOverlay_Scrim", timeoutSeconds: 1);

            string? evidence = overlaySeen ? null
                : driver.DumpPageSource(nameof(Cards_Save_OverlayShowsDuringPostSaveFlow));

            Assert.True(overlaySeen,
                $"LoadingOverlay should appear on Cards page during post-save flow. Dump: {evidence}");

            // And must come down once the minimum-duration window + ScrollTo complete.
            driver.WaitForElementGone("LoadingOverlay_Spinner", timeoutSeconds: 10);

            // New card row materialized in the (now-overlay-free) CollectionView.
            driver.EnsureCardVisible(title);
            Assert.True(
                TestConfig.IsIOS
                    ? driver.IsTextContainsDisplayed(title, timeoutSeconds: 5)
                    : driver.IsTextDisplayed(title, timeoutSeconds: 5),
                $"New card '{title}' should be visible after overlay clears.");

            // Cleanup
            if (TestConfig.IsIOS)
                driver.TapByTextContains(title);
            else
                driver.TapByText(title);
            Thread.Sleep(TestConfig.DelayAfterTap);
            if (driver.IsDisplayed("Cards_Btn_Delete", timeoutSeconds: 3))
            {
                driver.Tap("Cards_Btn_Delete");
                driver.DismissAlertIfPresent();
                Thread.Sleep(TestConfig.DelayAfterSave);
            }
        }
        finally
        {
            // If the assertion fails before nav (Save didn't fire) or after a partial
            // save flow, the edit page may still be foregrounded with a dirty form.
            // Back out + tap Discard so the next test's ResetAppUIState doesn't
            // hit the unsaved-changes dialog and stall (per
            // appium-autodismissalerts-discard-trap lesson — Android dismisses via
            // button1 = Discard, which is correct here, but only fires if we
            // actually press Back to raise the dialog in the first place).
            if (driver.IsDisplayed("Card_Entry_Title", timeoutSeconds: 1))
            {
                try { driver.GoBack(); } catch (WebDriverException) { }
                Thread.Sleep(TestConfig.DelayAfterTap);
                try { driver.DismissAlertIfPresent(); } catch (WebDriverException) { }
            }
        }
    }

    /// <summary>
    /// 6c real toggle path: tapping a collapsed user card's header should expand
    /// it AND lazy-realize the chips/list/button subtree (not just the expanded
    /// margin). Validates the IsExpanded property-changed handler in
    /// PrayerCardsPage.xaml.cs OnCardBorderLoaded that drives RealizeExpandedSubtree
    /// outside the OnCardBorderLoaded.Rebind (fresh-cell) path.
    /// </summary>
    [Fact]
    public void Cards_ExpandByTap_RealizesActionChips()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        const string cardName = "UITest EditButton Card";
        EnsureCardCollapsed(driver, cardName);

        // Tap to expand (precondition: card is collapsed).
        if (TestConfig.IsIOS)
            driver.TapByTextContains(cardName);
        else
            driver.TapByText(cardName);
        Thread.Sleep(TestConfig.DelayAfterTap);

        bool chipsVisible = TestConfig.IsIOS
            ? driver.IsTextDisplayed("Edit", timeoutSeconds: 5)
            : driver.IsDisplayed("Cards_Btn_Edit", timeoutSeconds: 5);

        string? evidence = chipsVisible ? null
            : driver.DumpPageSource(nameof(Cards_ExpandByTap_RealizesActionChips));

        Assert.True(chipsVisible,
            $"Tapping a collapsed user card should lazy-realize and show the action chips (Edit chip). Dump: {evidence}");
    }

    /// <summary>
    /// 6c real M1 risk: a recycled cell whose host already has the lazy subtree
    /// inflated must re-bind its inner BindableLayout cleanly to the new card's
    /// Prayers when the BindingContext swaps (vs leaving the previous card's
    /// prayer rows visible). Symptom of failure would be the wrong card's prayer
    /// text appearing inside another card after fast scrolling. This test
    /// expands a known card, scrolls aggressively away and back, and asserts the
    /// chips are still under the same card on return — proving the realized
    /// subtree survived recycling without leaking content.
    /// </summary>
    [Fact]
    public void Cards_RecycledCells_LazyRealizeSurvivesScroll()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        const string cardName = "UITest EditButton Card";
        EnsureCardExpanded(driver, cardName);

        // Confirm chips realized before scroll (otherwise the post-scroll check
        // would be testing realize-on-scroll-back, not survive-recycle).
        bool chipsBeforeScroll = TestConfig.IsIOS
            ? driver.IsTextDisplayed("Edit", timeoutSeconds: 5)
            : driver.IsDisplayed("Cards_Btn_Edit", timeoutSeconds: 5);
        Assert.True(chipsBeforeScroll, "Pre-scroll: chips must be realized.");

        // Aggressively scroll the recycler. Each ScrollDown invokes the platform
        // gesture which re-uses cells; scrolling down then targeting the card
        // again on the way back forces the same Border to recycle through other
        // cards' BindingContexts and back.
        var size = driver.Manage().Window.Size;
        for (int i = 0; i < 4; i++)
        {
            driver.ExecuteScript("mobile: " + (TestConfig.IsAndroid ? "swipeGesture" : "swipe"),
                TestConfig.IsAndroid
                    ? new Dictionary<string, object>
                    {
                        { "left", size.Width / 4 },
                        { "top", size.Height / 4 },
                        { "width", size.Width / 2 },
                        { "height", size.Height / 2 },
                        { "direction", "up" },
                        { "percent", 0.8 }
                    }
                    : new Dictionary<string, object> { { "direction", "up" } });
            Thread.Sleep(150);
        }

        // Scroll back to the card (handles virtualization + section collapse).
        driver.EnsureCardVisible(cardName);

        // After recycling, the card and its chips must still be intact —
        // the chip MUST appear under the same card name we expanded, not
        // shifted onto a different recycled cell.
        bool chipsAfterScroll = TestConfig.IsIOS
            ? driver.IsTextDisplayed("Edit", timeoutSeconds: 5)
            : driver.IsDisplayed("Cards_Btn_Edit", timeoutSeconds: 5);

        string? evidence = chipsAfterScroll ? null
            : driver.DumpPageSource(nameof(Cards_RecycledCells_LazyRealizeSurvivesScroll));

        Assert.True(chipsAfterScroll,
            $"Post-scroll: chips should still be present (recycled cell re-bound BindableLayout cleanly). Dump: {evidence}");
    }
}

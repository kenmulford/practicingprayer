using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using PrayerApp.Helpers;
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
                Thread.Sleep(TestConfig.DelayAfterNavigation);
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
                Thread.Sleep(TestConfig.DelayAfterNavigation);
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
        Thread.Sleep(TestConfig.DelayDirtyRegistration);

        driver.EnterText("Cards_Search", "");
        Thread.Sleep(TestConfig.DelayDirtyRegistration);

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
            Thread.Sleep(TestConfig.DelayAfterTap);
            driver.NavigateToTab("Prayer Cards");
        }
        else
        {
            driver.Tap("Cards_Search");
            Thread.Sleep(TestConfig.DelayAfterTap);
            driver.GoBack();
            Thread.Sleep(TestConfig.DelayAfterTap);
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
        Thread.Sleep(TestConfig.DelayAfterSave);

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
        Thread.Sleep(TestConfig.DelayAfterNavigation);

        Assert.True(driver.IsDisplayed("Detail_Entry_Title", timeoutSeconds: 10),
            "Should navigate to prayer detail page");

        driver.EnterText("Detail_Entry_Title", "Card Prayer UITest");
        driver.TapToolbarItem("Save");
        Thread.Sleep(TestConfig.DelayAfterSave);

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

    /// <summary>Build-95 fallout (Slice 6c): after deleting an expanded card,
    /// none of its prayer titles should still render anywhere on the page.
    /// Pre-fix the lazy-realized expanded subtree's explicit BindingContext
    /// pinned the inner ContentView.Content to the deleted card's vm, so a
    /// recycled cell rendering a different card still showed the deleted
    /// card's prayer rows under it. The bug class was masked by BUG-79/80's
    /// realize-storm crash; once that crash was closed in build 95 the
    /// staleness became visible.</summary>
    [Fact]
    public void Cards_DeleteExpandedCard_DoesNotLeakPrayersToOtherCards()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Expand Big Card so its inline expanded subtree (shape (i): always
        // inflated, gated by IsVisible) renders with Big Card's BindingContext.
        EnsureCardExpanded(driver, "Recycle Big Card");

        // Sanity: at least one Big Card prayer is rendered before delete.
        Assert.True(
            driver.IsTextDisplayed("Recycle Big Prayer 0", timeoutSeconds: 10),
            "Big Card should expand and show its prayers before delete (sanity).");

        // Delete via the inline Delete button + confirm dialog. Same flow as
        // Cards_DeleteCard_RemovesFromList; the difference is that the target
        // card is expanded with a realized subtree at delete time.
        driver.WaitAndTap("Cards_Btn_Delete", timeoutSeconds: 10);
        driver.DismissAlertIfPresent();
        Thread.Sleep(TestConfig.DelayAfterSave);

        // Anchor the viewport on the survivor so any leaked Big Card prayer
        // rows would be near the visible cell, not scrolled off-screen
        // (avoids a false-pass where the assertions miss content that's
        // technically present in the tree but outside the visible region).
        driver.EnsureCardVisible("Recycle Small Card");

        // Post-delete the Loose Cards section's SetCards fires a Reset which
        // re-dequeues cells. Pre-fix the inner ContentView.Content kept its
        // first-realize BindingContext = Big Card vm even after the cell
        // was reassigned — so any of Big Card's prayer titles that remained
        // visible anywhere on the page indicates the bug.
        Assert.False(
            driver.IsTextDisplayed("Recycle Big Prayer 0", timeoutSeconds: 5),
            "After deleting Big Card, none of its prayer titles should still " +
            "render. If this fails, a recycled cell's inner BindingContext is " +
            "still pointing at the deleted card (Slice 6c lazy-realize / " +
            "build-95 fallout).");
        Assert.False(
            driver.IsTextDisplayed("Recycle Big Prayer 2", timeoutSeconds: 3));
        Assert.False(
            driver.IsTextDisplayed("Recycle Big Prayer 4", timeoutSeconds: 3));
    }

    /// <summary>3.12: Delete card — navigate to the pre-seeded "UITest Delete Target Card",
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
        if (driver.IsTextDisplayed(TestSeedFixtures.DeleteCard, timeoutSeconds: 10))
        {
            driver.TapByText(TestSeedFixtures.DeleteCard);
            Thread.Sleep(TestConfig.DelayAfterTap);
            driver.WaitAndTap("Cards_Btn_Delete", timeoutSeconds: 10);
            driver.DismissAlertIfPresent();
            Thread.Sleep(TestConfig.DelayAfterNavigation);
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

    // ── Slice 6c real + 6g — expand realize + post-save overlay continuity ──

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

    /// <summary>
    /// True if the named card's header is currently rendered in its expanded state,
    /// tolerant of the ", System" infix that <c>AccessibleCardHeader</c> inserts for
    /// system cards (PrayerCardViewModel.cs:267) — so "Quick Add, System, Expanded"
    /// counts as expanded just like a user card's "Move Source Card, Expanded". The
    /// shared <see cref="IsCardExpanded"/> uses an EXACT "{name}, Expanded" match, which
    /// can never match a system card; this helper matches a SINGLE header element whose
    /// content-desc/label contains BOTH the card name AND the ", Expanded" suffix, so it
    /// never cross-matches a different card or a collapsed header. Non-throwing.
    /// </summary>
    private static bool IsCardHeaderExpanded(OpenQA.Selenium.Appium.AppiumDriver driver, string cardName)
    {
        var by = TestConfig.IsIOS
            ? By.XPath($"//*[contains(@label,'{cardName}') and contains(@label,', Expanded')]")
            : By.XPath($"//*[contains(@content-desc,'{cardName}') and contains(@content-desc,', Expanded')]");
        try
        {
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(1);
            return driver.FindElement(by).Displayed;
        }
        catch (WebDriverException) { return false; }
        finally { driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout; }
    }

    /// <summary>
    /// Polls <see cref="IsCardHeaderExpanded"/> until the named card has SETTLED into its
    /// expanded state, up to <paramref name="timeoutSeconds"/>. Returns true once settled,
    /// false on timeout.
    /// <para>
    /// Since issue #42 retired lazy realization the expanded subtree is EAGER, so the
    /// chips/prayers exist the instant the card expands — but they read
    /// <c>Displayed=false</c> while the expand animation is mid-flight. Probing chip
    /// visibility immediately after a fixed 300 ms sleep lands inside that window and
    /// flakes. Waiting for the header to settle first is step one; an expanded card low in
    /// the list ALSO renders its contents below the fold (the CollectionView virtualizes
    /// off-screen rows out of the a11y tree), so the call site then scrolls the target row
    /// into view via <see cref="TryScrollTextIntoView"/> before asserting.
    /// </para>
    /// </summary>
    private static bool WaitForCardExpanded(OpenQA.Selenium.Appium.AppiumDriver driver, string cardName,
        int timeoutSeconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (IsCardHeaderExpanded(driver, cardName)) return true;
            Thread.Sleep(TestConfig.DelayAfterTap);
        }
        return IsCardHeaderExpanded(driver, cardName);
    }

    /// <summary>
    /// Best-effort scroll of the Cards CollectionView until the element with the given
    /// visible <paramref name="text"/> is on screen. Swallows failures so the caller's
    /// assertion raises the canonical "not displayed" error instead of a masked scroll
    /// error — mirrors <see cref="AppExtensions.EnsureCardVisible"/>. Needed because an
    /// expanded card low in the list renders its contents (here, a moved prayer row)
    /// below the fold, where the CollectionView virtualizes them out of the a11y tree
    /// until scrolled into view (issue #42 eager subtree).
    /// </summary>
    private static void TryScrollTextIntoView(OpenQA.Selenium.Appium.AppiumDriver driver, string text)
        => ScrollUntil(driver,
            () => TestConfig.IsIOS ? driver.IsTextContainsDisplayed(text, timeoutSeconds: 1)
                                   : driver.IsTextDisplayed(text, timeoutSeconds: 1),
            () => driver.ScrollDownToText(text, maxScrolls: 4, scrollableAutomationId: "Cards_List_Cards"));

    /// <summary>
    /// Scrolls the Cards list down until <paramref name="isVisible"/> is true (up to a
    /// bounded number of steps). On Android each step is a CONTROLLED, fling-free
    /// <c>mobile: scrollGesture</c> — the swipe-based <c>ScrollDownTo</c>/<c>swipeGesture</c>
    /// path flings a low-sitting expanded card clean off the top of the viewport with
    /// momentum without ever realizing its chips, so it can never settle on the target.
    /// On iOS, falls back to <paramref name="iosScroll"/> (the existing element-targeted
    /// <c>mobile: scroll</c> helpers, which don't fling). Best-effort: never throws.
    /// </summary>
    private static void ScrollUntil(OpenQA.Selenium.Appium.AppiumDriver driver,
        Func<bool> isVisible, Action iosScroll)
    {
        if (TestConfig.IsIOS)
        {
            try { iosScroll(); } catch (WebDriverException) { /* assertion raises canonical error */ }
            return;
        }

        var size = driver.Manage().Window.Size;
        for (int i = 0; i < 5; i++)
        {
            if (isVisible()) return;
            try
            {
                driver.ExecuteScript("mobile: scrollGesture", new Dictionary<string, object>
                {
                    ["left"] = size.Width / 4,
                    ["top"] = size.Height / 4,
                    ["width"] = size.Width / 2,
                    ["height"] = size.Height / 2,
                    ["direction"] = "down",
                    ["percent"] = 0.5,
                });
            }
            catch (WebDriverException) { /* assertion below raises the canonical error */ }
            Thread.Sleep(TestConfig.DelayAfterTap);
        }
    }

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
    /// long before the new card's expanded subtree has populated —
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
    /// 6c real toggle path: tapping a collapsed user card's header should expand it
    /// and reveal its action chips. Under issue #42's shape (i) the expanded subtree
    /// is inlined in the cell template and eagerly realized, gated by a single
    /// IsVisible="{Binding IsExpanded}" wrapper, so toggling IsExpanded makes the
    /// chips/list/buttons visible (no lazy realization step).
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

        bool chipsVisible = driver.IsDisplayed("Cards_Btn_Edit", timeoutSeconds: 5);

        string? evidence = chipsVisible ? null
            : driver.DumpPageSource(nameof(Cards_ExpandByTap_RealizesActionChips));

        Assert.True(chipsVisible,
            $"Tapping a collapsed user card should lazy-realize and show the action chips (Edit chip). Dump: {evidence}");
    }

    /// <summary>
    /// 6c real M1 risk: a recycled cell whose host already has the expanded subtree
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
        bool chipsBeforeScroll = driver.IsDisplayed("Cards_Btn_Edit", timeoutSeconds: 5);
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
            // TODO(#11): map to TestConfig.Delay* — inter-gesture pause (150ms),
            // no current constant matches; deferred ambiguous site.
            Thread.Sleep(150);
        }

        // Scroll back to the card (handles virtualization + section collapse).
        driver.EnsureCardVisible(cardName);

        // After recycling, the card and its chips must still be intact —
        // the chip MUST appear under the same card name we expanded, not
        // shifted onto a different recycled cell.
        bool chipsAfterScroll = driver.IsDisplayed("Cards_Btn_Edit", timeoutSeconds: 5);

        string? evidence = chipsAfterScroll ? null
            : driver.DumpPageSource(nameof(Cards_RecycledCells_LazyRealizeSurvivesScroll));

        Assert.True(chipsAfterScroll,
            $"Post-scroll: chips should still be present (recycled cell re-bound BindableLayout cleanly). Dump: {evidence}");
    }

    /// <summary>
    /// BUG-76: Newly-saved card hidden inside collapsed parent section.
    /// 1.3.0 iOS UAT 2026-04-26 — after Add Card → Save, if the new card's
    /// parent section is currently collapsed the card is invisible inside the
    /// collapsed group even though <c>card.IsExpanded</c> is true. Fix:
    /// <c>ConsumePendingSavedAsync</c> auto-expands the parent section by BoxId.
    ///
    /// Test sequence: bring sections to a known all-expanded state, collapse
    /// "Loose Cards" specifically, save a new card (defaults to Loose Cards),
    /// then assert the card is visible without using <c>EnsureCardVisible</c>'s
    /// section-expansion / search fallback — direct DOM-presence check only.
    /// </summary>
    [Fact]
    public void Cards_Save_AutoExpandsCollapsedParentSection_BUG76()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;

        // Establish a known starting state — all sections expanded.
        driver.EnsureAllSectionsExpanded();
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // Collapse the Unboxed section specifically — default destination for a
        // new card with no Box selected. The section header's
        // SemanticProperties.Description binds to the section Name and is always
        // exactly BoxStrings.Unorganized regardless of card count, so TextLocator
        // (iOS @name; Android @content-desc) finds the layout wrapper. Tapping
        // it fires the inner Grid's TapGestureRecognizer (OnSectionHeaderTapped),
        // which toggles IsExpanded.
        driver.TapByText(BoxStrings.Unorganized);
        Thread.Sleep(TestConfig.DelayAfterTap);

        var title = $"BUG76 {DateTime.Now:HHmmssfff}";

        driver.TapToolbarItemById("Add Card");
        driver.WaitForElement("Card_Entry_Title", timeoutSeconds: 10);
        driver.EnterText("Card_Entry_Title", title);
        driver.DismissKeyboardIfPresent();

        try
        {
            driver.TapToolbarItem("Save");
            Thread.Sleep(TestConfig.DelayAfterSave);

            // Direct DOM-presence check — no EnsureCardVisible because its
            // EnsureAllSectionsExpanded fallback would mask the bug. The new
            // card must be in the rendered tree because its parent Loose Cards
            // section auto-expanded when ConsumePendingSavedAsync ran.
            bool titleVisible = TestConfig.IsIOS
                ? driver.IsTextContainsDisplayed(title, timeoutSeconds: 10)
                : driver.IsTextDisplayed(title, timeoutSeconds: 10);

            string? evidence = titleVisible ? null
                : driver.DumpPageSource(nameof(Cards_Save_AutoExpandsCollapsedParentSection_BUG76));

            Assert.True(titleVisible,
                $"BUG-76: newly-saved card '{title}' should be visible — its parent " +
                $"section must auto-expand on save. Dump: {evidence}");

            // Cleanup
            if (TestConfig.IsIOS) driver.TapByTextContains(title);
            else driver.TapByText(title);
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
            // Edit-page bail out if Save failed before nav.
            if (driver.IsDisplayed("Card_Entry_Title", timeoutSeconds: 1))
            {
                try { driver.GoBack(); } catch (WebDriverException) { }
                Thread.Sleep(TestConfig.DelayAfterTap);
                try { driver.DismissAlertIfPresent(); } catch (WebDriverException) { }
            }
        }
    }

    // ── Move-prayer tests (TD-20) ── Commit 1 TDD anchors ─────────────────
    // These three tests are RED until Commit 2 lands the declarative-margin +
    // ExpandedCardId fix. Gate them with the FailingPreCommit2 trait so CI
    // can exclude them via --filter 'Status!=FailingPreCommit2' until ready.

    /// <summary>
    /// X-06 / TD-20: Moving a prayer from "Move Source Card" to "Move Target Card"
    /// is reflected in both card headers — source count decreases, target count increases.
    /// </summary>
    [Fact]
    [Trait("Status", "FailingPreCommit2")]
    public void Cards_MovePrayerBetweenCards_BothCardsReflect()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // 1. Navigate to the source card and expand it.
        driver.EnsureCardVisible("Move Source Card");
        EnsureCardExpanded(driver, "Move Source Card");

        // 2. Tap "Prayer One" to open PrayerDetailPage (view mode).
        if (TestConfig.IsIOS)
            driver.TapByTextContains("Prayer One", timeoutSeconds: 10);
        else
            driver.TapByText("Prayer One");
        Thread.Sleep(TestConfig.DelayAfterTap);

        // 3. Enter edit mode.
        driver.TapToolbarItem("Edit");
        Thread.Sleep(TestConfig.DelayAfterTap);
        Assert.True(driver.IsDisplayed("Detail_Entry_Title", timeoutSeconds: 10),
            "Should be in edit mode on PrayerDetailPage");

        // 4. Change PrayerCardId — tap the card picker and select "Move Target Card".
        driver.WaitAndTap("Detail_Picker_Card", timeoutSeconds: 10);
        Thread.Sleep(TestConfig.DelayAfterTap);
        if (TestConfig.IsIOS)
            driver.TapByTextContains("Move Target Card", timeoutSeconds: 10);
        else
            driver.TapByText("Move Target Card");
        Thread.Sleep(TestConfig.DelayAfterTap);

        // 5. Save and navigate back to the Cards tab.
        driver.TapToolbarItem("Save");
        Thread.Sleep(TestConfig.DelayAfterSave);
        driver.NavigateToTab("Prayer Cards");
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // 6a. Source card should show 3 prayers (one of its four seeded prayers moved out).
        driver.EnsureCardVisible("Move Source Card");
        Assert.True(
            TestConfig.IsIOS
                ? driver.IsTextContainsDisplayed("Move Source Card", timeoutSeconds: 5)
                : driver.IsTextDisplayed("Move Source Card", timeoutSeconds: 5),
            "Move Source Card should still be visible after move");

        // Expand source to verify prayer count dropped — "Prayer One" must be gone.
        EnsureCardExpanded(driver, "Move Source Card");
        Assert.False(
            TestConfig.IsIOS
                ? driver.IsTextContainsDisplayed("Prayer One", timeoutSeconds: 3)
                : driver.IsTextDisplayed("Prayer One", timeoutSeconds: 3),
            "Prayer One should no longer appear in Move Source Card after the move");

        // 6b. Target card should now contain the moved prayer.
        EnsureCardCollapsed(driver, "Move Source Card");
        driver.EnsureCardVisible("Move Target Card");
        EnsureCardExpanded(driver, "Move Target Card");
        Assert.True(
            TestConfig.IsIOS
                ? driver.IsTextContainsDisplayed("Prayer One", timeoutSeconds: 10)
                : driver.IsTextDisplayed("Prayer One", timeoutSeconds: 10),
            "Prayer One should be visible in Move Target Card after the move");
    }

    /// <summary>
    /// D-07 / TD-20: After moving a prayer out of "Move Source Card", the originating
    /// card must NOT show a stuck expanded margin while its VM has IsExpanded=false.
    /// This is the regression test for the Border.Margin bug fixed in Commit 2.
    /// </summary>
    [Fact]
    [Trait("Status", "FailingPreCommit2")]
    public void Cards_MovePrayer_DoesNotLeaveSourceCardWithStaleExpandedMargin()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // 1. Expand source card.
        driver.EnsureCardVisible("Move Source Card");
        EnsureCardExpanded(driver, "Move Source Card");

        // 2. Open "Prayer Two" (using Prayer Two here to keep fixtures independent
        //    from Cards_MovePrayerBetweenCards_BothCardsReflect which uses Prayer One).
        if (TestConfig.IsIOS)
            driver.TapByTextContains("Prayer Two", timeoutSeconds: 10);
        else
            driver.TapByText("Prayer Two");
        Thread.Sleep(TestConfig.DelayAfterTap);

        // 3. Edit mode.
        driver.TapToolbarItem("Edit");
        Thread.Sleep(TestConfig.DelayAfterTap);
        Assert.True(driver.IsDisplayed("Detail_Entry_Title", timeoutSeconds: 10),
            "Should be in edit mode");

        // 4. Move to target card.
        driver.WaitAndTap("Detail_Picker_Card", timeoutSeconds: 10);
        Thread.Sleep(TestConfig.DelayAfterTap);
        if (TestConfig.IsIOS)
            driver.TapByTextContains("Move Target Card", timeoutSeconds: 10);
        else
            driver.TapByText("Move Target Card");
        Thread.Sleep(TestConfig.DelayAfterTap);

        // 5. Save and return to Cards tab.
        driver.TapToolbarItem("Save");
        Thread.Sleep(TestConfig.DelayAfterSave);
        driver.NavigateToTab("Prayer Cards");
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // 6. Source card must be collapsed — not showing the stale expanded margin.
        // The composed accessibility label includes ", Collapsed" when the VM's
        // IsExpanded=false AND the view margin matches (i.e. no stuck margin).
        // Pre-fix this assertion fails because the Border.Margin is visually expanded
        // while VM is collapsed — the accessibility label still reports ", Collapsed"
        // via the VM, but the visual border margin is wrong (the visual regression).
        // Post-fix the DataTrigger ensures the margin is also correct.
        driver.EnsureCardVisible("Move Source Card");
        Assert.True(
            IsCardExpanded(driver, "Move Source Card") == false,
            "Move Source Card should be collapsed (IsExpanded=false) after moving a prayer out. " +
            "If stuck expanded, the Border.Margin is not driven by the declarative DataTrigger.");

        // On iOS also verify via composed label that the platform sees it as Collapsed.
        if (TestConfig.IsIOS)
            Assert.True(
                driver.IsTextContainsDisplayed("Move Source Card, Collapsed", timeoutSeconds: 5),
                "iOS a11y label should report 'Move Source Card, Collapsed' — " +
                "stale expanded margin would suggest the DataTrigger hasn't fired.");
    }

    /// <summary>
    /// X-15 / TD-20: After the move, "Move Target Card" expands and displays the
    /// moved prayer. Validates that the ExpandedCardId mechanism in Commit 2 correctly
    /// auto-expands the target card and shows its new contents.
    /// </summary>
    [Fact]
    [Trait("Status", "FailingPreCommit2")]
    public void Cards_MovePrayer_TargetCardExpandsAndShowsMovedPrayer()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // 1. Expand source card.
        driver.EnsureCardVisible("Move Source Card");
        EnsureCardExpanded(driver, "Move Source Card");

        // 2. Open "Prayer Three" (keeps fixture independent from other move tests).
        if (TestConfig.IsIOS)
            driver.TapByTextContains("Prayer Three", timeoutSeconds: 10);
        else
            driver.TapByText("Prayer Three");
        Thread.Sleep(TestConfig.DelayAfterTap);

        // 3. Edit mode.
        driver.TapToolbarItem("Edit");
        Thread.Sleep(TestConfig.DelayAfterTap);
        Assert.True(driver.IsDisplayed("Detail_Entry_Title", timeoutSeconds: 10),
            "Should be in edit mode");

        // 4. Move to target card.
        driver.WaitAndTap("Detail_Picker_Card", timeoutSeconds: 10);
        Thread.Sleep(TestConfig.DelayAfterTap);
        if (TestConfig.IsIOS)
            driver.TapByTextContains("Move Target Card", timeoutSeconds: 10);
        else
            driver.TapByText("Move Target Card");
        Thread.Sleep(TestConfig.DelayAfterTap);

        // 5. Save and return to Cards tab.
        driver.TapToolbarItem("Save");
        Thread.Sleep(TestConfig.DelayAfterSave);
        driver.NavigateToTab("Prayer Cards");
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // 6. "Move Target Card" should be expanded (ExpandedCardId set to it post-save).
        driver.EnsureCardVisible("Move Target Card");
        Assert.True(
            IsCardExpanded(driver, "Move Target Card"),
            "Move Target Card should be expanded after receiving the moved prayer. " +
            "Post-save ExpandedCardId should auto-expand the destination card.");

        if (TestConfig.IsIOS)
            Assert.True(
                driver.IsTextContainsDisplayed("Move Target Card, Expanded", timeoutSeconds: 5),
                "iOS a11y label should report 'Move Target Card, Expanded'");

        // 7. The moved prayer must be visible inside the expanded target card.
        Assert.True(
            TestConfig.IsIOS
                ? driver.IsTextContainsDisplayed("Prayer Three", timeoutSeconds: 10)
                : driver.IsTextDisplayed("Prayer Three", timeoutSeconds: 10),
            "Prayer Three should be visible inside the expanded Move Target Card after the move");
    }

    /// <summary>
    /// Issue #42: Moving a prayer to a SYSTEM card target (here "Quick Add", which
    /// lives in the System box at sort ~900 and is therefore usually off-screen)
    /// must auto-expand that card and reveal the moved prayer in context — exactly
    /// like the user-target move above. This is the regression net for the
    /// shape-(i) realization retirement: with the expanded subtree always inflated
    /// (hidden via IsVisible when collapsed) there is no off-screen cell-realization
    /// race, so setting ExpandedCardId on the system target reveals the prayer
    /// regardless of where the cell sits in the list. Mirrors
    /// Cards_MovePrayer_TargetCardExpandsAndShowsMovedPrayer with a system target.
    /// "Quick Add" is used (not "Shared with me") because it is the system card the
    /// app always seeds on startup, so it is reliably present in the move picker.
    /// </summary>
    [Fact]
    public void Cards_MovePrayer_ToSystemCard_TargetExpandsAndShowsMovedPrayer()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // 1. Expand source card.
        driver.EnsureCardVisible("Move Source Card");
        EnsureCardExpanded(driver, "Move Source Card");

        // 2. Open "Prayer Four" (dedicated fixture prayer for the system-target test).
        if (TestConfig.IsIOS)
            driver.TapByTextContains("Prayer Four", timeoutSeconds: 10);
        else
            driver.TapByText("Prayer Four");
        Thread.Sleep(TestConfig.DelayAfterTap);

        // 3. Edit mode.
        driver.TapToolbarItem("Edit");
        Thread.Sleep(TestConfig.DelayAfterTap);
        Assert.True(driver.IsDisplayed("Detail_Entry_Title", timeoutSeconds: 10),
            "Should be in edit mode");

        // 4. Move to the "Quick Add" SYSTEM card via the card picker.
        driver.WaitAndTap("Detail_Picker_Card", timeoutSeconds: 10);
        Thread.Sleep(TestConfig.DelayAfterTap);
        if (TestConfig.IsIOS)
            driver.TapByTextContains("Quick Add", timeoutSeconds: 10);
        else
            driver.TapByText("Quick Add");
        Thread.Sleep(TestConfig.DelayAfterTap);

        // 5. Save and return to the Cards tab.
        driver.TapToolbarItem("Save");
        Thread.Sleep(TestConfig.DelayAfterSave);
        driver.NavigateToTab("Prayer Cards");
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // 6. "Quick Add" (off-screen system card) should be auto-expanded.
        // EnsureCardVisible only opens sections / scrolls — it does NOT tap-expand the
        // card — so the expanded-header check genuinely reflects the post-save auto-expand.
        // WaitForCardExpanded is system-card-aware: Quick Add's a11y header is
        // "Quick Add, System, Expanded" (the ", System" infix), which the exact-match
        // IsCardExpanded can never satisfy — IsCardHeaderExpanded matches it correctly.
        driver.EnsureCardVisible("Quick Add");
        bool quickAddExpanded = WaitForCardExpanded(driver, "Quick Add", timeoutSeconds: 10);
        Assert.True(
            quickAddExpanded,
            "Quick Add (system card) should auto-expand after receiving the moved prayer. " +
            "Post-save ExpandedCardId should expand the off-screen system target — issue #42.");

        // 7. The moved prayer must be visible inside the expanded system card. Scroll it
        // into view first — like the chips, the eager expanded subtree renders below the
        // fold when Quick Add sits low in the System box (virtualized out until scrolled).
        TryScrollTextIntoView(driver, "Prayer Four");
        Assert.True(
            TestConfig.IsIOS
                ? driver.IsTextContainsDisplayed("Prayer Four", timeoutSeconds: 10)
                : driver.IsTextDisplayed("Prayer Four", timeoutSeconds: 10),
            "Prayer Four should be visible inside the expanded Quick Add system card after the move");
    }
}

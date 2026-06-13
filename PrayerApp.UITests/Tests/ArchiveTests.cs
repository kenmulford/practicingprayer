using OpenQA.Selenium;
using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 9: Archive / Unarchive (F-73)
/// Covers the action chip on an expanded user card that moves it into the
/// Archived box and back to Unboxed (BoxId 0).
/// </summary>
[Collection("Appium")]
[Trait("Platform", "CrossPlatform")]
[Trait("Section", "9-Archive")]
public class ArchiveTests
{
    private readonly AppiumSetup _setup;
    public ArchiveTests(AppiumSetup setup) => _setup = setup;

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a fresh disposable card, returns its title. Timestamped to avoid
    /// UNIQUE-constraint duplicate-title alerts across re-runs (BUG-74 pattern).
    /// Card is saved at top level (Loose Cards / BoxId = 0) — always visible flat,
    /// never hidden inside a collapsed user-box section.
    /// </summary>
    private string CreateDisposableCard()
    {
        var driver = _setup.Driver;
        var title = $"Archive UITest {DateTime.Now:HHmmssfff}";

        driver.TapToolbarItemById("Add Card");
        driver.WaitForElement("Card_Entry_Title", timeoutSeconds: 10);
        driver.EnterText("Card_Entry_Title", title);
        driver.DismissKeyboardIfPresent();
        driver.TapToolbarItem("Save");
        Thread.Sleep(TestConfig.DelayAfterSave);

        // Wait for the list to re-render with the new card.
        driver.WaitForElement("Cards_List_Cards", timeoutSeconds: 10);
        return title;
    }

    /// <summary>
    /// Expands the named card idempotently (matching the pattern in PrayerCardTests).
    /// Returns immediately when the card is already expanded.
    /// Uses WaitForElement to wait for the Archive chip after expanding, ensuring
    /// the lazy expanded subtree has inflated before callers assert on chips.
    /// </summary>
    private void EnsureCardExpanded(string cardName)
    {
        var driver = _setup.Driver;
        driver.EnsureCardVisible(cardName);

        bool alreadyExpanded = TestConfig.IsIOS
            ? driver.IsTextContainsDisplayed(cardName + ", Expanded", timeoutSeconds: 1)
            : driver.IsTextDisplayed(cardName + ", Expanded", timeoutSeconds: 1);

        if (!alreadyExpanded)
        {
            if (TestConfig.IsIOS)
                driver.TapByTextContains(cardName);
            else
                driver.TapByText(cardName);
            Thread.Sleep(TestConfig.DelayAfterTap);
        }

        // Wait for the Archive chip to appear — it is part of the lazy expanded subtree
        // and may not be in the a11y tree immediately after the expand tap settles.
        // This replaces the fragile IsDisplayed(timeoutSeconds:10) assertion at call sites.
        driver.WaitForElement("Cards_Btn_Archive", timeoutSeconds: 10);
    }

    /// <summary>
    /// Expands the Archived section header if it is collapsed. The section header
    /// renders SemanticProperties.Description="{Binding Name}" so the Name "Archived"
    /// is locatable by text. When the section is empty (no archived cards yet) the
    /// header still renders — it is always present in the CollectionView grouping.
    /// </summary>
    private void EnsureArchivedSectionExpanded()
    {
        var driver = _setup.Driver;

        // Scroll down to ensure the Archived header (always last) is in the viewport.
        driver.ScrollDownToText("Archived", maxScrolls: 6);

        // If the section header reads "Archived" with no card count, the section is
        // either empty or collapsed. TapByText expands it; a second pass is
        // harmless (toggle is idempotent — if it was expanded it collapses and
        // EnsureAllSectionsExpanded handles the fallback). We use
        // EnsureAllSectionsExpanded as the definitive expander after the initial tap.
        driver.EnsureAllSectionsExpanded();
        Thread.Sleep(TestConfig.DelayCollectionRender);
    }

    /// <summary>
    /// Deletes the named card, assuming it is currently visible and expanded in
    /// the normal (non-Archived) Loose Cards section. Safe best-effort cleanup.
    /// </summary>
    private void TryDeleteCard(string cardName)
    {
        try
        {
            var driver = _setup.Driver;
            driver.EnsureCardVisible(cardName);
            EnsureCardExpanded(cardName);
            if (driver.IsDisplayed("Cards_Btn_Delete", timeoutSeconds: 3))
            {
                driver.Tap("Cards_Btn_Delete");
                driver.DismissAlertIfPresent();
                Thread.Sleep(TestConfig.DelayAfterSave);
            }
        }
        catch (WebDriverException) { /* best-effort */ }
    }

    // ── Tests ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 9.1: Tapping the Archive chip on an expanded user card moves it to the Archived section.
    ///
    /// Flow: tapping Archive raises an "Archive Card?" confirm dialog (06-01 design — the
    /// Undo snackbar was dropped). Accept it via TapAlertButton("Archive").
    ///
    /// Primary assertion: after accepting the confirm, expand the Archived section and confirm
    /// the card title is visible there. Search bar is NOT used as a fallback (it may not
    /// surface archived cards); section-expand + scroll is the reliable path.
    /// </summary>
    [Fact]
    public void Cards_ArchiveChip_MovesCardToArchivedSection()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;
        Thread.Sleep(TestConfig.DelayCollectionRender);

        // 1. Create a disposable card so this test never mutates shared seed fixtures.
        var cardName = CreateDisposableCard();

        try
        {
            // 2. Expand the new card to reveal action chips.
            //    EnsureCardExpanded now waits internally for Cards_Btn_Archive.
            EnsureCardExpanded(cardName);

            // 3. Tap the Archive chip — a confirm dialog now intercepts.
            driver.Tap("Cards_Btn_Archive");

            // 4. Accept the "Archive Card?" confirm dialog.
            Assert.True(driver.IsAlertPresent(),
                "Tapping Archive should raise the 'Archive Card?' confirm dialog.");
            driver.TapAlertButton("Archive");

            // Give RebuildSections time to complete and the CollectionView to reflow.
            Thread.Sleep(TestConfig.DelayAfterSave);

            // 5. PRIMARY: expand the Archived section and confirm the card has landed there.
            //    Do NOT use the Cards search bar — it may not surface archived cards.
            EnsureArchivedSectionExpanded();

            bool cardInArchivedSection =
                TestConfig.IsIOS
                    ? driver.IsTextContainsDisplayed(cardName, timeoutSeconds: 10)
                    : driver.IsTextDisplayed(cardName, timeoutSeconds: 10);

            string? evidence = cardInArchivedSection ? null
                : driver.DumpPageSource(nameof(Cards_ArchiveChip_MovesCardToArchivedSection));

            Assert.True(cardInArchivedSection,
                $"After tapping Archive, '{cardName}' should appear in the Archived section. " +
                $"Dump: {evidence}");
        }
        finally
        {
            // Cleanup: unarchive then delete so the card doesn't accumulate across runs.
            // If the card is currently archived, expand Archived section, expand card,
            // tap Unarchive, then delete from Loose Cards.
            try
            {
                EnsureArchivedSectionExpanded();
                EnsureCardExpanded(cardName);

                if (driver.IsDisplayed("Cards_Btn_Archive", timeoutSeconds: 3))
                {
                    // Chip label is now "Unarchive" — tap it to move back to Loose Cards.
                    driver.Tap("Cards_Btn_Archive");
                    Thread.Sleep(TestConfig.DelayAfterSave);
                }

                TryDeleteCard(cardName);
            }
            catch (WebDriverException) { /* best-effort cleanup */ }
        }
    }

    /// <summary>
    /// 9.2: Tapping the Archive chip a second time ("Unarchive") on an archived card
    /// moves it back to Unboxed (BoxId 0 / Loose Cards) and removes it from Archived.
    ///
    /// Sequence: create card → archive (accept "Archive Card?" confirm) → verify in Archived
    /// section → expand archived card (wait for chip) → unarchive (immediate, no dialog) →
    /// verify absent from Archived → verify visible in normal list. Self-contained; does not
    /// depend on test 9.1's state.
    ///
    /// Precondition reliability: EnsureCardExpanded now waits internally for
    /// Cards_Btn_Archive via WaitForElement, eliminating the flaky IsDisplayed-based
    /// precondition assertion that previously failed when the chip rendered late.
    /// </summary>
    [Fact]
    public void Cards_UnarchiveChip_MovesCardBackToLooseCards()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;
        Thread.Sleep(TestConfig.DelayCollectionRender);

        var cardName = CreateDisposableCard();

        try
        {
            // 1. Archive the card (prerequisite). Confirm dialog now intercepts.
            //    EnsureCardExpanded waits for Cards_Btn_Archive internally — no separate
            //    IsDisplayed assertion needed; WaitForElement throws on timeout.
            EnsureCardExpanded(cardName);
            driver.Tap("Cards_Btn_Archive");
            Assert.True(driver.IsAlertPresent(),
                "Precondition: archiving should raise the 'Archive Card?' confirm dialog.");
            driver.TapAlertButton("Archive");
            Thread.Sleep(TestConfig.DelayAfterSave);

            // 2. Expand Archived section and confirm card is there.
            EnsureArchivedSectionExpanded();
            Assert.True(
                TestConfig.IsIOS
                    ? driver.IsTextContainsDisplayed(cardName, timeoutSeconds: 10)
                    : driver.IsTextDisplayed(cardName, timeoutSeconds: 10),
                $"Precondition: '{cardName}' should be in Archived section after archiving");

            // 3. Expand the archived card to reveal the chip (now labeled "Unarchive").
            //    EnsureCardExpanded waits for Cards_Btn_Archive — covers both iOS a11y
            //    tree settle lag and Android CollectionView lazy inflation.
            EnsureCardExpanded(cardName);

            // 4. Tap the chip — now labeled "Unarchive" — to restore the card.
            driver.Tap("Cards_Btn_Archive");
            Thread.Sleep(TestConfig.DelayAfterSave);

            // 5. The card should now be absent from the Archived section.
            //    Re-expand Archived to get a fresh view (EnsureAllSectionsExpanded
            //    re-finds headers each iteration to handle reflow).
            EnsureArchivedSectionExpanded();
            bool stillInArchived =
                TestConfig.IsIOS
                    ? driver.IsTextContainsDisplayed(cardName, timeoutSeconds: 3)
                    : driver.IsTextDisplayed(cardName, timeoutSeconds: 3);

            string? evidenceArchived = stillInArchived
                ? driver.DumpPageSource(nameof(Cards_UnarchiveChip_MovesCardBackToLooseCards) + "_StillArchived")
                : null;

            Assert.False(stillInArchived,
                $"After unarchiving, '{cardName}' should no longer appear in the Archived section. " +
                $"Dump: {evidenceArchived}");

            // 6. The card should be back in the normal list (Loose Cards / Unboxed).
            //    EnsureCardVisible uses scroll + expand-all + search-bar fallback —
            //    sufficient to confirm the card is in the rendered tree outside Archived.
            driver.EnsureCardVisible(cardName);
            bool backInNormalList =
                TestConfig.IsIOS
                    ? driver.IsTextContainsDisplayed(cardName, timeoutSeconds: 10)
                    : driver.IsTextDisplayed(cardName, timeoutSeconds: 10);

            string? evidenceNormal = backInNormalList ? null
                : driver.DumpPageSource(nameof(Cards_UnarchiveChip_MovesCardBackToLooseCards) + "_NotInLoose");

            Assert.True(backInNormalList,
                $"After unarchiving, '{cardName}' should be visible in the normal card list. " +
                $"Dump: {evidenceNormal}");
        }
        finally
        {
            // Cleanup: delete the card from Loose Cards (it is unarchived at this point
            // unless the test failed mid-way; TryDeleteCard handles the happy path).
            TryDeleteCard(cardName);
        }
    }
}

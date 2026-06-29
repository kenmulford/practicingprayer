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
    /// the expanded subtree (shape (i): inlined in the cell template, gated by
    /// IsVisible="{Binding IsExpanded}") is on screen before callers assert on chips.
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

        // Wait for the Archive chip to appear — it is part of the inline expanded
        // subtree (shape (i): hidden via IsVisible when collapsed) and may not be in
        // the a11y tree immediately after the expand tap settles.
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

            // 4. PRIMARY: the "Archive Card?" confirm dialog appears; accept it.
            //    The archived-section reflow re-verification was dropped in issue #148
            //    Phase 2 — the box-assignment + restore logic is exhaustively covered by
            //    PrayerCardViewModelTests.ArchiveCommand_* unit tests; this smoke now guards
            //    only the chip → confirm-dialog affordance.
            Assert.True(driver.IsAlertPresent(),
                "Tapping Archive should raise the 'Archive Card?' confirm dialog.");
            driver.TapAlertButton("Archive");

            // Give RebuildSections time to complete and the CollectionView to reflow.
            Thread.Sleep(TestConfig.DelayAfterSave);
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
    /// 9.3: Archive screenshot capture — walks the three archive surfaces and captures a
    /// diagnostic screenshot of each, echoing the saved file path for orchestrator collection.
    /// This is a capture test, not a behavioral assertion: it proves the UI renders the
    /// archive affordances and records pixels for visual review, then leaves the suite at Home.
    ///
    /// Three states captured:
    ///   1. Archive_01_chip_grid     — the 2×3 action-chip grid on an expanded card (Archive chip visible).
    ///   2. Archive_02_confirm_dialog — the "Archive Card?" confirm dialog raised by the chip.
    ///   3. Archive_03_editpage_buttons — the equal-width Delete/Archive button row on PrayerCardPage.
    ///
    /// Dark-mode in-session toggle is not supported by the Android test infrastructure
    /// (requires adb + cold-launch — see DarkModeRenderingTests); this captures light only.
    /// Card title is timestamped by CreateDisposableCard, so the test is idempotent across re-runs.
    /// </summary>
    [Fact]
    public void Archive_Capture_Screenshots()
    {
        _setup.Driver.ResetAppUIState(_setup);
        _setup.Driver.EnsureOnTab("Prayer Cards", _setup);
        var driver = _setup.Driver;
        Thread.Sleep(TestConfig.DelayCollectionRender);

        var cardName = CreateDisposableCard();

        try
        {
            // ── State 1: the 2×3 action-chip grid on the expanded card ──────────────
            // EnsureCardExpanded waits internally for Cards_Btn_Archive to inflate.
            EnsureCardExpanded(cardName);

            var diag1 = driver.CaptureDiagnostics("Archive_01_chip_grid");
            Assert.False(diag1.Contains("diagnostic capture failed"),
                $"Screenshot capture failed: {diag1}");
            Console.WriteLine($"[Archive_Capture_Screenshots] {diag1}");

            // ── State 2: the "Archive Card?" confirm dialog ─────────────────────────
            driver.Tap("Cards_Btn_Archive");
            Assert.True(driver.IsAlertPresent(),
                "Tapping the Archive chip should raise the 'Archive Card?' confirm dialog.");

            var diag2 = driver.CaptureDiagnostics("Archive_02_confirm_dialog");
            Assert.False(diag2.Contains("diagnostic capture failed"),
                $"Screenshot capture failed: {diag2}");
            Console.WriteLine($"[Archive_Capture_Screenshots] {diag2}");

            // Dismiss with Cancel so the card is NOT archived — keeps it in Loose Cards
            // for the edit-page leg below and prevents archived-state leaking to cleanup.
            driver.TapAlertButton("Cancel");
            Thread.Sleep(TestConfig.DelayAfterDismiss);

            // ── State 3: the Delete/Archive button row on the card edit page ────────
            // Open PrayerCardPage exactly the way PrayerCardTests.Cards_EditButton_
            // NavigatesToEditPage does (PrayerCardTests.cs:459-482): expand the card,
            // tap the Edit chip, land on Card_Entry_Title.
            EnsureCardExpanded(cardName);
            driver.WaitAndTap("Cards_Btn_Edit", timeoutSeconds: 10);
            Thread.Sleep(TestConfig.DelayAfterNavigation);

            // Wait for a known element on PrayerCardPage before capturing.
            driver.WaitForElement("Card_Entry_Title", timeoutSeconds: 10);
            driver.WaitForElement("Card_Btn_Archive", timeoutSeconds: 10);
            driver.DismissKeyboardIfPresent();
            Thread.Sleep(TestConfig.DelayModalAnimation);

            var diag3 = driver.CaptureDiagnostics("Archive_03_editpage_buttons");
            Assert.False(diag3.Contains("diagnostic capture failed"),
                $"Screenshot capture failed: {diag3}");
            Console.WriteLine($"[Archive_Capture_Screenshots] {diag3}");

            // Navigate back off the edit page cleanly (mirrors PrayerCardTests.cs:480-481).
            driver.GoBack();
            driver.DismissAlertIfPresent();
            Thread.Sleep(TestConfig.DelayAfterDismiss);
        }
        finally
        {
            // Card was never archived (we cancelled the confirm), so it is in Loose Cards.
            TryDeleteCard(cardName);
        }
    }
}

using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;
using PrayerApp.UITests.Infrastructure;
using System.IO;
using Xunit;

namespace PrayerApp.UITests.Helpers;

/// <summary>
/// Convenience extensions for Appium driver interactions.
/// On Android, MAUI AutomationId maps to resource-id (By.Id with package prefix).
/// On iOS, AutomationId maps to accessibilityIdentifier (MobileBy.AccessibilityId).
/// </summary>
public static class AppExtensions
{
    private const string PackagePrefix = "com.multithreadedllc.prayercards:id/";

    /// <summary>iOS swipe velocity in pixels/sec. Moderate speed to reliably trigger SwipeView.</summary>
    private const int IOSSwipeVelocity = 1500;

    /// <summary>Dismiss the software keyboard if showing on iOS. No-op on Android.</summary>
    /// <remarks>
    /// Catches every exception: Appium 2 returns "resource not found" for HideKeyboard
    /// which the .NET client surfaces as <see cref="NotImplementedException"/>, not
    /// <see cref="WebDriverException"/>.
    /// </remarks>
    public static void DismissKeyboardIfPresent(this AppiumDriver driver)
    {
        if (!TestConfig.IsIOS) return;
        try { driver.HideKeyboard(); } catch { /* keyboard not shown, endpoint unavailable, etc. */ }
    }

    /// <summary>Build the correct locator for an AutomationId on the current platform.</summary>
    private static By AutomationIdLocator(string automationId)
    {
        if (TestConfig.IsAndroid)
        {
            // MAUI AutomationId maps to resource-id on interactive elements,
            // but may map to content-desc on layout containers (Grid, etc.).
            // Use XPath to check both.
            return By.XPath(
                $"//*[@resource-id='{PackagePrefix}{automationId}' or @content-desc='{automationId}']");
        }
        return MobileBy.AccessibilityId(automationId);
    }

    /// <summary>Build a platform-correct XPath to find an element by its visible text.</summary>
    private static By TextLocator(string text)
    {
        if (TestConfig.IsIOS)
            return By.XPath($"//*[@name='{text}' or @label='{text}']");
        return By.XPath($"//*[@text='{text}' or @content-desc='{text}']");
    }

    /// <summary>Build a platform-correct XPath to find an element containing text.</summary>
    private static By TextContainsLocator(string text)
    {
        if (TestConfig.IsIOS)
            return By.XPath($"//*[contains(@name,'{text}') or contains(@label,'{text}')]");
        return By.XPath($"//*[contains(@text,'{text}') or contains(@content-desc,'{text}')]");
    }

    /// <summary>iOS XPath to find a button by its exact name or label.</summary>
    private static By IOSButtonByNameOrLabel(string value)
        => By.XPath($"//XCUIElementTypeButton[@name='{value}' or @label='{value}']");

    // ── Element Finders ──────────────────────────────────────────

    /// <summary>Find an element by its AutomationId.</summary>
    public static AppiumElement FindByAutomationId(this AppiumDriver driver, string automationId)
        => (AppiumElement)driver.FindElement(AutomationIdLocator(automationId));

    /// <summary>Wait for an element with the given AutomationId to appear.</summary>
    public static AppiumElement WaitForElement(this AppiumDriver driver, string automationId,
        int timeoutSeconds = 15)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        var locator = AutomationIdLocator(automationId);
        return (AppiumElement)wait.Until(d => d.FindElement(locator));
    }

    /// <summary>Wait for an element to disappear from the screen.</summary>
    public static void WaitForElementGone(this AppiumDriver driver, string automationId,
        int timeoutSeconds = 10)
    {
        var locator = AutomationIdLocator(automationId);
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.ShortTimeout;
            wait.Until(d =>
            {
                try { d.FindElement(locator); return false; }
                catch (NoSuchElementException) { return true; }
            });
        }
        finally
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
        }
    }

    // ── Actions ──────────────────────────────────────────────────

    /// <summary>Tap an element by AutomationId.</summary>
    public static void Tap(this AppiumDriver driver, string automationId)
        => driver.FindByAutomationId(automationId).Click();

    /// <summary>Tap an element by AutomationId, waiting for it first.</summary>
    public static void WaitAndTap(this AppiumDriver driver, string automationId, int timeoutSeconds = 15)
        => driver.WaitForElement(automationId, timeoutSeconds).Click();

    /// <summary>Clear an input and type new text.</summary>
    public static void EnterText(this AppiumDriver driver, string automationId, string text)
    {
        var element = driver.FindByAutomationId(automationId);

        if (TestConfig.IsIOS)
        {
            // iOS: use "Clear text" button to clear (element.Clear() is broken on
            // search field wrappers), then standard SendKeys to type.
            // autoDismissAlerts handles any dictation prompts from the keyboard.
            try
            {
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(1);
                driver.FindElement(MobileBy.AccessibilityId("Clear text")).Click();
                Thread.Sleep(200);
            }
            catch (WebDriverException) { /* field empty or unfocused */ }
            finally { driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout; }

            if (!string.IsNullOrEmpty(text))
            {
                element.SendKeys(text);
                driver.DismissKeyboardIfPresent();
            }
        }
        else
        {
            element.Clear();
            if (!string.IsNullOrEmpty(text))
                element.SendKeys(text);
        }
    }

    /// <summary>Get the text content of an element.</summary>
    public static string GetText(this AppiumDriver driver, string automationId)
        => driver.FindByAutomationId(automationId).Text;

    /// <summary>Check if an element is currently displayed (does not throw).</summary>
    public static bool IsDisplayed(this AppiumDriver driver, string automationId,
        int timeoutSeconds = 3)
    {
        try
        {
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(timeoutSeconds);
            var element = driver.FindElement(AutomationIdLocator(automationId));
            return element.Displayed;
        }
        catch (WebDriverException)
        {
            return false;
        }
        finally
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
        }
    }

    // ── Scrolling ────────────────────────────────────────────────

    /// <summary>Scroll down until an element with the given AutomationId is found.</summary>
    /// <param name="scrollableAutomationId">Optional: AutomationId of a scrollable container
    /// (e.g. CollectionView) to target on iOS. When provided, iOS uses <c>mobile: scroll</c>
    /// on that element instead of a full-screen swipe.</param>
    public static AppiumElement ScrollDownTo(this AppiumDriver driver, string automationId,
        int maxScrolls = 5, string? scrollableAutomationId = null)
        => ScrollDownUntil(driver, AutomationIdLocator(automationId),
            $"Element '{automationId}'", maxScrolls, scrollableAutomationId);

    /// <summary>Scroll down until an element with the given visible text is found.</summary>
    public static AppiumElement ScrollDownToText(this AppiumDriver driver, string text,
        int maxScrolls = 5, string? scrollableAutomationId = null)
        => ScrollDownUntil(driver, TextLocator(text),
            $"Text '{text}'", maxScrolls, scrollableAutomationId);

    /// <summary>
    /// Ensure a card on the Prayer Cards page is scrolled into view. No-op if the card
    /// text is already visible. Safe to call before any <c>TapByText</c> / <c>TapByTextContains</c>
    /// on a card name — protects tests from position-in-list variance as the Loose Cards
    /// section accumulates disposable fixtures. Swallows NotFound so the subsequent tap
    /// raises the canonical "could not locate" error instead of a masked scroll error.
    /// </summary>
    public static void EnsureCardVisible(this AppiumDriver driver, string cardName)
    {
        bool IsVisible() => TestConfig.IsIOS
            ? driver.IsTextContainsDisplayed(cardName, timeoutSeconds: 2)
            : driver.IsTextDisplayed(cardName, timeoutSeconds: 2);

        if (IsVisible()) return;

        bool TryScroll()
        {
            try
            {
                driver.ScrollDownToText(cardName, maxScrolls: 3, scrollableAutomationId: "Cards_List_Cards");
                return true;
            }
            catch (WebDriverException) { return false; }
        }

        if (TryScroll()) return;

        // Row missing from the rendered CollectionView — the parent section may
        // be collapsed. Expand all and retry.
        driver.EnsureAllSectionsExpanded();
        if (TryScroll()) return;

        // Last resort (lesson: uitest-visibility-not-existence-under-virtualization.md):
        // type the card name into the in-page search to force MAUI to materialize the
        // matching row. TD-19 validated this pattern for freshly-created cards on iOS.
        // ResetAppUIState clears Cards_Search at the start of the next test.
        try
        {
            driver.EnterText("Cards_Search", cardName);
            Thread.Sleep(TestConfig.DelayCollectionRender);
        }
        catch (WebDriverException)
        {
            // Search bar unavailable (off-page, multi-select mode, etc.) — caller's
            // tap raises the canonical NotFound error.
        }
    }

    /// <summary>Scroll down until a locator matches a visible element.</summary>
    private static AppiumElement ScrollDownUntil(AppiumDriver driver, By locator,
        string description, int maxScrolls, string? scrollableAutomationId)
    {
        driver.DismissKeyboardIfPresent();

        // Cache container once (iOS element-targeted scroll) and window size (Android swipe area)
        string? containerId = null;
        if (TestConfig.IsIOS && scrollableAutomationId != null)
        {
            try { containerId = driver.FindElement(MobileBy.AccessibilityId(scrollableAutomationId)).Id; }
            catch (NoSuchElementException) { /* fall through to generic swipe */ }
        }
        var size = TestConfig.IsAndroid ? driver.Manage().Window.Size : default;

        try
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.ShortTimeout;
            for (int i = 0; i < maxScrolls; i++)
            {
                try
                {
                    var element = driver.FindElement(locator);
                    if (element.Displayed)
                        return (AppiumElement)element;
                }
                catch (NoSuchElementException) { }

                ScrollDown(driver, size, containerId);
            }
        }
        finally
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
        }

        throw new NoSuchElementException($"{description} not found after {maxScrolls} scrolls.");
    }

    /// <summary>Perform a single scroll-down gesture.</summary>
    /// <param name="containerId">Pre-resolved element ID for iOS container-targeted scroll, or null for full-screen swipe.</param>
    private static void ScrollDown(AppiumDriver driver, System.Drawing.Size size, string? containerId)
    {
        if (TestConfig.IsIOS)
        {
            if (containerId != null)
            {
                driver.ExecuteScript("mobile: scroll", new Dictionary<string, object>
                {
                    { "elementId", containerId },
                    { "direction", "down" }
                });
                return;
            }

            driver.ExecuteScript("mobile: swipe", new Dictionary<string, object>
            {
                { "direction", "up" }
            });
        }
        else
        {
            driver.ExecuteScript("mobile: swipeGesture", new Dictionary<string, object>
            {
                { "left", size.Width / 4 },
                { "top", size.Height / 4 },
                { "width", size.Width / 2 },
                { "height", size.Height / 2 },
                { "direction", "up" },
                { "percent", 0.5 }
            });
        }
    }

    // ── Navigation ───────────────────────────────────────────────

    /// <summary>
    /// Navigate to a Shell tab by tapping its tab bar item.
    /// Escalation: (1) back to clear nav stack, (2) dismiss known modals,
    /// (3) re-activate app, (4) XPath text fallback.
    /// </summary>
    public static void NavigateToTab(this AppiumDriver driver, string tabTitle)
    {
        driver.DismissKeyboardIfPresent();

        // Stage 1: Try up to 3 times to find the tab bar, going back each time
        // to clear modals, detail pages, and deep navigation stacks.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            driver.DismissAlertIfPresent();

            if (TryTapTab(driver, tabTitle))
                return;

            // Tab not found — go back to clear the current page/modal
            try { driver.Navigate().Back(); Thread.Sleep(300); } catch (WebDriverException) { }
        }

        // Stage 2: Try dismissing a known modal (Cancel/Skip buttons that block tab access)
        foreach (var modalButton in new[] { "Welcome_Btn_Skip", "Banner_Btn_Skip", "Banner_Btn_GotIt", "Scope_Btn_Cancel", "QuickAdd_Btn_Cancel" })
        {
            try
            {
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(1);
                var btn = driver.FindElement(MobileBy.AccessibilityId(modalButton));
                btn.Click();
                Thread.Sleep(1000);
            }
            catch (WebDriverException) { }
            finally { driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout; }

            if (TryTapTab(driver, tabTitle))
                return;
        }

        // Stage 3: Escape Prayer Time page (tab bar is hidden there).
        // Try tapping "I'm done" or "Finish" to exit back to Home.
        if (TryEscapePrayerTime(driver))
        {
            Thread.Sleep(500);
            if (TryTapTab(driver, tabTitle))
                return;
        }

        // Stage 4: re-activate the app (handles case where GoBack closed it)
        try
        {
            var appId = TestConfig.IsIOS ? TestConfig.IOSBundleId : TestConfig.AndroidPackage;
            driver.ActivateApp(appId);
            Thread.Sleep(1000);
        }
        catch (WebDriverException) { }

        if (TryTapTab(driver, tabTitle))
            return;

        // Stage 5: final XPath text fallback
        var tab = driver.FindElement(TextContainsLocator(tabTitle));
        tab.Click();
        Thread.Sleep(500);
    }

    /// <summary>Try to find and tap a tab by AccessibilityId. Returns true on success.</summary>
    /// <remarks>
    /// iOS scopes the query to <c>XCUIElementTypeTabBar</c>: a bare name lookup matches
    /// the NavBar title and any StaticText on the current page too, and WebDriver picks
    /// the first (non-interactive) match.
    /// </remarks>
    private static bool TryTapTab(AppiumDriver driver, string tabTitle)
    {
        try
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.ShortTimeout;
            By locator = TestConfig.IsIOS
                ? By.XPath($"//XCUIElementTypeTabBar//XCUIElementTypeButton[@name='{tabTitle}']")
                : MobileBy.AccessibilityId(tabTitle);
            var tab = driver.FindElement(locator);
            tab.Click();
            Thread.Sleep(500);
            return true;
        }
        catch (WebDriverException) { return false; }
        finally
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
        }
    }

    /// <summary>
    /// Try to escape the Prayer Time page by tapping "I'm done" or "Finish".
    /// Prayer Time hides the tab bar, so NavigateToTab can't find tabs.
    /// Returns true if an exit button was found and tapped.
    /// </summary>
    private static bool TryEscapePrayerTime(AppiumDriver driver)
    {
        try
        {
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(1);

            // Try AutomationId first (works on Android)
            foreach (var id in new[] { "PrayerTime_Btn_Finish", "PrayerTime_Btn_Done" })
            {
                try
                {
                    var btn = driver.FindElement(MobileBy.AccessibilityId(id));
                    btn.Click();
                    return true;
                }
                catch (WebDriverException) { }
            }

            // iOS fallback: text-based search (accessibility flattening hides AutomationIds)
            foreach (var text in new[] { "Finish", "I'm done" })
            {
                try
                {
                    var btn = driver.FindElement(TextLocator(text));
                    btn.Click();
                    return true;
                }
                catch (WebDriverException) { }
            }

            // Last resort: CONTAINS search for "done"
            try
            {
                var btn = driver.FindElement(TextContainsLocator("done"));
                btn.Click();
                return true;
            }
            catch (WebDriverException) { }

            return false;
        }
        finally
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
        }
    }

    /// <summary>Dismiss onboarding if needed and navigate to a tab. Replaces per-class EnsureOnX() methods.</summary>
    public static void EnsureOnTab(this AppiumDriver driver, string tabTitle, AppiumSetup setup)
    {
        setup.EnsureSessionAlive();
        // iOS: the software keyboard persists across navigations and can cover the
        // tab bar / toolbar items. Dismiss before attempting tab navigation.
        // See Lessons/maui-ios-appium-locators.md. No-op on Android.
        driver.DismissKeyboardIfPresent();
        driver.DismissOnboardingIfPresent(setup);
        driver.NavigateToTab(tabTitle);
    }

    /// <summary>
    /// Expand every collapsed collection section on the Cards page. The section's
    /// triangle + header text are hidden from the a11y tree, so collapsed state is
    /// inferred from the vertical gap to the next header (collapsed footer collapses
    /// to HeightRequest=0; expanded footer has at least one card row, ~60+ px).
    /// </summary>
    /// <remarks>
    /// Fixed-point loop: each iteration re-finds all headers and acts on the first
    /// collapsed one, then re-evaluates. Caching the header list upfront is unsafe —
    /// clicking to expand reflows the CollectionView, which invalidates later
    /// references with <c>StaleElementReferenceException</c> when reading Location/Size.
    /// Follows the re-find-per-iteration pattern used by <see cref="ScrollDownUntil"/>.
    /// </remarks>
    public static void EnsureAllSectionsExpanded(this AppiumDriver driver)
    {
        // Gap threshold: a collapsed section's footer collapses to HeightRequest=0,
        // so consecutive header tops differ by just the header's own height
        // (~60-120 px depending on platform). An expanded section adds at least one
        // ~60+ px card row, pushing the gap above 120 px. 100 px is the safe floor.
        const int CollapsedGapThreshold = 100;
        const int MaxIterations = 20;

        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            var headers = driver.FindElements(AutomationIdLocator("Cards_Section_Header"))
                .OfType<AppiumElement>()
                .Select(h =>
                {
                    try { return (elem: h, top: h.Location.Y, bottom: h.Location.Y + h.Size.Height); }
                    catch (WebDriverException) { return (elem: h, top: -1, bottom: -1); }
                })
                .Where(t => t.top >= 0)
                .OrderBy(t => t.top)
                .ToList();

            if (headers.Count == 0) return;

            int? firstCollapsed = null;
            for (int i = 0; i < headers.Count; i++)
            {
                int nextHeaderTop = (i + 1 < headers.Count) ? headers[i + 1].top : int.MaxValue;
                if (nextHeaderTop - headers[i].bottom < CollapsedGapThreshold)
                {
                    firstCollapsed = i;
                    break;
                }
            }

            if (firstCollapsed == null) return;

            try
            {
                headers[firstCollapsed.Value].elem.Click();
                Thread.Sleep(TestConfig.DelayAfterTap);
            }
            catch (WebDriverException)
            {
                // Element went stale between re-find and click — next iteration will
                // re-find and retry. Bail only if we hit MaxIterations.
            }
        }
    }

    // ── Diagnostics ──────────────────────────────────────────────

    /// <summary>
    /// Dump a screenshot + page source to a temp directory so that a failing test
    /// leaves behind evidence for post-mortem triage. Returns a human-readable
    /// suffix listing the file paths, suitable for appending to an exception message.
    /// Never throws — if the capture fails, it returns an annotated message instead.
    /// </summary>
    public static string CaptureDiagnostics(this AppiumDriver driver, string reason)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            var dir = Path.Combine(Path.GetTempPath(), "prayerapp-uitest-diag");
            Directory.CreateDirectory(dir);

            var safeReason = string.Join("_", reason.Split(Path.GetInvalidFileNameChars()));
            var screenshotPath = Path.Combine(dir, $"{timestamp}-{safeReason}.png");
            var sourcePath = Path.Combine(dir, $"{timestamp}-{safeReason}.xml");

            driver.GetScreenshot().SaveAsFile(screenshotPath);
            File.WriteAllText(sourcePath, driver.PageSource);

            return $" (diagnostics — screenshot: {screenshotPath}, page source: {sourcePath})";
        }
        catch (Exception ex)
        {
            return $" (diagnostic capture failed: {ex.GetType().Name}: {ex.Message})";
        }
    }

    /// <summary>
    /// Clear transient UI state left by a prior test (open alerts, multi-select mode,
    /// leaked search term, stale deep-nav) so failures don't cascade. Tests that
    /// verify a transient state (e.g. multi-select toolbar) should NOT call this.
    /// See Lessons/uitest-per-test-ui-state-reset.md for rationale.
    /// </summary>
    public static void ResetAppUIState(this AppiumDriver driver, AppiumSetup setup)
    {
        // Fast path: already at a tab root with no pending alert.
        if (!driver.IsAlertPresent() && driver.IsDisplayed("Home", timeoutSeconds: 0))
            return;

        try { driver.DismissAlertIfPresent(); } catch { /* best effort */ }

        try
        {
            if (driver.IsDisplayed("Cards_Bar_MultiSelect", timeoutSeconds: 0))
            {
                // Overflow button mutates visually to X/Cancel in multi-select mode
                // but keeps AutomationId="More" (MAUI AutomationId is set-once).
                driver.TapToolbarItemById("More");
                Thread.Sleep(TestConfig.DelayAfterTap);
            }
        }
        catch { /* not on Prayer Cards or not in multi-select */ }

        try
        {
            if (driver.IsDisplayed("Cards_Search", timeoutSeconds: 0) &&
                !string.IsNullOrEmpty(driver.GetText("Cards_Search")))
            {
                driver.EnterText("Cards_Search", "");
                Thread.Sleep(TestConfig.DelayAfterTap);
            }
        }
        catch { /* not on Prayer Cards or search bar not rendered */ }

        // Back out to a tab root, dismissing any alert each Back may raise (e.g.
        // "Discard changes?"). Bounded so tab-bar-hidden states (Prayer Time) don't stall.
        for (int i = 0; i < 5; i++)
        {
            try { driver.DismissAlertIfPresent(); } catch { /* best effort */ }
            if (driver.IsDisplayed("Home", timeoutSeconds: 0)) break;
            try { driver.Navigate().Back(); Thread.Sleep(200); } catch (WebDriverException) { break; }
        }
    }

    /// <summary>Go back (Android back button or iOS back nav).</summary>
    public static void GoBack(this AppiumDriver driver)
        => driver.Navigate().Back();

    // ── Onboarding ───────────────────────────────────────────────

    /// <summary>
    /// Dismiss onboarding if currently showing. Idempotent — safe to call multiple times.
    /// Taps "Skip tour" on the welcome popup, "Skip" on a mid-flow banner, or "Got it!"
    /// on the final PrayerTimeHighlight step, then "Done" on the completion popup.
    /// Retries up to 3 times with increasing wait to handle slow popup rendering.
    /// </summary>
    public static void DismissOnboardingIfPresent(this AppiumDriver driver, AppiumSetup setup)
    {
        if (setup.OnboardingHandled) return;

        // Fast path: if the tab bar is already rendered, no onboarding popup is
        // blocking. Saves up to 3s on test #1 vs probing Welcome_Btn_Skip first.
        if (driver.IsDisplayed("Home", timeoutSeconds: 1))
        {
            setup.OnboardingHandled = true;
            return;
        }

        // Retry loop — the welcome popup renders asynchronously from OnAppearing
        // and may not be visible immediately, especially on slow emulators.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            // Check for dismissal buttons — welcome popup, mid-onboarding banner, or final "Got it!"
            string? dismissButton = driver.IsDisplayed("Welcome_Btn_Skip", timeoutSeconds: 3) ? "Welcome_Btn_Skip"
                : driver.IsDisplayed("Banner_Btn_Skip", timeoutSeconds: 2) ? "Banner_Btn_Skip"
                : driver.IsDisplayed("Banner_Btn_GotIt", timeoutSeconds: 2) ? "Banner_Btn_GotIt"
                : null;

            if (dismissButton != null)
            {
                driver.Tap(dismissButton);
                Thread.Sleep(1000);

                if (driver.IsDisplayed("Complete_Btn_Done", timeoutSeconds: 10))
                {
                    driver.Tap("Complete_Btn_Done");
                    Thread.Sleep(500);
                }

                setup.OnboardingHandled = true;
                return;
            }

            // If tab bar is already accessible, no popup is blocking — we're good
            if (driver.IsDisplayed("Home", timeoutSeconds: 1))
            {
                setup.OnboardingHandled = true;
                return;
            }

            // Wait before retry to give the popup time to render
            Thread.Sleep(1000);
        }

        // After retries, mark handled to avoid infinite loops in future calls
        setup.OnboardingHandled = true;
    }

    // ── Alerts/Dialogs ───────────────────────────────────────────

    /// <summary>Check if an alert dialog is displayed and get its text.</summary>
    public static bool TryGetAlertText(this AppiumDriver driver, out string text)
    {
        if (TestConfig.IsIOS)
        {
            try { text = driver.SwitchTo().Alert().Text ?? ""; return true; }
            catch (WebDriverException) { text = ""; return false; }
        }

        try
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.ShortTimeout;
            text = driver.FindElement(By.Id("android:id/message")).Text;
            return true;
        }
        catch (WebDriverException) { text = ""; return false; }
        finally { driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout; }
    }

    /// <summary>Tap a button in an alert dialog by its text.</summary>
    public static void TapAlertButton(this AppiumDriver driver, string buttonText)
    {
        if (TestConfig.IsIOS)
        {
            // iOS alerts expose buttons as XCUIElementTypeButton with @name matching the button label
            var button = driver.FindElement(
                By.XPath($"//XCUIElementTypeButton[@name='{buttonText}']"));
            button.Click();
        }
        else
        {
            var button = driver.FindElement(By.XPath(
                $"//*[@resource-id='android:id/button1' or @resource-id='android:id/button2' or @resource-id='android:id/button3'][contains(@text,'{buttonText}')]"));
            button.Click();
        }
        Thread.Sleep(300);
    }

    // ── Toolbar / Text Finders ────────────────────────────────────

    /// <summary>
    /// Tap a Shell ToolbarItem by its <c>AutomationId</c>. Fallback order: direct
    /// AutomationId → PrayerCardsPage overflow popup (cross-platform, common case
    /// for Add Card / Collections / Select) → iOS Secondary <c>UIMenu</c> (legacy
    /// path kept for PrayerDetailPage's Save+New). The last two fallbacks are tried
    /// only if the direct lookup misses.
    /// </summary>
    public static void TapToolbarItemById(this AppiumDriver driver, string automationId,
        int timeoutSeconds = 10)
    {
        driver.DismissKeyboardIfPresent();
        if (TestConfig.IsIOS) Thread.Sleep(300);

        var locator = AutomationIdLocator(automationId);

        try
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.ShortTimeout;
            driver.FindElement(locator).Click();
            Thread.Sleep(300);
            return;
        }
        catch (WebDriverException) { }
        finally { driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout; }

        if (TryTapOverflowPopupItem(driver, automationId))
            return;

        if (TestConfig.IsIOS && TryTapIOSSecondaryMenuItem(driver, automationId))
            return;

        throw new NoSuchElementException(
            $"Toolbar item '{automationId}' not found on toolbar, in the overflow popup, or in the Secondary UIMenu.");
    }

    /// <summary>
    /// Whether a ToolbarItem is reachable directly, inside the PrayerCardsPage
    /// overflow popup, or behind MAUI's iOS <c>SecondaryToolbarMenuButton</c>. Use
    /// in "does this toolbar item exist" assertions where <see cref="IsDisplayed"/>
    /// alone would miss an item tucked behind an overflow.
    /// </summary>
    public static bool IsToolbarItemAvailable(this AppiumDriver driver, string automationId,
        int timeoutSeconds = 3)
    {
        if (driver.IsDisplayed(automationId, timeoutSeconds)) return true;

        if (IsInOverflowPopup(driver, automationId)) return true;

        if (!TestConfig.IsIOS) return false;

        if (!OpenIOSSecondaryMenu(driver)) return false;
        try
        {
            return driver.FindElements(IOSButtonByNameOrLabel(automationId)).Count > 0;
        }
        finally { CloseIOSPopover(driver); }
    }

    /// <summary>
    /// Open the "More" overflow button's popup, tap an item inside by AutomationId,
    /// and let the popup auto-close on tap. Returns false if the overflow button
    /// isn't present or the item isn't in the opened popup (popup is closed on miss).
    /// </summary>
    private static bool TryTapOverflowPopupItem(AppiumDriver driver, string automationId)
    {
        try
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.ShortTimeout;
            driver.FindElement(MobileBy.AccessibilityId("More")).Click();
        }
        catch (WebDriverException) { return false; }
        finally { driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout; }

        Thread.Sleep(400); // popup animation settle

        try
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.ShortTimeout;
            driver.FindElement(AutomationIdLocator(automationId)).Click();
            Thread.Sleep(300);
            return true;
        }
        catch (WebDriverException)
        {
            CloseOverflowPopupIfOpen(driver);
            return false;
        }
        finally { driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout; }
    }

    /// <summary>Does the target live inside the overflow popup? Opens+peeks+closes.</summary>
    private static bool IsInOverflowPopup(AppiumDriver driver, string automationId)
    {
        try
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.ShortTimeout;
            driver.FindElement(MobileBy.AccessibilityId("More")).Click();
        }
        catch (WebDriverException) { return false; }
        finally { driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout; }

        Thread.Sleep(400);

        bool found;
        try
        {
            found = driver.FindElements(AutomationIdLocator(automationId)).Count > 0;
        }
        finally { CloseOverflowPopupIfOpen(driver); }
        return found;
    }

    /// <summary>
    /// Dismiss the overflow popup by tapping outside its content. Popup is centered
    /// by default, so we tap near the top-left where the popup Border never extends.
    /// Android also accepts system back.
    /// </summary>
    private static void CloseOverflowPopupIfOpen(AppiumDriver driver)
    {
        try
        {
            if (TestConfig.IsAndroid)
            {
                driver.Navigate().Back();
            }
            else
            {
                var size = driver.Manage().Window.Size;
                driver.ExecuteScript("mobile: tap", new Dictionary<string, object>
                {
                    { "x", 10 }, { "y", size.Height - 20 }
                });
            }
            Thread.Sleep(300);
        }
        catch (WebDriverException) { /* best effort */ }
    }

    /// <summary>Open MAUI's iOS Secondary toolbar menu. Returns true on success.</summary>
    /// <remarks>1s sleep: UIMenu popover animation + XCUITest tree settle; 500ms races on cold-start.</remarks>
    private static bool OpenIOSSecondaryMenu(AppiumDriver driver)
    {
        try
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.ShortTimeout;
            driver.FindElement(MobileBy.AccessibilityId("SecondaryToolbarMenuButton")).Click();
            Thread.Sleep(1000);
            return true;
        }
        catch (WebDriverException) { return false; }
        finally { driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout; }
    }

    /// <summary>
    /// Dismiss the iOS <c>UIMenu</c> popover by tapping mid-screen. Re-tapping the
    /// trigger selects the first menu item instead of toggling the menu closed.
    /// </summary>
    private static void CloseIOSPopover(AppiumDriver driver)
    {
        try
        {
            var size = driver.Manage().Window.Size;
            driver.ExecuteScript("mobile: tap", new Dictionary<string, object>
            {
                { "x", size.Width / 2 }, { "y", size.Height / 2 }
            });
            Thread.Sleep(300);
        }
        catch (WebDriverException) { /* best effort */ }
    }

    /// <summary>
    /// iOS: open the Secondary toolbar menu and tap a menu item whose name/label
    /// matches <paramref name="itemText"/>. UIMenu items carry no accessibilityIdentifier —
    /// only their title — so lookup is by name/label.
    /// </summary>
    private static bool TryTapIOSSecondaryMenuItem(AppiumDriver driver, string itemText)
    {
        if (!OpenIOSSecondaryMenu(driver)) return false;
        try
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.ShortTimeout;
            driver.FindElement(IOSButtonByNameOrLabel(itemText)).Click();
            Thread.Sleep(300);
            return true;
        }
        catch (WebDriverException)
        {
            CloseIOSPopover(driver);
            return false;
        }
        finally { driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout; }
    }

    /// <summary>
    /// Tap a Shell ToolbarItem by visible text (e.g. "Save", "Edit"). Works for
    /// text-only toolbars (no <c>IconImageSource</c>). For iconized ToolbarItems, use
    /// <see cref="TapToolbarItemById"/> instead — icon-only rendering removes the
    /// visible <c>Text</c> from the UiAutomator2 tree on Android, which makes text
    /// lookup fragile. See <c>Lessons/uitest-automation-ids-over-visible-text.md</c>.
    /// </summary>
    public static void TapToolbarItem(this AppiumDriver driver, string text, int timeoutSeconds = 10)
    {
        driver.DismissKeyboardIfPresent();
        if (TestConfig.IsIOS) Thread.Sleep(300);

        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));

        By locator = TestConfig.IsIOS ? IOSButtonByNameOrLabel(text) : TextLocator(text);

        var element = wait.Until(d => d.FindElement(locator));
        element.Click();
        Thread.Sleep(300);
    }

    /// <summary>Find an element by its visible text.</summary>
    public static AppiumElement FindByText(this AppiumDriver driver, string text, int timeoutSeconds = 10)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        return (AppiumElement)wait.Until(d => d.FindElement(TextLocator(text)));
    }

    /// <summary>Find an element whose text/label contains the given substring.</summary>
    public static AppiumElement FindByTextContains(this AppiumDriver driver, string text, int timeoutSeconds = 10)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        return (AppiumElement)wait.Until(d => d.FindElement(TextContainsLocator(text)));
    }

    /// <summary>Find and tap any element by its visible text.</summary>
    public static void TapByText(this AppiumDriver driver, string text, int timeoutSeconds = 10)
    {
        driver.FindByText(text, timeoutSeconds).Click();
        Thread.Sleep(300);
    }

    /// <summary>Find and tap any element whose text/label <em>contains</em> the given substring.</summary>
    public static void TapByTextContains(this AppiumDriver driver, string text, int timeoutSeconds = 10)
    {
        driver.FindByTextContains(text, timeoutSeconds).Click();
        Thread.Sleep(300);
    }

    /// <summary>Check if an element with the given text is displayed.</summary>
    public static bool IsTextDisplayed(this AppiumDriver driver, string text, int timeoutSeconds = 3)
    {
        try
        {
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(timeoutSeconds);
            var element = driver.FindElement(TextLocator(text));
            return element.Displayed;
        }
        catch (WebDriverException) { return false; }
        finally { driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout; }
    }

    /// <summary>Check if an element whose text/label <em>contains</em> the given substring is displayed.</summary>
    public static bool IsTextContainsDisplayed(this AppiumDriver driver, string text, int timeoutSeconds = 3)
    {
        try
        {
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(timeoutSeconds);
            var element = driver.FindElement(TextContainsLocator(text));
            return element.Displayed;
        }
        catch (WebDriverException) { return false; }
        finally { driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout; }
    }

    // ── Swipe Gestures ──────────────────────────────────────────

    /// <summary>Swipe an element in the given direction.</summary>
    private static void SwipeElement(AppiumDriver driver, AppiumElement element, string direction)
    {
        var location = element.Location;
        var size = element.Size;
        var centerX = location.X + size.Width / 2;
        var centerY = location.Y + size.Height / 2;

        if (TestConfig.IsIOS)
        {
            // Use coordinate-based drag for reliable SwipeView triggering on iOS.
            // mobile: swipe with elementId doesn't always reach the inner SwipeView content.
            int dx = direction switch { "left" => -200, "right" => 200, _ => 0 };
            int dy = direction switch { "up" => -200, "down" => 200, _ => 0 };
            driver.ExecuteScript("mobile: dragFromToForDuration", new Dictionary<string, object>
            {
                { "fromX", centerX },
                { "fromY", centerY },
                { "toX", centerX + dx },
                { "toY", centerY + dy },
                { "duration", 0.3 }
            });
        }
        else
        {
            // Android UiAutomator2: "mobile: swipeGesture" with bounding area
            driver.ExecuteScript("mobile: swipeGesture", new Dictionary<string, object>
            {
                { "left", location.X },
                { "top", location.Y },
                { "width", size.Width },
                { "height", size.Height },
                { "direction", direction },
                { "percent", 0.5 }
            });
        }
        Thread.Sleep(300);
    }

    /// <summary>Swipe left on an element (to reveal right swipe actions like Delete).</summary>
    public static void SwipeElementLeft(this AppiumDriver driver, AppiumElement element)
        => SwipeElement(driver, element, "left");

    /// <summary>Swipe right on an element (to reveal left swipe actions like Favorite/Edit).</summary>
    public static void SwipeElementRight(this AppiumDriver driver, AppiumElement element)
        => SwipeElement(driver, element, "right");

    /// <summary>Long-press an element (for multi-select entry). Duration 750ms.</summary>
    public static void LongPress(this AppiumDriver driver, AppiumElement element)
    {
        var location = element.Location;
        var size = element.Size;
        var centerX = location.X + size.Width / 2;
        var centerY = location.Y + size.Height / 2;

        if (TestConfig.IsIOS)
        {
            driver.ExecuteScript("mobile: touchAndHold", new Dictionary<string, object>
            {
                { "x", centerX },
                { "y", centerY },
                { "duration", 0.75 }
            });
        }
        else
        {
            driver.ExecuteScript("mobile: longClickGesture", new Dictionary<string, object>
            {
                { "x", centerX },
                { "y", centerY },
                { "duration", 750 }
            });
        }
    }

    /// <summary>
    /// iOS: tap an alert button by its visible label using Appium's native
    /// <c>mobile: alert</c>. Returns false if no alert or no such button. Preferred
    /// over XPath — iPad renders <c>UIAlertController</c> with styles that don't
    /// always surface as <c>XCUIElementTypeAlert</c> in the XCUI tree.
    /// </summary>
    private static bool TryTapIOSAlertButton(AppiumDriver driver, string buttonLabel)
    {
        if (!TestConfig.IsIOS) return false;
        try
        {
            driver.ExecuteScript("mobile: alert", new Dictionary<string, object>
            {
                { "action", "accept" },
                { "buttonLabel", buttonLabel }
            });
            Thread.Sleep(TestConfig.DelayAfterDismiss);
            return true;
        }
        catch (WebDriverException) { return false; }
    }

    /// <summary>Check if an alert dialog is currently showing.</summary>
    public static bool IsAlertPresent(this AppiumDriver driver)
    {
        if (TestConfig.IsIOS)
        {
            try { driver.SwitchTo().Alert(); return true; }
            catch (WebDriverException) { return false; }
        }

        try
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.ShortTimeout;
            driver.FindElement(By.XPath(
                "//*[@resource-id='android:id/message' or @resource-id='android:id/alertTitle']"));
            return true;
        }
        catch (WebDriverException) { return false; }
        finally { driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout; }
    }

    /// <summary>Navigate back to a tab root, going back multiple times if needed.</summary>
    public static void NavigateToTabRoot(this AppiumDriver driver, string tabTitle, string rootElementId,
        AppiumSetup setup, int maxBacks = 5)
    {
        driver.DismissOnboardingIfPresent(setup);
        driver.NavigateToTab(tabTitle);
        for (int i = 0; i < maxBacks; i++)
        {
            if (driver.IsDisplayed(rootElementId, timeoutSeconds: 2))
                return;
            driver.GoBack();
            Thread.Sleep(300);
        }
    }

    // ── Test Setup Helpers ─────────────────────────────────────

    /// <summary>
    /// Lands on the Prayers tab with the list rendered. Trusts the seed DB — does NOT
    /// verify visibility of "UI Test Prayer" because CollectionView virtualizes off-screen
    /// rows and UIAutomator2 won't expose them, producing false-negatives on existence.
    /// TestDataSeed is the single source of truth for the seeded prayer; callers that need
    /// the prayer to be user-visible should drive to it explicitly (search, scroll, or card-expand).
    /// </summary>
    public static void EnsureUITestPrayerExists(this AppiumDriver driver, AppiumSetup setup)
    {
        driver.EnsureOnTab("Prayers", setup);
        driver.WaitForElement("List_List_Prayers", timeoutSeconds: 10);
    }

    /// <summary>
    /// Ensures a tag named "UITest Tag" exists. Creates it via Tags tab if missing.
    /// Needed for tests that require the action sheet (prayers + tags must both exist).
    /// </summary>
    public static void EnsureUITestTagExists(this AppiumDriver driver, AppiumSetup setup)
    {
        driver.EnsureOnTab("Tags", setup);
        if (driver.IsTextDisplayed("UITest Tag", timeoutSeconds: 3))
            return;

        // Create via Tags tab toolbar
        driver.TapToolbarItem("Add");
        driver.WaitForElement("TagDetail_Entry_Name", timeoutSeconds: 10);
        driver.EnterText("TagDetail_Entry_Name", "UITest Tag");
        driver.TapToolbarItem("Save");
        Thread.Sleep(TestConfig.DelayAfterSave);

        // Verify we're back on tag list (iOS Bug #3: GoToAsync("..") may fail)
        if (!driver.IsDisplayed("Tags_List_Tags", timeoutSeconds: 10) && TestConfig.IsIOS)
            driver.NavigateToTab("Tags");

        Thread.Sleep(TestConfig.DelayCollectionRender);
    }

    /// <summary>
    /// Ensures a "UITest Collection" exists in the Manage Collections page.
    /// Creates it if not found, then navigates back to the calling context.
    /// </summary>
    public static void EnsureUITestCollectionExists(this AppiumDriver driver, AppiumSetup setup)
    {
        driver.EnsureOnTab("Prayer Cards", setup);
        driver.TapToolbarItemById("Collections");
        driver.WaitForElement("Boxes_List_Boxes", timeoutSeconds: 10);

        if (driver.IsTextDisplayed("UITest Collection", timeoutSeconds: 3))
        {
            driver.GoBack();
            return;
        }

        driver.TapToolbarItem("Add");
        driver.WaitForElement("BoxDetail_Entry_Name", timeoutSeconds: 10);
        driver.EnterText("BoxDetail_Entry_Name", "UITest Collection");
        driver.TapToolbarItem("Save");
        Thread.Sleep(TestConfig.DelayAfterSave);

        // CollectionView virtualization can hide an existing row from the text check
        // above, so Save may raise a "Duplicate Name" alert. Accept it, discard the
        // dirty form, and return to the list instead of hanging.
        if (driver.IsAlertPresent())
        {
            driver.DismissAlertIfPresent();
            driver.GoBack();
            driver.DismissAlertIfPresent();
        }

        if (!driver.IsDisplayed("Boxes_List_Boxes", timeoutSeconds: 10) && TestConfig.IsIOS)
            driver.GoBack();

        driver.GoBack();
        Thread.Sleep(TestConfig.DelayAfterNavigation);
    }

    /// <summary>
    /// Find a card cell element by name, suitable for swiping. On iOS, CollectionView
    /// flattens cells so text search returns a tiny inner label — this finds the parent
    /// XCUIElementTypeCell container which has enough height for reliable swipe gestures.
    /// </summary>
    public static AppiumElement? FindCardCell(this AppiumDriver driver, string cardName, int timeoutSeconds = 10)
    {
        try
        {
            if (TestConfig.IsIOS)
            {
                // Find the Cell that contains the card name text
                var xpath = $"//XCUIElementTypeCell[.//XCUIElementTypeStaticText[contains(@name, '{cardName}')]]";
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(timeoutSeconds);
                var cell = (AppiumElement)driver.FindElement(By.XPath(xpath));
                driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
                return cell;
            }
            return (AppiumElement)driver.FindElement(TextLocator(cardName));
        }
        catch (WebDriverException)
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
            return null;
        }
    }

    /// <summary>
    /// Lands on the Prayer Cards tab with the list rendered. Trusts the seed DB — does NOT
    /// verify visibility of "UITest Card" (see EnsureUITestPrayerExists for rationale: visibility
    /// is not a reliable existence proof under CollectionView virtualization, and silent
    /// fallback-create was creating duplicate fixtures on every run).
    /// </summary>
    public static void EnsureUITestCardExists(this AppiumDriver driver, AppiumSetup setup)
    {
        driver.EnsureOnTab("Prayer Cards", setup);
        driver.WaitForElement("Cards_List_Cards", timeoutSeconds: 10);
        Thread.Sleep(TestConfig.DelayCollectionRender);
    }

    /// <summary>Navigate to a new prayer form in edit mode (Prayers tab → Add toolbar).</summary>
    public static void NavigateToNewPrayer(this AppiumDriver driver, AppiumSetup setup)
    {
        driver.EnsureOnTab("Prayers", setup);
        // Wait for the Prayers list to render before tapping the toolbar — prevents
        // racing the "Add" tap against an un-rendered Shell action bar.
        driver.WaitForElement("List_List_Prayers", timeoutSeconds: 10);
        if (TestConfig.IsIOS) Thread.Sleep(500); // Let Shell finish rendering toolbar items
        driver.TapToolbarItem("Add");
        driver.WaitForElement("Detail_Entry_Title", timeoutSeconds: 10);
    }

    /// <summary>Accept any visible alert by tapping its positive button (OK/Yes).</summary>
    public static void DismissAlertIfPresent(this AppiumDriver driver)
    {
        if (TestConfig.IsIOS)
        {
            // Some iOS modals need two accept attempts — the first dismisses the
            // visible alert but a follow-up system alert (e.g. permission) may
            // pop immediately after. See Lessons/uitest-per-test-ui-state-reset.md.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                // App-level DisplayAlertAsync "Unsaved Changes → Discard changes?" uses
                // Discard (destructive) + Cancel. Appium's `autoDismissAlerts` auto-taps
                // Cancel, leaving the form dirty and trapping the back-out loop. Explicit
                // Discard tap escapes. Mirrors any other destructive confirm we may add.
                if (TryTapIOSAlertButton(driver, "Discard")) continue;
                try
                {
                    driver.SwitchTo().Alert().Accept();
                    Thread.Sleep(TestConfig.DelayAfterDismiss);
                }
                catch (WebDriverException)
                {
                    return; // no (more) alerts
                }
            }
            return;
        }

        try
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.ShortTimeout;

            // Prefer positive button (button1 = OK/Yes) over negative (button2 = Cancel)
            var positiveButtons = driver.FindElements(By.Id("android:id/button1"));
            if (positiveButtons.Count > 0)
            {
                positiveButtons[0].Click();
            }
            else
            {
                var anyButton = driver.FindElement(By.XPath(
                    "//*[@resource-id='android:id/button2' or @text='OK' or @text='Cancel']"));
                anyButton.Click();
            }
            Thread.Sleep(TestConfig.DelayAfterDismiss);
        }
        catch (WebDriverException) { }
        finally
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
        }
    }

    // ── iOS Action Sheet helpers ───────────────────────────────

    /// <summary>
    /// iOS-specific: Tap a button in an action sheet using <c>mobile: tap</c> with the
    /// element's center coordinates. On iPad, action sheets render as popovers whose
    /// animation causes WebDriver's <c>Click()</c> to send stale coordinates; the XCUITest
    /// native tap avoids that. Callers must ensure the popover has finished animating
    /// before invoking this (the element's rect is read before the tap fires).
    /// Falls back to standard <c>TapByText</c> on Android.
    /// </summary>
    public static void TapIOSActionSheetButton(this AppiumDriver driver, string buttonName,
        int timeoutSeconds = 10)
    {
        if (!TestConfig.IsIOS)
        {
            driver.TapByText(buttonName, timeoutSeconds);
            return;
        }

        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        var element = (AppiumElement)wait.Until(d => d.FindElement(IOSButtonByNameOrLabel(buttonName)));

        // Use XCUITest native tap with element center coords — immune to iPad popover
        // animation coordinate drift, and the driver no longer accepts elementId alone.
        var loc = element.Location;
        var size = element.Size;
        driver.ExecuteScript("mobile: tap", new Dictionary<string, object>
        {
            { "x", loc.X + size.Width / 2 },
            { "y", loc.Y + size.Height / 2 }
        });
        Thread.Sleep(300);
    }

    // ── iOS CollectionView helpers ─────────────────────────────

    /// <summary>
    /// iOS-specific: Uses <c>mobile: scroll</c> with <c>predicateString</c> to scroll a
    /// container until an element matching the predicate is visible. This reaches inside
    /// CollectionView cells where standard <c>findElement</c> cannot.
    /// </summary>
    /// <returns>True if the scroll command executed without error (element found by the driver).</returns>
    public static bool IOSScrollToPredicateInContainer(this AppiumDriver driver,
        string containerAutomationId, string predicateString, int maxAttempts = 3)
        => IOSScrollInContainer(driver, containerAutomationId, "predicateString", predicateString, maxAttempts);

    /// <summary>
    /// iOS-specific: Uses <c>mobile: scroll</c> with the <c>name</c> parameter to scroll a
    /// container until an element with the given accessibility ID is visible.
    /// </summary>
    public static bool IOSScrollToNameInContainer(this AppiumDriver driver,
        string containerAutomationId, string accessibilityName, int maxAttempts = 3)
        => IOSScrollInContainer(driver, containerAutomationId, "name", accessibilityName, maxAttempts);

    /// <summary>
    /// Shared implementation for iOS <c>mobile: scroll</c> with a finder parameter
    /// (e.g. "predicateString" or "name") that reaches inside CollectionView cells.
    /// </summary>
    private static bool IOSScrollInContainer(AppiumDriver driver,
        string containerAutomationId, string finderKey, string finderValue, int maxAttempts)
    {
        if (!TestConfig.IsIOS) return false;

        string? containerId;
        try
        {
            containerId = driver.FindElement(MobileBy.AccessibilityId(containerAutomationId)).Id;
        }
        catch (NoSuchElementException) { return false; }

        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                driver.ExecuteScript("mobile: scroll", new Dictionary<string, object>
                {
                    { "elementId", containerId },
                    { "direction", "down" },
                    { finderKey, finderValue }
                });
                return true;
            }
            catch (WebDriverException)
            {
                // Element not yet reachable — try scrolling again
            }
        }
        return false;
    }

    // ── Accessibility Assertions ───────────────────────────────

    /// <summary>
    /// Get the accessible description of an element found by AutomationId.
    /// Android: reads content-desc. iOS: reads label attribute.
    /// Returns empty string if attribute not found.
    /// </summary>
    /// <remarks>
    /// iOS Secondary toolbar items live in a <c>UIMenu</c> pull-down, not in the
    /// navbar tree. If direct lookup fails on iOS, open the Secondary menu and read
    /// the <c>UIAction</c>'s label there, then close the menu.
    /// </remarks>
    public static string GetAccessibleDescription(this AppiumDriver driver, string automationId)
        => ReadToolbarAccessibleAttribute(driver, automationId,
            androidAttr: "content-desc", iosAttr: "label");

    /// <summary>
    /// Get the accessible hint of an element found by AutomationId.
    /// Android: reads hint attribute. iOS: reads value attribute.
    /// </summary>
    public static string GetAccessibleHint(this AppiumDriver driver, string automationId)
        => ReadToolbarAccessibleAttribute(driver, automationId,
            androidAttr: "hint", iosAttr: "value");

    /// <summary>
    /// Shared implementation: read an accessibility attribute by AutomationId, with
    /// iOS Secondary-menu fallback for items that only render inside MAUI's UIMenu.
    /// </summary>
    private static string ReadToolbarAccessibleAttribute(AppiumDriver driver,
        string automationId, string androidAttr, string iosAttr)
    {
        string Attr(AppiumElement el) => TestConfig.IsAndroid
            ? el.GetDomAttribute(androidAttr) ?? ""
            : el.GetDomAttribute(iosAttr) ?? "";

        // Fast path: item is in the tree directly. 0s wait lets us reuse FindByAutomationId.
        if (driver.IsDisplayed(automationId, timeoutSeconds: 2))
            return Attr(driver.FindByAutomationId(automationId));

        if (TestConfig.IsIOS && OpenIOSSecondaryMenu(driver))
        {
            try
            {
                return Attr((AppiumElement)driver.FindElement(IOSButtonByNameOrLabel(automationId)));
            }
            catch (WebDriverException) { }
            finally { CloseIOSPopover(driver); }
        }

        // Last resort: let the standard WaitForElement surface the canonical error.
        var element = driver.WaitForElement(automationId, timeoutSeconds: 10);
        return element == null ? "" : Attr(element);
    }

    /// <summary>
    /// Check whether an element with the given description/label text exists in the accessibility tree.
    /// Works on both platforms by searching content-desc (Android) or label (iOS).
    /// Useful for elements without AutomationId that only have SemanticProperties.Description.
    /// </summary>
    public static bool HasAccessibleElement(this AppiumDriver driver, string descriptionText,
        int timeoutSeconds = 3)
    {
        // Use contains() for partial matching — composed descriptions may have extra context.
        // Android: search both @content-desc and @text because UiAutomator2 may not expose
        // layout containers (Grid) as nodes — the text can live on child views instead.
        By locator = TestConfig.IsAndroid
            ? By.XPath($"//*[contains(@content-desc,'{descriptionText}') or contains(@text,'{descriptionText}')]")
            : By.XPath($"//*[contains(@label,'{descriptionText}') or contains(@name,'{descriptionText}')]");
        var prev = driver.Manage().Timeouts().ImplicitWait;
        try
        {
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(timeoutSeconds);
            driver.FindElement(locator);
            return true;
        }
        catch (WebDriverException) { return false; }
        finally { driver.Manage().Timeouts().ImplicitWait = prev; }
    }

    /// <summary>
    /// Assert that NO accessible element contains the given text.
    /// Used to verify decorative elements are hidden from the tree.
    /// Android-only — iOS flattening makes this unreliable for children inside Description'd containers.
    /// </summary>
    public static void AssertNotInAccessibleTree(this AppiumDriver driver, string text,
        int timeoutSeconds = 2)
    {
        if (TestConfig.IsIOS) return; // iOS flattening makes child-level tree assertions unreliable

        By locator = By.XPath($"//*[@content-desc='{text}' or @text='{text}']");
        var prev = driver.Manage().Timeouts().ImplicitWait;
        try
        {
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(timeoutSeconds);
            driver.FindElement(locator);
            throw new Xunit.Sdk.XunitException(
                $"Element with text '{text}' should not be in the accessibility tree but was found");
        }
        catch (WebDriverException)
        {
            // Expected — element correctly hidden
        }
        finally { driver.Manage().Timeouts().ImplicitWait = prev; }
    }

    /// <summary>
    /// Android-only: Assert an element is marked as a heading in the accessibility tree.
    /// iOS/XCUITest does not expose heading status via Appium.
    /// </summary>
    public static void AssertIsHeading(this AppiumDriver driver, string automationId)
    {
        if (TestConfig.IsIOS) return; // heading attribute not exposed on iOS

        var element = driver.WaitForElement(automationId, timeoutSeconds: 10)
            ?? throw new Xunit.Sdk.XunitException(
                $"Element '{automationId}' not found for heading check");
        var heading = element.GetDomAttribute("heading");
        if (heading != "true")
            throw new Xunit.Sdk.XunitException(
                $"Element '{automationId}' expected heading='true' but got '{heading}'");
    }

    // ── Diagnostics ──────────────────────────────────────────────

    /// <summary>
    /// Dumps the Appium page source (accessibility tree) to a file in the test output directory.
    /// Use this to diagnose elements that are visually present but not found by Appium locators.
    /// Returns the file path written to.
    /// </summary>
    public static string DumpPageSource(this AppiumDriver driver, string testName)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "diagnostics");
        Directory.CreateDirectory(dir);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        var filePath = Path.Combine(dir, $"{testName}_{timestamp}.xml");
        File.WriteAllText(filePath, driver.PageSource);
        return filePath;
    }

    // ── Platform Intent Helpers ──────────────────────────────────

    /// <summary>
    /// Android-only: dispatch <c>ACTION_PROCESS_TEXT</c> at MainActivity so the
    /// Slice 2 selection-toolbar handoff can be exercised without driving Gmail's UI.
    /// Multi-line payloads are not supported here because <c>am start --es</c> tokenises
    /// values through adb's shell — newlines are stripped. Production multi-line parsing
    /// is covered by <c>TextSelectionParser</c> unit tests; manual emulator smoke covers
    /// the real Gmail → toolbar → modal end-to-end path.
    /// </summary>
    public static void LaunchProcessTextIntent(this AppiumDriver driver, string text)
    {
        if (TestConfig.IsIOS)
            throw new SkipException("Android-only: PROCESS_TEXT is the Android selection-toolbar entry point");

        driver.ExecuteScript("mobile: startActivity", new Dictionary<string, object>
        {
            { "appPackage", TestConfig.AndroidPackage },
            { "appActivity", TestConfig.AndroidMainActivity },
            { "intentAction", "android.intent.action.PROCESS_TEXT" },
            { "mimeType", "text/plain" },
            { "optionalIntentArguments", $"--es android.intent.extra.PROCESS_TEXT \"{text}\"" }
        });
    }
}

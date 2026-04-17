using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;
using PrayerApp.UITests.Infrastructure;
using System.IO;

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
    public static void DismissKeyboardIfPresent(this AppiumDriver driver)
    {
        if (!TestConfig.IsIOS) return;
        try { driver.HideKeyboard(); } catch (WebDriverException) { }
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
        bool visible = TestConfig.IsIOS
            ? driver.IsTextContainsDisplayed(cardName, timeoutSeconds: 2)
            : driver.IsTextDisplayed(cardName, timeoutSeconds: 2);
        if (visible) return;
        try
        {
            driver.ScrollDownToText(cardName, maxScrolls: 3,
                scrollableAutomationId: "Cards_List_Cards");
        }
        catch (WebDriverException) { /* let the caller's tap raise the canonical error */ }
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
    private static bool TryTapTab(AppiumDriver driver, string tabTitle)
    {
        try
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.ShortTimeout;
            var tab = driver.FindElement(MobileBy.AccessibilityId(tabTitle));
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

        // TD-17 step 1: on the Cards tab, force every collection section to "expanded"
        // so downstream assertions that look for cards inside a collection don't time
        // out against a collapsed section. Preferences default is "all collapsed" on
        // first launch — which is what every seeded test session starts from.
        if (tabTitle == "Prayer Cards")
        {
            driver.EnsureAllSectionsExpanded();
        }
    }

    /// <summary>
    /// Expand every collapsed collection section on the Cards page. No-op for
    /// sections that are already expanded.
    ///
    /// Why this exists: MAUI persists <c>ExpandedSectionIds</c> in Preferences
    /// and the default (empty) state renders every section collapsed. Many
    /// UITests assert on cards inside a collection without expanding it first;
    /// on a freshly-seeded session they time out. See TD-17 / the
    /// uitest-fix-harness-globals-before-per-test-audits lesson.
    ///
    /// Detection heuristic: the triangle indicator and HeaderText labels inside
    /// the section header are marked <c>AutomationProperties.IsInAccessibleTree="False"</c>,
    /// so we can't read "▷" vs "▼" from the a11y tree. Instead we look at the
    /// vertical gap between consecutive section headers: collapsed sections sit
    /// flush against their footer (HeightRequest=0 when collapsed per
    /// PrayerCardsPage.xaml), so the gap to the next header is tiny. Expanded
    /// sections have 60+ px per card between headers.
    /// </summary>
    public static void EnsureAllSectionsExpanded(this AppiumDriver driver)
    {
        var headers = driver.FindElements(AutomationIdLocator("Cards_Section_Header"))
            .OfType<AppiumElement>()
            .OrderBy(h => h.Location.Y)
            .ToList();

        if (headers.Count == 0) return;

        // Gap threshold: a collapsed section's footer collapses to HeightRequest=0,
        // so consecutive header tops differ by just the header's own height
        // (~60-120 px depending on platform). An expanded section adds at least one
        // ~60+ px card row, pushing the gap above 120 px. 100 px is the safe floor.
        const int CollapsedGapThreshold = 100;

        for (int i = 0; i < headers.Count; i++)
        {
            int headerBottom = headers[i].Location.Y + headers[i].Size.Height;
            int nextHeaderTop = (i + 1 < headers.Count)
                ? headers[i + 1].Location.Y
                : int.MaxValue;

            bool looksCollapsed = (nextHeaderTop - headerBottom) < CollapsedGapThreshold;
            if (!looksCollapsed) continue;

            try
            {
                headers[i].Click();
                Thread.Sleep(TestConfig.DelayAfterTap);
            }
            catch (WebDriverException)
            {
                // Best-effort: if a header becomes stale after a prior tap reflowed
                // the list, swallow and move on. Tests that genuinely need a specific
                // section will fall back to their own TapByText(sectionName).
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
    /// Clear transient UI state (open alerts, multi-select mode) that a prior failing
    /// test may have left behind, so it doesn't cascade into false failures in the
    /// next test. Navigation is NOT part of this helper — callers navigate to their
    /// target tab right after.
    ///
    /// Call at the TOP of every test that assumes a clean baseline. Tests that
    /// specifically verify a transient state (e.g. `Cards_MultiSelect_ToolbarAppearsAndCancels`)
    /// should NOT call this — but the NEXT test always should.
    ///
    /// See Lessons/uitest-per-test-ui-state-reset.md for rationale.
    /// </summary>
    public static void ResetAppUIState(this AppiumDriver driver, AppiumSetup setup)
    {
        try { driver.DismissAlertIfPresent(); } catch { /* best effort */ }

        // The Select AutomationId is stable across multi-select toggle — the visible text
        // mutates between "Select" and "Cancel" but the ID stays "Select" for automation.
        // 0-second timeout: ~99% of tests don't need this, and a 1-second implicit wait
        // per call would add ~90s to the suite on the cold path.
        try
        {
            if (driver.IsDisplayed("Cards_Bar_MultiSelect", timeoutSeconds: 0))
            {
                driver.TapToolbarItemById("Select");
                Thread.Sleep(TestConfig.DelayAfterTap);
            }
        }
        catch { /* not on Prayer Cards or not in multi-select */ }
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
    /// Tap a Shell ToolbarItem by its <c>AutomationId</c>. Preferred over
    /// <see cref="TapToolbarItem"/> for any iconized toolbar: once a ToolbarItem has
    /// <c>IconImageSource</c>, MAUI Shell renders it as an icon-only button on Android
    /// and the visible <c>Text</c> is no longer a <c>TextView</c> in the UiAutomator2
    /// tree. <c>AutomationId</c> is the stable contract and works for text-only AND
    /// icon-only ToolbarItems on both platforms.
    /// </summary>
    public static void TapToolbarItemById(this AppiumDriver driver, string automationId,
        int timeoutSeconds = 10)
    {
        driver.DismissKeyboardIfPresent();
        if (TestConfig.IsIOS) Thread.Sleep(300);

        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        var locator = AutomationIdLocator(automationId);

        var element = wait.Until(d => d.FindElement(locator));
        element.Click();
        Thread.Sleep(300);
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

        // iOS Shell toolbar items render as XCUIElementTypeButton — use specific locator
        By locator = TestConfig.IsIOS
            ? By.XPath($"//XCUIElementTypeButton[@name='{text}' or @label='{text}']")
            : TextLocator(text);

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
    /// element's native ID. On iPad, action sheets render as popovers whose animation
    /// causes WebDriver's <c>Click()</c> to send stale coordinates. Using <c>mobile: tap</c>
    /// with <c>elementId</c> delegates coordinate resolution to XCUITest, which taps the
    /// element's actual current position.
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
        var locator = By.XPath(
            $"//XCUIElementTypeButton[@name='{buttonName}' or @label='{buttonName}']");
        var element = (AppiumElement)wait.Until(d => d.FindElement(locator));

        // Use XCUITest native tap instead of WebDriver Click() — immune to
        // iPad popover animation coordinate drift
        driver.ExecuteScript("mobile: tap", new Dictionary<string, object>
        {
            { "elementId", element.Id }
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
    public static string GetAccessibleDescription(this AppiumDriver driver, string automationId)
    {
        var element = driver.WaitForElement(automationId, timeoutSeconds: 10);
        if (element == null) return "";
        return TestConfig.IsAndroid
            ? element.GetDomAttribute("content-desc") ?? ""
            : element.GetDomAttribute("label") ?? "";
    }

    /// <summary>
    /// Get the accessible hint of an element found by AutomationId.
    /// Android: reads hint attribute. iOS: reads value attribute.
    /// </summary>
    public static string GetAccessibleHint(this AppiumDriver driver, string automationId)
    {
        var element = driver.WaitForElement(automationId, timeoutSeconds: 10);
        if (element == null) return "";
        return TestConfig.IsAndroid
            ? element.GetDomAttribute("hint") ?? ""
            : element.GetDomAttribute("value") ?? "";
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
}

using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;
using PrayerApp.UITests.Infrastructure;

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

        // Stage 2: Try dismissing a known modal (Cancel buttons that block tab access)
        foreach (var modalButton in new[] { "Scope_Btn_Cancel", "QuickAdd_Btn_Cancel" })
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
        driver.DismissOnboardingIfPresent(setup);
        driver.NavigateToTab(tabTitle);
    }

    /// <summary>Go back (Android back button or iOS back nav).</summary>
    public static void GoBack(this AppiumDriver driver)
        => driver.Navigate().Back();

    // ── Onboarding ───────────────────────────────────────────────

    /// <summary>
    /// Dismiss onboarding if currently showing. Idempotent — safe to call multiple times.
    /// Taps "Skip tour" on the welcome popup, "Skip" on a mid-flow banner, or "Got it!"
    /// on the final PrayerTimeHighlight step, then "Done" on the completion popup.
    /// </summary>
    public static void DismissOnboardingIfPresent(this AppiumDriver driver, AppiumSetup setup)
    {
        if (setup.OnboardingHandled) return;

        // Check for dismissal buttons — welcome popup, mid-onboarding banner, or final "Got it!"
        string? dismissButton = driver.IsDisplayed("Welcome_Btn_Skip", timeoutSeconds: 3) ? "Welcome_Btn_Skip"
            : driver.IsDisplayed("Banner_Btn_Skip", timeoutSeconds: 2) ? "Banner_Btn_Skip"
            : driver.IsDisplayed("Banner_Btn_GotIt", timeoutSeconds: 2) ? "Banner_Btn_GotIt"
            : null;

        if (dismissButton != null)
        {
            driver.Tap(dismissButton);
            Thread.Sleep(1000);

            if (driver.IsDisplayed("Complete_Btn_Done", timeoutSeconds: 5))
            {
                driver.Tap("Complete_Btn_Done");
                Thread.Sleep(500);
            }
        }

        setup.OnboardingHandled = true;
    }

    // ── Alerts/Dialogs ───────────────────────────────────────────

    /// <summary>Check if an alert dialog is displayed and get its text.</summary>
    public static bool TryGetAlertText(this AppiumDriver driver, out string text)
    {
        if (TestConfig.IsIOS)
        {
            try { text = driver.SwitchTo().Alert().Text; return true; }
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

    /// <summary>Tap a Shell ToolbarItem by its text label (e.g., "Save", "Edit", "Add Card").</summary>
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
    public static AppiumElement FindByText(this AppiumDriver driver, string text, int timeoutSeconds = 5)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        return (AppiumElement)wait.Until(d => d.FindElement(TextLocator(text)));
    }

    /// <summary>Find an element whose text/label contains the given substring.</summary>
    public static AppiumElement FindByTextContains(this AppiumDriver driver, string text, int timeoutSeconds = 5)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        return (AppiumElement)wait.Until(d => d.FindElement(TextContainsLocator(text)));
    }

    /// <summary>Find and tap any element by its visible text.</summary>
    public static void TapByText(this AppiumDriver driver, string text, int timeoutSeconds = 5)
    {
        driver.FindByText(text, timeoutSeconds).Click();
        Thread.Sleep(300);
    }

    /// <summary>Find and tap any element whose text/label <em>contains</em> the given substring.</summary>
    public static void TapByTextContains(this AppiumDriver driver, string text, int timeoutSeconds = 5)
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
        if (TestConfig.IsIOS)
        {
            // iOS XCUITest: "mobile: swipe" with elementId, direction, and velocity (px/sec)
            driver.ExecuteScript("mobile: swipe", new Dictionary<string, object>
            {
                { "elementId", element.Id },
                { "direction", direction },
                { "velocity", IOSSwipeVelocity }
            });
        }
        else
        {
            // Android UiAutomator2: "mobile: swipeGesture" with bounding area
            var location = element.Location;
            var size = element.Size;
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
    /// Ensures a prayer named "UI Test Prayer" exists. Creates it via QuickAdd if missing.
    /// Call at the start of any test that depends on this prayer existing.
    /// </summary>
    public static void EnsureUITestPrayerExists(this AppiumDriver driver, AppiumSetup setup)
    {
        driver.EnsureOnTab("Prayers", setup);
        if (driver.IsTextDisplayed("UI Test Prayer", timeoutSeconds: 3))
            return;

        // Create via QuickAdd from Home tab
        driver.NavigateToTab("Home");
        driver.Tap("Home_Btn_QuickAdd");
        driver.WaitForElement("QuickAdd_Entry_Title");
        driver.EnterText("QuickAdd_Entry_Title", "UI Test Prayer");
        driver.Tap("QuickAdd_Btn_Add");
        Thread.Sleep(TestConfig.DelayAfterSave);

        // Navigate back to Prayers and wait for the new prayer to render
        driver.NavigateToTab("Prayers");
        Thread.Sleep(TestConfig.DelayCollectionRender);
        driver.IsTextDisplayed("UI Test Prayer", timeoutSeconds: 5);
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
        driver.WaitForElement("TagDetail_Entry_Name", timeoutSeconds: 5);
        driver.EnterText("TagDetail_Entry_Name", "UITest Tag");
        driver.TapToolbarItem("Save");
        Thread.Sleep(TestConfig.DelayAfterSave);

        // Verify we're back on tag list (iOS Bug #3: GoToAsync("..") may fail)
        if (!driver.IsDisplayed("Tags_List_Tags", timeoutSeconds: 5) && TestConfig.IsIOS)
            driver.NavigateToTab("Tags");

        Thread.Sleep(TestConfig.DelayCollectionRender);
    }

    /// <summary>
    /// Ensures a user-created card named "UITest Card" exists. Creates it via the
    /// Prayer Cards tab toolbar if missing. Needed for tests that require a non-system card.
    /// </summary>
    public static void EnsureUITestCardExists(this AppiumDriver driver, AppiumSetup setup)
    {
        driver.EnsureOnTab("Prayer Cards", setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);

        bool found = TestConfig.IsIOS
            ? driver.IsTextContainsDisplayed("UITest Card", timeoutSeconds: 3)
            : driver.IsTextDisplayed("UITest Card", timeoutSeconds: 3);

        if (found) return;

        // Create via toolbar Add Card
        driver.TapToolbarItem("Add Card");
        driver.WaitForElement("Card_Entry_Title", timeoutSeconds: 5);
        driver.EnterText("Card_Entry_Title", "UITest Card");
        driver.DismissKeyboardIfPresent();
        driver.TapToolbarItem("Save");
        Thread.Sleep(TestConfig.DelayAfterSave);

        // Return to card list
        driver.EnsureOnTab("Prayer Cards", setup);
        Thread.Sleep(TestConfig.DelayCollectionRender);
    }

    /// <summary>Navigate to a new prayer form in edit mode (Prayers tab → Add toolbar).</summary>
    public static void NavigateToNewPrayer(this AppiumDriver driver, AppiumSetup setup)
    {
        driver.EnsureOnTab("Prayers", setup);
        if (TestConfig.IsIOS) Thread.Sleep(500); // Let Shell finish rendering toolbar items
        driver.TapToolbarItem("Add");
        driver.WaitForElement("Detail_Entry_Title", timeoutSeconds: 5);
    }

    /// <summary>Accept any visible alert by tapping its positive button (OK/Yes).</summary>
    public static void DismissAlertIfPresent(this AppiumDriver driver)
    {
        if (TestConfig.IsIOS)
        {
            try { driver.SwitchTo().Alert().Accept(); Thread.Sleep(300); }
            catch (WebDriverException) { }
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
        int timeoutSeconds = 5)
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

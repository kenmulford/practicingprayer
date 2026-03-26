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
        wait.Until(d =>
        {
            try
            {
                driver.Manage().Timeouts().ImplicitWait = TestConfig.ShortTimeout;
                d.FindElement(locator);
                return false;
            }
            catch (NoSuchElementException)
            {
                return true;
            }
            finally
            {
                driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
            }
        });
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
        element.Clear();
        element.SendKeys(text);
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

    /// <summary>Scroll down until an element with the given AutomationId is found (Android).</summary>
    public static AppiumElement ScrollDownTo(this AppiumDriver driver, string automationId,
        int maxScrolls = 5)
    {
        var locator = AutomationIdLocator(automationId);
        var size = driver.Manage().Window.Size;
        for (int i = 0; i < maxScrolls; i++)
        {
            try
            {
                driver.Manage().Timeouts().ImplicitWait = TestConfig.ShortTimeout;
                var element = driver.FindElement(locator);
                if (element.Displayed)
                    return (AppiumElement)element;
            }
            catch (NoSuchElementException) { }
            finally
            {
                driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
            }

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

        throw new NoSuchElementException($"Element '{automationId}' not found after {maxScrolls} scrolls.");
    }

    // ── Navigation ───────────────────────────────────────────────

    /// <summary>Navigate to a Shell tab by tapping its tab bar item.</summary>
    public static void NavigateToTab(this AppiumDriver driver, string tabTitle)
    {
        // Try up to 3 times to find the tab bar, going back each time to clear
        // modals, detail pages, and deep navigation stacks.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            driver.DismissAlertIfPresent();

            if (TryTapTab(driver, tabTitle))
                return;

            // Tab not found — go back to clear the current page/modal
            try { driver.Navigate().Back(); Thread.Sleep(300); } catch (WebDriverException) { }
        }

        // Nuclear recovery: re-activate the app (handles case where GoBack closed it)
        try
        {
            driver.ActivateApp(TestConfig.AndroidPackage);
            Thread.Sleep(1000);
        }
        catch (WebDriverException) { }

        if (TryTapTab(driver, tabTitle))
            return;

        // Final XPath fallback
        var tab = driver.FindElement(By.XPath(
            $"//*[contains(@text,'{tabTitle}') or contains(@content-desc,'{tabTitle}')]"));
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
        catch (NoSuchElementException) { return false; }
        catch (WebDriverException) { return false; }
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
    /// Taps "Skip tour" on the welcome popup, then "Done" on the completion popup.
    /// </summary>
    public static void DismissOnboardingIfPresent(this AppiumDriver driver, AppiumSetup setup)
    {
        if (setup.OnboardingHandled) return;

        // Check for skip buttons — welcome popup or mid-onboarding banner
        string? skipButton = driver.IsDisplayed("Welcome_Btn_Skip", timeoutSeconds: 3) ? "Welcome_Btn_Skip"
            : driver.IsDisplayed("Banner_Btn_Skip", timeoutSeconds: 2) ? "Banner_Btn_Skip"
            : null;

        if (skipButton != null)
        {
            driver.Tap(skipButton);
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
        try
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.ShortTimeout;
            var alert = driver.FindElement(By.Id("android:id/message"));
            text = alert.Text;
            return true;
        }
        catch (WebDriverException)
        {
            text = "";
            return false;
        }
        finally
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
        }
    }

    /// <summary>Tap a button in an alert dialog by its text.</summary>
    public static void TapAlertButton(this AppiumDriver driver, string buttonText)
    {
        var button = driver.FindElement(By.XPath(
            $"//*[@resource-id='android:id/button1' or @resource-id='android:id/button2' or @resource-id='android:id/button3'][contains(@text,'{buttonText}')]"));
        button.Click();
        Thread.Sleep(300);
    }

    // ── Toolbar / Text Finders ────────────────────────────────────

    /// <summary>Tap a Shell ToolbarItem by its text label (e.g., "Save", "Edit", "Add Card").</summary>
    public static void TapToolbarItem(this AppiumDriver driver, string text, int timeoutSeconds = 5)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        var element = wait.Until(d => d.FindElement(By.XPath(
            $"//*[@text='{text}' or @content-desc='{text}']")));
        element.Click();
        Thread.Sleep(300);
    }

    /// <summary>Find an element by its visible text.</summary>
    public static AppiumElement FindByText(this AppiumDriver driver, string text, int timeoutSeconds = 5)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        return (AppiumElement)wait.Until(d => d.FindElement(By.XPath($"//*[@text='{text}']")));
    }

    /// <summary>Find and tap any element by its visible text.</summary>
    public static void TapByText(this AppiumDriver driver, string text, int timeoutSeconds = 5)
    {
        driver.FindByText(text, timeoutSeconds).Click();
        Thread.Sleep(300);
    }

    /// <summary>Check if an element with the given text is displayed.</summary>
    public static bool IsTextDisplayed(this AppiumDriver driver, string text, int timeoutSeconds = 3)
    {
        try
        {
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(timeoutSeconds);
            var element = driver.FindElement(By.XPath($"//*[@text='{text}']"));
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
        driver.ExecuteScript("mobile: swipeGesture", new Dictionary<string, object>
        {
            { "left", location.X },
            { "top", location.Y },
            { "width", size.Width },
            { "height", size.Height },
            { "direction", direction },
            { "percent", 0.5 }
        });
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

    /// <summary>Navigate to a new prayer form in edit mode (Prayers tab → Add toolbar).</summary>
    public static void NavigateToNewPrayer(this AppiumDriver driver, AppiumSetup setup)
    {
        driver.EnsureOnTab("Prayers", setup);
        driver.TapToolbarItem("Add");
        driver.WaitForElement("Detail_Entry_Title", timeoutSeconds: 5);
    }

    /// <summary>Dismiss any visible alert by tapping its positive button (OK/Yes/Cancel).</summary>
    public static void DismissAlertIfPresent(this AppiumDriver driver)
    {
        try
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.ShortTimeout;

            // Single XPath: native AlertDialog buttons OR common button text
            var button = driver.FindElement(By.XPath(
                "//*[@resource-id='android:id/button1' or @resource-id='android:id/button2' " +
                "or @text='OK' or @text='Cancel']"));
            button.Click();
            Thread.Sleep(300);
        }
        catch (WebDriverException) { }
        finally
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
        }
    }
}

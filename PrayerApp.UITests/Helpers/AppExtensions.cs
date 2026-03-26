using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;
using PrayerApp.UITests.Infrastructure;

namespace PrayerApp.UITests.Helpers;

/// <summary>
/// Convenience extensions for Appium driver interactions.
/// All element lookups use AccessibilityId (maps to AutomationId in MAUI).
/// </summary>
public static class AppExtensions
{
    /// <summary>Find an element by its AutomationId.</summary>
    public static AppiumElement FindByAutomationId(this AppiumDriver driver, string automationId)
        => driver.FindElement(MobileBy.AccessibilityId(automationId));

    /// <summary>Wait for an element with the given AutomationId to appear.</summary>
    public static AppiumElement WaitForElement(this AppiumDriver driver, string automationId,
        int timeoutSeconds = 15)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        return (AppiumElement)wait.Until(d => d.FindElement(MobileBy.AccessibilityId(automationId)));
    }

    /// <summary>Wait for an element to disappear from the screen.</summary>
    public static void WaitForElementGone(this AppiumDriver driver, string automationId,
        int timeoutSeconds = 10)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        wait.Until(d =>
        {
            try
            {
                d.FindElement(MobileBy.AccessibilityId(automationId));
                return false; // still present
            }
            catch (NoSuchElementException)
            {
                return true; // gone
            }
        });
    }

    /// <summary>Tap an element by AutomationId.</summary>
    public static void Tap(this AppiumDriver driver, string automationId)
        => driver.FindByAutomationId(automationId).Click();

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
    public static bool IsDisplayed(this AppiumDriver driver, string automationId)
    {
        try
        {
            // Use a short implicit wait so we don't block for 15s
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);
            var element = driver.FindElement(MobileBy.AccessibilityId(automationId));
            return element.Displayed;
        }
        catch (NoSuchElementException)
        {
            return false;
        }
        finally
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
        }
    }

    /// <summary>Scroll down until an element with the given AutomationId is found (Android only).</summary>
    public static AppiumElement ScrollDownTo(this AppiumDriver driver, string automationId,
        int maxScrolls = 5)
    {
        for (int i = 0; i < maxScrolls; i++)
        {
            try
            {
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);
                var element = driver.FindElement(MobileBy.AccessibilityId(automationId));
                if (element.Displayed)
                    return (AppiumElement)element;
            }
            catch (NoSuchElementException) { }
            finally
            {
                driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
            }

            // Swipe up to scroll down
            var size = driver.Manage().Window.Size;
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

    /// <summary>Navigate to a Shell tab by tapping its tab bar item text.</summary>
    public static void NavigateToTab(this AppiumDriver driver, string tabText)
    {
        // Shell tabs are identified by their title text
        var tab = driver.FindElement(MobileBy.AccessibilityId(tabText));
        tab.Click();
        Thread.Sleep(500); // Brief settle time for page transition
    }

    /// <summary>Go back (Android back button or iOS back nav).</summary>
    public static void GoBack(this AppiumDriver driver)
        => driver.Navigate().Back();
}

using OpenQA.Selenium;
using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 6: Reminders / Notifications (UI-only — no actual notification firing)
/// </summary>
[Collection("Appium")]
[Trait("Platform", "CrossPlatform")]
[Trait("Section", "6-Reminders")]
public class ReminderTests
{
    private readonly AppiumSetup _setup;
    public ReminderTests(AppiumSetup setup) => _setup = setup;

    /// <summary>Navigate to a new prayer and enable the reminders toggle.</summary>
    private void NavigateToNewPrayerWithReminders(string title)
    {
        var driver = _setup.Driver;
        driver.NavigateToNewPrayer(_setup);
        driver.EnterText("Detail_Entry_Title", title);
        driver.ScrollDownTo("Detail_Switch_Reminders");
        driver.Tap("Detail_Switch_Reminders");
        Thread.Sleep(TestConfig.DelayDirtyRegistration);
    }

    /// <summary>6.1 + 6.6: Reminders toggle round-trip — toggling on reveals the
    /// frequency/time pickers, toggling off hides them again. Merged from the former
    /// Reminders_ToggleOn_ShowsPickers and Reminders_ToggleOff_HidesPickers in issue
    /// #148 Phase 2 — both shared the NavigateToNewPrayerWithReminders prologue, so one
    /// test exercises both edges with a single navigation.</summary>
    [Fact]
    public void Reminders_Toggle_ShowsThenHidesPickers()
    {
        _setup.Driver.ResetAppUIState(_setup);
        NavigateToNewPrayerWithReminders("Reminder Test Prayer");
        var driver = _setup.Driver;

        // Toggle ON (done by the prologue) — pickers appear.
        Assert.True(driver.IsDisplayed("Detail_Picker_Frequency", timeoutSeconds: 10),
            "Frequency picker should appear when reminders are enabled");
        Assert.True(driver.IsDisplayed("Detail_Picker_ReminderTime", timeoutSeconds: 3),
            "Reminder time picker should appear when reminders are enabled");

        // Toggle OFF — pickers hide.
        driver.Tap("Detail_Switch_Reminders");
        Thread.Sleep(TestConfig.DelayDirtyRegistration);

        Assert.False(driver.IsDisplayed("Detail_Picker_Frequency", timeoutSeconds: 2),
            "Frequency picker should be hidden when reminders are disabled");
        Assert.False(driver.IsDisplayed("Detail_Picker_ReminderTime", timeoutSeconds: 2),
            "Reminder time picker should be hidden when reminders are disabled");

        driver.GoBack();
        driver.DismissAlertIfPresent();
    }

    /// <summary>6.2: Frequency picker is populated with options.</summary>
    [Fact]
    public void Reminders_FrequencyPicker_HasOptions()
    {
        _setup.Driver.ResetAppUIState(_setup);
        NavigateToNewPrayerWithReminders("Freq Test Prayer");
        var driver = _setup.Driver;

        if (driver.IsDisplayed("Detail_Picker_Frequency", timeoutSeconds: 3))
        {
            driver.Tap("Detail_Picker_Frequency");
            Thread.Sleep(TestConfig.DelayAfterNavigation);

            if (TestConfig.IsIOS)
            {
                // iOS shows a native picker wheel — options aren't findable via text locators.
                // Verify the picker opened by looking for the picker wheel element or Done button.
                bool pickerOpened;
                try
                {
                    driver.Manage().Timeouts().ImplicitWait = TestConfig.ShortTimeout;
                    pickerOpened = driver.IsTextDisplayed("Done", timeoutSeconds: 3)
                        || driver.FindElements(By.XPath("//XCUIElementTypePickerWheel")).Count > 0;
                }
                finally
                {
                    driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
                }
                Assert.True(pickerOpened,
                    "Frequency picker should open and show a selection wheel on iOS");
                // Dismiss the picker
                if (driver.IsTextDisplayed("Done", timeoutSeconds: 1))
                    driver.TapByText("Done");
            }
            else
            {
                var hasDaily = driver.IsTextDisplayed("Daily", timeoutSeconds: 3);
                var hasWeekly = driver.IsTextDisplayed("Weekly", timeoutSeconds: 2);
                driver.DismissAlertIfPresent();
                Assert.True(hasDaily || hasWeekly,
                    "Frequency picker should show options like Daily, Weekly");
            }
        }

        driver.GoBack();
        driver.DismissAlertIfPresent();
    }
}

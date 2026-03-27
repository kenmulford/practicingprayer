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

    /// <summary>6.1: Enable reminders — toggle on shows frequency/time pickers.</summary>
    [Fact]
    public void Reminders_ToggleOn_ShowsPickers()
    {
        _setup.Driver.NavigateToNewPrayer(_setup);
        var driver = _setup.Driver;

        driver.EnterText("Detail_Entry_Title", "Reminder Test Prayer");
        driver.ScrollDownTo("Detail_Switch_Reminders");

        driver.Tap("Detail_Switch_Reminders");
        Thread.Sleep(500);

        Assert.True(driver.IsDisplayed("Detail_Picker_Frequency", timeoutSeconds: 5),
            "Frequency picker should appear when reminders are enabled");
        Assert.True(driver.IsDisplayed("Detail_Picker_ReminderTime", timeoutSeconds: 3),
            "Reminder time picker should appear when reminders are enabled");

        driver.GoBack();
        driver.DismissAlertIfPresent();
    }

    /// <summary>6.2: Frequency picker is populated with options.</summary>
    [Fact]
    public void Reminders_FrequencyPicker_HasOptions()
    {
        _setup.Driver.NavigateToNewPrayer(_setup);
        var driver = _setup.Driver;

        driver.EnterText("Detail_Entry_Title", "Freq Test Prayer");
        driver.ScrollDownTo("Detail_Switch_Reminders");

        driver.Tap("Detail_Switch_Reminders");
        Thread.Sleep(500);

        if (driver.IsDisplayed("Detail_Picker_Frequency", timeoutSeconds: 3))
        {
            driver.Tap("Detail_Picker_Frequency");
            Thread.Sleep(500);

            var hasDaily = driver.IsTextDisplayed("Daily", timeoutSeconds: 3);
            var hasWeekly = driver.IsTextDisplayed("Weekly", timeoutSeconds: 2);

            driver.DismissAlertIfPresent();

            Assert.True(hasDaily || hasWeekly,
                "Frequency picker should show options like Daily, Weekly");
        }

        driver.GoBack();
        driver.DismissAlertIfPresent();
    }

    /// <summary>6.6: Disable reminders — toggle off hides pickers.</summary>
    [Fact]
    public void Reminders_ToggleOff_HidesPickers()
    {
        _setup.Driver.NavigateToNewPrayer(_setup);
        var driver = _setup.Driver;

        driver.EnterText("Detail_Entry_Title", "Toggle Off Test");
        driver.ScrollDownTo("Detail_Switch_Reminders");

        // Toggle ON
        driver.Tap("Detail_Switch_Reminders");
        Thread.Sleep(500);
        Assert.True(driver.IsDisplayed("Detail_Picker_Frequency", timeoutSeconds: 3));

        // Toggle OFF
        driver.Tap("Detail_Switch_Reminders");
        Thread.Sleep(500);

        Assert.False(driver.IsDisplayed("Detail_Picker_Frequency", timeoutSeconds: 2),
            "Frequency picker should be hidden when reminders are disabled");

        driver.GoBack();
        driver.DismissAlertIfPresent();
    }
}

using Foundation;
using Microsoft.Maui.Handlers;
using UIKit;

namespace PrayerApp.Platforms.iOS.Handlers;

/// <summary>
/// Forces the iOS UIDatePicker (used as InputView for TimePicker/DatePicker)
/// to use en_US locale so AM/PM renders in English regardless of device settings.
/// </summary>
public static class EnglishLocaleTimePickerHandler
{
    public static void Configure()
    {
        var enUS = new NSLocale("en_US");

        TimePickerHandler.Mapper.AppendToMapping("ForceEnglishLocale", (handler, view) =>
        {
            if (handler.PlatformView is UITextField textField &&
                textField.InputView is UIDatePicker datePicker)
                datePicker.Locale = enUS;
        });

        DatePickerHandler.Mapper.AppendToMapping("ForceEnglishLocale", (handler, view) =>
        {
            if (handler.PlatformView is UITextField textField &&
                textField.InputView is UIDatePicker datePicker)
                datePicker.Locale = enUS;
        });
    }
}

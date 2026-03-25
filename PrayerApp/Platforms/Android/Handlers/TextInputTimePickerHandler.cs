using Android.Widget;
using Google.Android.Material.TimePicker;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using AndroidView = Android.Views.View;

namespace PrayerApp.Platforms.Android.Handlers;

/// <summary>
/// Forces the Android TimePicker dialog to open in text-input mode (HH:MM fields)
/// instead of the default clock-face mode.
/// </summary>
public static class TextInputTimePickerHandler
{
    public static void Configure()
    {
        TimePickerHandler.Mapper.AppendToMapping("TextInputMode", (handler, view) =>
        {
            if (handler.PlatformView is MauiTimePicker nativePicker)
            {
                // Replace the click handler so we control how the dialog is created
                nativePicker.Focusable = false;
                nativePicker.Clickable = true;
                nativePicker.SetOnClickListener(new TimePickerClickListener(handler));
            }
        });
    }

    private class TimePickerClickListener(ITimePickerHandler handler)
        : Java.Lang.Object, AndroidView.IOnClickListener
    {
        public void OnClick(AndroidView? v)
        {
            var currentTime = handler.VirtualView.Time ?? TimeSpan.Zero;

            var picker = new MaterialTimePicker.Builder()
                .SetInputMode(MaterialTimePicker.InputModeKeyboard)
                .SetTimeFormat(TimeFormat.Clock12h)
                .SetHour(currentTime.Hours)
                .SetMinute(currentTime.Minutes)
                .Build();

            picker.AddOnPositiveButtonClickListener(new TimeSelectedListener(handler, picker));

            if (Platform.CurrentActivity is AndroidX.AppCompat.App.AppCompatActivity activity)
            {
                picker.Show(activity.SupportFragmentManager, "time_picker");

                // After the dialog is laid out, set select-all-on-focus on the hour/minute EditTexts
                v?.Post(() =>
                {
                    var dialog = picker.Dialog;
                    if (dialog?.Window?.DecorView is AndroidView root)
                        SetSelectAllOnFocusForEditTexts(root);
                });
            }
        }
    }

    private static void SetSelectAllOnFocusForEditTexts(AndroidView root)
    {
        if (root is global::Android.Views.ViewGroup group)
        {
            for (int i = 0; i < group.ChildCount; i++)
            {
                var child = group.GetChildAt(i);
                if (child is EditText editText)
                    editText.SetSelectAllOnFocus(true);
                else if (child is global::Android.Views.ViewGroup)
                    SetSelectAllOnFocusForEditTexts(child!);
            }
        }
    }

    private class TimeSelectedListener(ITimePickerHandler handler, MaterialTimePicker picker)
        : Java.Lang.Object, AndroidView.IOnClickListener
    {
        public void OnClick(AndroidView? v)
        {
            handler.VirtualView.Time = new TimeSpan(picker.Hour, picker.Minute, 0);
        }
    }
}

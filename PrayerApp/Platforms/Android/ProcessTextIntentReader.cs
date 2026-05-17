namespace PrayerApp.Platforms.Android;

/// <summary>
/// Reads <c>EXTRA_PROCESS_TEXT</c> from a <c>PROCESS_TEXT</c> intent.
/// </summary>
/// <remarks>
/// Production share targets (Chrome, Gmail) supply a <c>CharSequence</c>;
/// <c>GetStringExtra</c> returns null for those payloads. Appium's
/// <c>am start --es</c> synthesis supplies a plain <c>String</c> extra instead —
/// accept both so UITests and real toolbar shares work.
/// </remarks>
internal static class ProcessTextIntentReader
{
    public static string? Read(Android.Content.Intent intent)
    {
        if (intent.Action != Android.Content.Intent.ActionProcessText)
            return null;

        if (!OperatingSystem.IsAndroidVersionAtLeast(23))
            return intent.GetStringExtra(Android.Content.Intent.ExtraProcessText);

        var fromCharSequence = intent.GetCharSequenceExtra(Android.Content.Intent.ExtraProcessText)?.ToString();
        if (!string.IsNullOrWhiteSpace(fromCharSequence))
            return fromCharSequence;

        return intent.GetStringExtra(Android.Content.Intent.ExtraProcessText);
    }
}

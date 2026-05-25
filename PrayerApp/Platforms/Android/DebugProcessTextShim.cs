#if DEBUG
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Text.Style;
using Java.Lang;

namespace PrayerApp.Platforms.Android;

/// <summary>
/// DEBUG-build-only broadcast receiver that lets UITests exercise the
/// <c>SpannableString</c> boundary of the <c>ACTION_PROCESS_TEXT</c> pipeline.
/// </summary>
/// <remarks>
/// <para>
/// <c>am start --es</c> only puts a plain <c>String</c> extra, so the existing
/// <c>LaunchProcessTextIntent</c> helper exercises only the String happy path.
/// Chrome and Gmail deliver <c>EXTRA_PROCESS_TEXT</c> as a <c>SpannableString</c>
/// (with markup spans attached to the selected rich text). Production at
/// <c>MauiProgram.HandleAndroidIntent</c> correctly uses <c>GetCharSequenceExtra</c>,
/// but a regression to <c>GetStringExtra</c> would silently drop the share — the
/// test suite needs a way to defend that boundary.
/// </para>
/// <para>
/// This receiver, on receiving a <c>PRAYER_TEST_SPANNABLE</c> broadcast with a
/// <c>text</c> string extra, constructs a real <c>SpannableString</c> payload
/// (with a <c>StyleSpan</c> attached) and re-dispatches via the real
/// <c>ACTION_PROCESS_TEXT</c> pipeline. The companion UITest helper
/// <c>LaunchProcessTextIntentSpannable</c> invokes this receiver via
/// <c>mobile: shell am broadcast</c>.
/// </para>
/// <para>
/// <c>Exported = true</c> is required because the <c>adb shell</c> caller runs
/// as the <c>shell</c> uid, not the app uid; an unexported receiver would not
/// be reachable. The receiver is wrapped in <c>#if DEBUG</c> so the
/// <c>[BroadcastReceiver]</c> attribute does NOT emit the <c>&lt;receiver&gt;</c>
/// element into the Release-build <c>AndroidManifest.xml</c>.
/// </para>
/// </remarks>
[BroadcastReceiver(
    Name = "com.multithreadedllc.prayercards.DebugProcessTextShim",
    Exported = true,
    Enabled = true)]
[IntentFilter(new[] { ActionInjectSpannable })]
public sealed class DebugProcessTextShim : BroadcastReceiver
{
    public const string ActionInjectSpannable = "com.multithreadedllc.prayercards.PRAYER_TEST_SPANNABLE";
    public const string ExtraText = "text";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent?.Action != ActionInjectSpannable) return;

        var raw = intent.GetStringExtra(ExtraText) ?? string.Empty;
        if (raw.Length == 0) return;

        // Mirror the payload shape Chrome / Gmail emit: a SpannableString with at least
        // one markup span attached. The specific span doesn't matter for the defense —
        // what matters is that the extra is a SpannableString, not a String.
        var spannable = new SpannableString(raw);
        spannable.SetSpan(
            new StyleSpan(TypefaceStyle.Bold),
            0,
            raw.Length,
            SpanTypes.ExclusiveExclusive);

        // Re-dispatch via the real PROCESS_TEXT pipeline so production code
        // (GetCharSequenceExtra in MauiProgram.HandleAndroidIntent) is exercised.
        // SetClassName targets the JNI-registered MainActivity name; matches the
        // value used by TestConfig.AndroidMainActivity on the UITest side.
        var dispatch = new Intent(Intent.ActionProcessText)
            .SetClassName(context.PackageName!, "crc6425c6d21f3599989c.MainActivity")
            .PutExtra(Intent.ExtraProcessText, (ICharSequence)spannable)
            .SetType("text/plain")
            .AddFlags(ActivityFlags.NewTask);

        context.StartActivity(dispatch);
    }
}
#endif

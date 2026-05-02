using Foundation;
using ObjCRuntime;
using UIKit;

namespace PrayerApp.ActionExtension;

/// <summary>
/// Slice 3a: Share Extension stub. Appears in the iOS Share sheet's top apps row when
/// text is selected (Mail, Notes, Safari, etc.). Shows a brief "Importing…" indicator,
/// then dismisses without doing anything yet. Real payload extraction + App Group write
/// lands in 3b; main-app handoff via custom URL scheme lands in 3c.
///
/// The bundle ID and project name still say "ActionExtension" — historical artifact
/// from before the pivot from Action Extension (com.apple.ui-services) to Share Extension
/// (com.apple.share-services). Reusing the bundle ID keeps the existing provisioning
/// profiles valid.
/// </summary>
[Register("ShareViewController")]
public class ShareViewController : UIViewController
{
    private const double DismissDelaySeconds = 0.8;

    public ShareViewController() : base()
    {
    }

    public ShareViewController(NativeHandle handle) : base(handle)
    {
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        BuildIndicatorUI();

        // Auto-dismiss after a brief pause. Slice 3b/3c will replace the timer with a
        // real payload-handoff sequence (extract NSItemProvider text → write to App Group
        // → openURL into main app → CompleteRequest).
        NSTimer.CreateScheduledTimer(DismissDelaySeconds, _ =>
        {
            ExtensionContext?.CompleteRequest(System.Array.Empty<NSExtensionItem>(), null);
        });
    }

    private void BuildIndicatorUI()
    {
        View!.BackgroundColor = UIColor.SystemBackground;

        var spinner = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Medium)
        {
            TranslatesAutoresizingMaskIntoConstraints = false,
            IsAccessibilityElement = false,
        };
        spinner.StartAnimating();

        var label = new UILabel
        {
            Text = "Importing prayer cards…",
            Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Body),
            TextColor = UIColor.Label,
            TextAlignment = UITextAlignment.Center,
            Lines = 0,
            TranslatesAutoresizingMaskIntoConstraints = false,
        };

        var stack = new UIStackView(new UIView[] { spinner, label })
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Spacing = 12,
            Alignment = UIStackViewAlignment.Center,
            TranslatesAutoresizingMaskIntoConstraints = false,
        };

        View.AddSubview(stack);
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            stack.CenterXAnchor.ConstraintEqualTo(View.CenterXAnchor),
            stack.CenterYAnchor.ConstraintEqualTo(View.CenterYAnchor),
            stack.LeadingAnchor.ConstraintGreaterThanOrEqualTo(View.LayoutMarginsGuide.LeadingAnchor),
            stack.TrailingAnchor.ConstraintLessThanOrEqualTo(View.LayoutMarginsGuide.TrailingAnchor),
        });

        // Announce to VoiceOver users — the auto-dismissing modal otherwise gives no
        // audible cue, since the sheet closes faster than VO would naturally read it.
        UIAccessibility.PostNotification(UIAccessibilityPostNotification.Announcement, (NSString)label.Text);
    }
}

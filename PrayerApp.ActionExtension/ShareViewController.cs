using System;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Foundation;
using ObjCRuntime;
using PrayerApp.Shared;
using UIKit;

namespace PrayerApp.ActionExtension;

/// <summary>
/// iOS Share Extension: captures the user's text selection from the Share sheet,
/// writes it as a JSON payload to the App Group container, then auto-launches
/// the host app via responder-chain → UIApplication.OpenUrl.
///
/// Class and bundle ID retain the "ActionExtension" name post-pivot from Action
/// to Share Extension — reusing the bundle ID keeps the existing provisioning
/// profiles valid.
///
/// AOT-clean shape: ViewDidLoad is synchronous; <see cref="NSItemProvider.LoadItem"/>
/// callback-form replaces <c>await LoadItemAsync</c> + <c>async void ViewDidLoad</c>.
/// Slice 3c's gate test under <c>-p:UseInterpreter=false</c> hung the scene host
/// inside the awaited continuation chain even with source-gen JSON in place; the
/// trimmer was cutting an async-state-machine continuation that wasn't statically
/// reachable. Eliminating async/await here removes that surface entirely.
/// </summary>
[Register("ShareViewController")]
public class ShareViewController : UIViewController
{
    private const string PlainTextUti = "public.plain-text";
    private const int MaxPayloadBytes = 256 * 1024;

    // PrayerApp brand muted green. Mirror in PrayerApp/Resources/Styles/Colors.xaml
    // (Primary) and PrayerApp.csproj's MauiSplashScreen Color — keep the three in sync.
    // Extension is a separate binary; can't reference XAML resources directly.
    private static readonly UIColor BrandGreen = UIColor.FromRGB(0x6B, 0x7D, 0x5A);

    private const string SuccessCaption = "Saving to Practicing Prayer…";
    private const string FailureCaption = "Couldn't save — try again";

    private UILabel? _statusLabel;

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
        StartImport();
    }

    private void StartImport()
    {
        var attachment = FindPlainTextAttachment();
        if (attachment is null)
        {
            FinishOnMainThread(payloadWritten: false);
            return;
        }

        // LoadItem's completion runs on whatever thread Foundation picks;
        // file IO is safe there directly, CompleteRequest must be hopped to main.
        attachment.LoadItem(PlainTextUti, null, (loaded, error) =>
        {
            bool payloadWritten = false;
            try
            {
                if (error is not null)
                {
                    Debug.WriteLine($"[ShareExt] LoadItem error: {error.LocalizedDescription}");
                    return;
                }

                var text = (loaded as NSString)?.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // NFC-normalize at the bridge boundary so payload bytes match
                    // canonicalized form. Some characters can grow under NFC (rare;
                    // some Hangul) so re-check the byte cap on the normalized form.
                    var normalized = text.Normalize(NormalizationForm.FormC);
                    if (System.Text.Encoding.UTF8.GetByteCount(normalized) <= MaxPayloadBytes)
                    {
                        payloadWritten = WriteToAppGroup(normalized);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ShareExt] payload pipeline failed: {ex}");
            }
            finally
            {
                FinishOnMainThread(payloadWritten);
            }
        });
    }

    private NSItemProvider? FindPlainTextAttachment()
    {
        var inputItems = ExtensionContext?.InputItems ?? Array.Empty<NSExtensionItem>();
        foreach (var item in inputItems)
        {
            var attachments = item.Attachments ?? Array.Empty<NSItemProvider>();
            foreach (var attachment in attachments)
            {
                if (attachment.HasItemConformingTo(PlainTextUti))
                    return attachment;
            }
        }
        return null;
    }

    private void FinishOnMainThread(bool payloadWritten)
    {
        // CompleteRequest must run on the main thread; the LoadItem callback can
        // resume on a non-main queue. UIViewController inherits BeginInvokeOnMainThread
        // from NSObject.
        BeginInvokeOnMainThread(() =>
        {
            var ctx = ExtensionContext;
            if (ctx is null) return;

            if (!payloadWritten)
            {
                if (_statusLabel is not null)
                    _statusLabel.Text = FailureCaption;
                ctx.CompleteRequest(Array.Empty<NSExtensionItem>(), null);
                return;
            }

            CompleteAndLaunchHost(ctx);
        });
    }

    /// <summary>
    /// Completes the extension request, then walks the responder chain in the
    /// completion handler to find a <see cref="UIApplication"/> and calls its
    /// 3-arg <c>openURL:options:completionHandler:</c> binding to foreground
    /// the host app via the registered <c>practicingprayer://</c> scheme.
    ///
    /// The <c>BeginInvokeOnMainThread</c> hop is load-bearing: CompleteRequest's
    /// completion fires on <c>com.apple.expiringTaskExecutionQueue</c>, and the
    /// .NET-for-iOS UIKit bindings call <c>UIApplication.EnsureUIThread()</c>
    /// on every UIResponder/UIApplication member access — including reading
    /// <c>UIResponder.NextResponder</c>. Off-main access throws
    /// <c>UIKitThreadAccessException</c> before any UIKit call reaches the
    /// system. Reddit/Swift samples of this pattern don't show the hop because
    /// Swift has no managed-side guard. Don't strip it.
    /// </summary>
    private void CompleteAndLaunchHost(NSExtensionContext ctx)
    {
        var hostUrl = new NSUrl($"{AppGroupConstants.HostAppScheme}://import");

        ctx.CompleteRequest(Array.Empty<NSExtensionItem>(), _ =>
        {
            BeginInvokeOnMainThread(() => OpenHostAppViaResponderChain(hostUrl));
        });
    }

    /// <summary>
    /// Walks the UIResponder chain starting from this view controller and
    /// invokes the 3-arg <c>openURL:options:completionHandler:</c> on the
    /// first <see cref="UIApplication"/> found. Caller must be on the main
    /// thread (see <see cref="CompleteAndLaunchHost"/>).
    /// </summary>
    private void OpenHostAppViaResponderChain(NSUrl url)
    {
        UIResponder? responder = this;
        while (responder is not null)
        {
            if (responder is UIApplication app)
            {
                app.OpenUrl(url, new NSDictionary(), null);
                return;
            }
            responder = responder.NextResponder;
        }
    }

    /// <summary>
    /// Returns true iff the payload was successfully written to the App Group
    /// container. False on entitlement misconfig, NSData encoding failure, or
    /// disk write failure.
    /// </summary>
    private static bool WriteToAppGroup(string text)
    {
        var container = NSFileManager.DefaultManager.GetContainerUrl(AppGroupConstants.AppGroupId);
        if (container is null)
        {
            Debug.WriteLine("[ShareExt] GetContainerUrl returned null. Entitlement misconfig?");
            return false;
        }

        var url = container.Append(AppGroupConstants.PayloadFileName, false);

        // Caller (LoadItem boundary) normalizes to NFC and enforces the byte cap.
        // System.Text.Json source-gen context keeps parity with the host's read
        // side (HandleAppGroupImport uses JsonDocument.Parse) and avoids
        // IL2026/IL3050 under AOT.
        var payload = new ImportPayload(text, DateTime.UtcNow.ToString("o"));
        var json = JsonSerializer.Serialize(payload, ImportPayloadJsonContext.Default.ImportPayload);

        var data = NSData.FromString(json, NSStringEncoding.UTF8);
        if (data is null)
        {
            Debug.WriteLine("[ShareExt] NSData.FromString returned null");
            AppGroupBreadcrumbLog.Append(container.Path!, BreadcrumbOutcome.IoFail, byteCount: -1);
            return false;
        }

        // atomically:true → sibling temp file, fsync, rename. Crash mid-write leaves
        // the prior file intact.
        if (data.Save(url, atomically: true))
        {
            AppGroupBreadcrumbLog.Append(container.Path!, BreadcrumbOutcome.WriteOk, byteCount: json.Length);
            return true;
        }

        Debug.WriteLine($"[ShareExt] NSData.Save returned false for {url.AbsoluteString}");
        AppGroupBreadcrumbLog.Append(container.Path!, BreadcrumbOutcome.IoFail, byteCount: -1);
        return false;
    }

    private void BuildIndicatorUI()
    {
        View!.BackgroundColor = BrandGreen;

        var spinner = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Medium)
        {
            TranslatesAutoresizingMaskIntoConstraints = false,
            IsAccessibilityElement = false,
            Color = UIColor.White,
        };
        spinner.StartAnimating();

        _statusLabel = new UILabel
        {
            Text = SuccessCaption,
            Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Body),
            TextColor = UIColor.White,
            TextAlignment = UITextAlignment.Center,
            Lines = 0,
            TranslatesAutoresizingMaskIntoConstraints = false,
        };

        var stack = new UIStackView(new UIView[] { spinner, _statusLabel })
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
        UIAccessibility.PostNotification(UIAccessibilityPostNotification.Announcement, (NSString)_statusLabel.Text);
    }
}

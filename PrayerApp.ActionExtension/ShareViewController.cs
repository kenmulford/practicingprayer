using System;
using System.Diagnostics;
using System.Text.Json;
using Foundation;
using ObjCRuntime;
using PrayerApp.Shared;
using UIKit;

namespace PrayerApp.ActionExtension;

/// <summary>
/// iOS Share Extension: captures the user's text selection from the Share sheet
/// and writes it as a JSON payload to the App Group container for the main app
/// to pick up.
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

    // Wakeup URL the responder-chain openURL: trick fires after a successful
    // payload write. iOS routes the scheme to the host app via Info.plist's
    // CFBundleURLTypes; Window.Activated → AppGroupImportDispatcher reads the
    // payload from the App Group container. Only the scheme matters for routing.
    private static readonly NSUrl HostAppWakeupUrl = new($"{AppGroupConstants.HostAppScheme}://import");

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
            FinishOnMainThread();
            return;
        }

        // Callback-form LoadItem instead of LoadItemAsync. The completion runs
        // on whatever thread Foundation picks; file IO and CompleteRequest are
        // both safe to dispatch from there (file IO directly, CompleteRequest
        // hopped to main).
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
                if (!string.IsNullOrWhiteSpace(text)
                    && System.Text.Encoding.UTF8.GetByteCount(text) <= MaxPayloadBytes)
                {
                    payloadWritten = WriteToAppGroup(text);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ShareExt] payload pipeline failed: {ex}");
            }
            finally
            {
                FinishOnMainThread(wakeHostApp: payloadWritten);
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

    private void FinishOnMainThread(bool wakeHostApp = false)
    {
        // CompleteRequest must run on the main thread; the LoadItem callback can
        // resume on a non-main queue. UIViewController inherits BeginInvokeOnMainThread
        // from NSObject. The optional host-app wakeup runs on the same hop, BEFORE
        // CompleteRequest tears down the extension context.
        BeginInvokeOnMainThread(() =>
        {
            if (wakeHostApp)
                TryWakeHostApp();
            ExtensionContext?.CompleteRequest(Array.Empty<NSExtensionItem>(), null);
        });
    }

    /// <summary>
    /// Walk the UIResponder chain looking for an object that responds to
    /// <c>openURL:</c> (single-argument selector). On Share Extensions,
    /// <see cref="UIApplication.SharedApplication"/> is unavailable; the
    /// responder-chain trick is the established pattern for asking iOS to
    /// open a URL. Tolerated by App Review for the share-extension wakeup
    /// pattern; the URL routes back to our own host app via the custom
    /// scheme registered in Info.plist.
    /// </summary>
    private void TryWakeHostApp()
    {
        try
        {
            var sel = new ObjCRuntime.Selector("openURL:");
            UIResponder? responder = this;
            while (responder is not null)
            {
                if (responder.RespondsToSelector(sel))
                {
                    responder.PerformSelector(sel, HostAppWakeupUrl);
                    return;
                }
                responder = responder.NextResponder;
            }
            Debug.WriteLine("[ShareExt] no responder in chain handles openURL:");
        }
        catch (Exception ex)
        {
            // Wakeup is best-effort — if it fails the user just falls back to
            // manually re-opening the app, which still works (Window.Activated
            // dispatcher reads the pending payload).
            Debug.WriteLine($"[ShareExt] TryWakeHostApp failed: {ex}");
        }
    }

    /// <summary>
    /// Returns true iff the payload was successfully written to the App Group
    /// container. False on entitlement misconfig, NSData encoding failure, or
    /// disk write failure. Caller uses the bool to gate the host-app wakeup.
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

        // System.Text.Json source-gen context keeps parity with the 3c read side
        // (HandleAppGroupImport uses JsonDocument.Parse) and avoids IL2026/IL3050
        // under AOT.
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

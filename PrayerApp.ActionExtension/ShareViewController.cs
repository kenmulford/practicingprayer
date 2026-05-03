using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
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
/// </summary>
[Register("ShareViewController")]
public class ShareViewController : UIViewController
{
    private const string PlainTextUti = "public.plain-text";
    private const int MaxPayloadBytes = 256 * 1024;

    public ShareViewController() : base()
    {
    }

    public ShareViewController(NativeHandle handle) : base(handle)
    {
    }

    public override async void ViewDidLoad()
    {
        base.ViewDidLoad();
        BuildIndicatorUI();

        try
        {
            var text = await ExtractPlainTextAsync();

            if (!string.IsNullOrWhiteSpace(text)
                && System.Text.Encoding.UTF8.GetByteCount(text) <= MaxPayloadBytes)
            {
                WriteToAppGroup(text);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ShareExt] payload pipeline failed: {ex}");
        }
        finally
        {
            // CompleteRequest must run on the main thread; LoadItemAsync's continuation
            // can resume on a non-main queue. UIViewController inherits BeginInvokeOnMainThread
            // from NSObject.
            BeginInvokeOnMainThread(() =>
            {
                ExtensionContext?.CompleteRequest(Array.Empty<NSExtensionItem>(), null);
            });
        }
    }

    private async Task<string?> ExtractPlainTextAsync()
    {
        var inputItems = ExtensionContext?.InputItems ?? Array.Empty<NSExtensionItem>();
        foreach (var item in inputItems)
        {
            var attachments = item.Attachments ?? Array.Empty<NSItemProvider>();
            foreach (var attachment in attachments)
            {
                if (!attachment.HasItemConformingTo(PlainTextUti))
                    continue;

                var loaded = await attachment.LoadItemAsync(PlainTextUti, null);
                return (loaded as NSString)?.ToString();
            }
        }
        return null;
    }

    private static void WriteToAppGroup(string text)
    {
        var container = NSFileManager.DefaultManager.GetContainerUrl(AppGroupConstants.AppGroupId);
        if (container is null)
        {
            Debug.WriteLine("[ShareExt] GetContainerUrl returned null. Entitlement misconfig?");
            return;
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
            return;
        }

        // atomically:true → sibling temp file, fsync, rename. Crash mid-write leaves
        // the prior file intact.
        if (data.Save(url, atomically: true))
        {
            AppGroupBreadcrumbLog.Append(container.Path!, BreadcrumbOutcome.WriteOk, byteCount: json.Length);
        }
        else
        {
            Debug.WriteLine($"[ShareExt] NSData.Save returned false for {url.AbsoluteString}");
            AppGroupBreadcrumbLog.Append(container.Path!, BreadcrumbOutcome.IoFail, byteCount: -1);
        }
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

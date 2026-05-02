using Foundation;
using ObjCRuntime;
using UIKit;

namespace PrayerApp.ActionExtension;

/// <summary>
/// Slice 3a: empty Action Extension. Registers in the iOS Share sheet for text selections,
/// then exits silently when invoked. Real payload extraction + App Group write lands in 3b;
/// main-app handoff via custom URL scheme lands in 3c.
/// </summary>
[Register("ActionViewController")]
public class ActionViewController : UIViewController
{
    public ActionViewController() : base()
    {
    }

    public ActionViewController(NativeHandle handle) : base(handle)
    {
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        // 3a: silent exit. No UI, no logic. Validates that the .appex bundles, signs, and
        // registers as a UI service so iOS shows it in the Share sheet for text selections.
        ExtensionContext?.CompleteRequest(System.Array.Empty<NSExtensionItem>(), null);
    }
}

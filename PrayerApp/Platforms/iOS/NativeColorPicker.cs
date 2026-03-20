using UIKit;
using Foundation;
using Microsoft.Maui.Platform;
using System.Runtime.InteropServices;

namespace PrayerApp.Platforms.iOS;

/// <summary>
/// Presents the native iOS UIColorPickerViewController and returns the selected color.
/// </summary>
public static class NativeColorPicker
{
    public static Task<Color?> PickColorAsync(Color? initialColor = null)
    {
        var tcs = new TaskCompletionSource<Color?>();

        var picker = new UIColorPickerViewController
        {
            SupportsAlpha = false,
            ModalPresentationStyle = UIModalPresentationStyle.PageSheet
        };

        if (initialColor is not null)
        {
            picker.SelectedColor = initialColor.ToPlatform();
        }

        var del = new ColorPickerDelegate(tcs);
        picker.Delegate = del;

        // Keep a strong reference to the delegate so it's not GC'd
        var handle = GCHandle.Alloc(del);

        picker.PresentationController!.Delegate = new DismissDelegate(() =>
        {
            // Cancelled by swiping down without picking
            if (!tcs.Task.IsCompleted)
                tcs.TrySetResult(null);
            handle.Free();
        });

        var rootVc = GetRootViewController();
        if (rootVc is null)
        {
            tcs.SetResult(null);
            return tcs.Task;
        }

        rootVc.PresentViewController(picker, true, null);
        return tcs.Task;
    }

    private static UIViewController? GetRootViewController()
    {
        var scene = UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .FirstOrDefault();
        return scene?.Windows.FirstOrDefault(w => w.IsKeyWindow)?.RootViewController;
    }

    private class ColorPickerDelegate : UIColorPickerViewControllerDelegate
    {
        private readonly TaskCompletionSource<Color?> _tcs;

        public ColorPickerDelegate(TaskCompletionSource<Color?> tcs)
        {
            _tcs = tcs;
        }

        public override void DidSelectColor(UIColorPickerViewController viewController, UIColor color, bool continuously)
        {
            // Only capture the final selection (not continuous updates)
            if (continuously) return;
        }

        public override void DidFinish(UIColorPickerViewController viewController)
        {
            var uiColor = viewController.SelectedColor;
            uiColor.GetRGBA(out var r, out var g, out var b, out _);
            var hex = $"#{(int)(r * 255):X2}{(int)(g * 255):X2}{(int)(b * 255):X2}";
            _tcs.TrySetResult(Color.FromArgb(hex));
            viewController.DismissViewController(true, null);
        }
    }

    private class DismissDelegate : UIAdaptivePresentationControllerDelegate
    {
        private readonly Action _onDismiss;

        public DismissDelegate(Action onDismiss)
        {
            _onDismiss = onDismiss;
        }

        public override void DidDismiss(UIPresentationController presentationController)
        {
            _onDismiss();
        }
    }
}

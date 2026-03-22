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
            // Sheet was swiped down. Use the last selected color if the user
            // made a selection, or null if they never touched a color.
            if (!tcs.Task.IsCompleted)
                tcs.TrySetResult(del.LastSelectedColor);
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

        // Track the last-selected color so we can return it on dismiss.
        // On iOS 26, DidFinish may not fire or may report a stale SelectedColor.
        // DidSelectColor(continuously: false) is the reliable final-selection signal.
        private Color? _lastSelectedColor;

        public ColorPickerDelegate(TaskCompletionSource<Color?> tcs)
        {
            _tcs = tcs;
        }

        public override void DidSelectColor(UIColorPickerViewController viewController, UIColor color, bool continuously)
        {
            // Capture every selection (continuous and final) so we always have the
            // most recent color when the picker is dismissed.
            color.GetRGBA(out var r, out var g, out var b, out _);
            var hex = $"#{(int)(r * 255):X2}{(int)(g * 255):X2}{(int)(b * 255):X2}";
            _lastSelectedColor = Color.FromArgb(hex);
        }

        public override void DidFinish(UIColorPickerViewController viewController)
        {
            // Use the tracked selection — more reliable than re-reading SelectedColor
            _tcs.TrySetResult(_lastSelectedColor);
            viewController.DismissViewController(true, null);
        }

        /// <summary>
        /// Called by the dismiss delegate when the sheet is swiped down.
        /// Returns the last selected color (if any), treating swipe-down as confirm.
        /// </summary>
        public Color? LastSelectedColor => _lastSelectedColor;
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

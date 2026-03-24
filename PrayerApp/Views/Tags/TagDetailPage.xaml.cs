using PrayerApp.ViewModels;
#if ANDROID
using Android.Views;
#elif IOS
using UIKit;
#endif

namespace PrayerApp.Views.Tags
{
    public partial class TagDetailPage : ContentPage
    {
        public TagDetailPage(TagDetailViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
        }

        private void OnSwatchLoaded(object? sender, EventArgs e)
        {
            if (sender is not Grid grid) return;

            // Handler may not be set yet at Loaded time — hook HandlerChanged
            if (grid.Handler is not null)
                AttachNativeLongPress(grid);
            else
                grid.HandlerChanged += OnSwatchHandlerChanged;
        }

        private void OnSwatchHandlerChanged(object? sender, EventArgs e)
        {
            if (sender is Grid grid && grid.Handler is not null)
            {
                grid.HandlerChanged -= OnSwatchHandlerChanged;
                AttachNativeLongPress(grid);
            }
        }

        private static void AttachNativeLongPress(Grid grid)
        {
#if ANDROID
            if (grid.Handler?.PlatformView is Android.Views.View nativeView)
            {
                // Handle BOTH tap and long-press natively on Android.
                // MAUI's TapGestureRecognizer and the native GestureDetector
                // corrupt each other's touch pipelines when both are active.
                // By owning the full touch sequence natively (OnDown → true,
                // args.Handled = detector result), we avoid all conflicts.
                // The XAML TapGestureRecognizer remains for iOS where no
                // native handler is attached.
                var listener = new SwatchGestureListener(grid);
                var detector = new GestureDetector(nativeView.Context, listener);
                nativeView.Touch += (s, args) =>
                {
                    args.Handled = detector.OnTouchEvent(args.Event!);
                };
            }
#elif IOS
            if (grid.Handler?.PlatformView is UIView nativeView)
            {
                var longPress = new UILongPressGestureRecognizer(r =>
                {
                    if (r.State == UIGestureRecognizerState.Began &&
                        grid.BindingContext is ColorSwatchViewModel vm &&
                        vm.DeleteCommand.CanExecute(null))
                    {
                        MainThread.BeginInvokeOnMainThread(() => vm.DeleteCommand.Execute(null));
                    }
                });
                longPress.MinimumPressDuration = 0.5; // 500ms, matches prior LongPressDuration
                nativeView.AddGestureRecognizer(longPress);
            }
#endif
        }

#if ANDROID
        /// <summary>
        /// Handles both tap (select) and long-press (delete) natively on Android.
        /// MAUI's TapGestureRecognizer and native GestureDetector corrupt each
        /// other's touch pipelines, so we handle everything at the native level.
        /// </summary>
        private sealed class SwatchGestureListener : GestureDetector.SimpleOnGestureListener
        {
            private readonly Grid _grid;
            public SwatchGestureListener(Grid grid) => _grid = grid;

            public override bool OnDown(MotionEvent? e) => true;

            public override bool OnSingleTapUp(MotionEvent? e)
            {
                if (_grid.BindingContext is ColorSwatchViewModel vm)
                    vm.SelectCommand.Execute(null);
                return true;
            }

            public override void OnLongPress(MotionEvent? e)
            {
                if (_grid.BindingContext is ColorSwatchViewModel vm &&
                    vm.DeleteCommand.CanExecute(null))
                {
                    MainThread.BeginInvokeOnMainThread(() => vm.DeleteCommand.Execute(null));
                }
            }
        }
#endif
    }
}

using CommunityToolkit.Maui.Extensions;
using PrayerApp.ViewModels;
#if ANDROID
using Android.Views;
#endif

namespace PrayerApp.Views.PrayerCard;

public partial class PrayerCardsPage : ContentPage
{
    private bool _loaded;

    public PrayerCardsPage(PrayerCardsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        vm.HighlightCardRequested += OnHighlightCardRequested;
        vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PrayerCardsViewModel.IsMultiSelectMode)) return;
        ApplyMultiSelectToolbarState((PrayerCardsViewModel)sender!);
    }

    /// <summary>
    /// Mutates the single overflow toolbar item between "More" (opens popup) and
    /// "Cancel" (exits multi-select). <b>AutomationId is never reassigned</b> — MAUI's
    /// `BindableProperty` enforces set-once on AutomationId and throws on mutation.
    /// Only Text/Icon/SemanticProperties change between modes. Screen-reader labels
    /// degrade slightly on Android in multi-select mode (TalkBack announces "More"
    /// instead of "Cancel" because Shell ToolbarItem on Android uses AutomationId
    /// as contentDescription and ignores SemanticProperties.Description). Acceptable
    /// trade-off — the Cards_Bar_MultiSelect Border below carries the mode context.
    /// </summary>
    private void ApplyMultiSelectToolbarState(PrayerCardsViewModel vm)
    {
        var item = ToolbarItems.FirstOrDefault();
        if (item is null) return;

        if (vm.IsMultiSelectMode)
        {
            item.Text = "Cancel";
            item.IconImageSource = "xmark_solid_full.png";
            SemanticProperties.SetDescription(item, "Cancel");
            SemanticProperties.SetHint(item, "Exit multi-select mode");
        }
        else
        {
            item.Text = "More";
            item.IconImageSource = "ellipsis_vertical_solid_full.png";
            SemanticProperties.SetDescription(item, "More actions");
            SemanticProperties.SetHint(item, "Opens a menu with Add Card, Manage Collections, and Select");
        }
    }

    private async void OnHighlightCardRequested(object? sender, PrayerCardViewModel card)
    {
        // ScrollTo with grouped CollectionView uses the group + item overload
        var vm = BindingContext as PrayerCardsViewModel;
        var section = vm?.BoxSections.FirstOrDefault(s => s.Contains(card));
        if (section != null)
            cardCollection.ScrollTo(card, section, ScrollToPosition.Center, animate: true);
        else
            cardCollection.ScrollTo(card, position: ScrollToPosition.Center, animate: true);

        SemanticScreenReader.Announce($"New card: {card.Title}");

        await Task.Delay(2500);
        card.IsHighlighted = false;
    }

    private void OnSectionHeaderTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is BoxSectionViewModel section)
        {
            section.IsExpanded = !section.IsExpanded;

            // Persist expansion state so it survives app restarts
            if (BindingContext is PrayerCardsViewModel vm)
                vm.SaveSectionExpansionState();
        }
    }

    private void OnCardBorderLoaded(object? sender, EventArgs e)
    {
        if (sender is not Border border) return;

        // Margin is a Thickness (not animatable by FadeTo/TranslateTo) — tween via the low-level
        // Animation class. Initial margin is set without animation to avoid a load-in jump.
        if (border.BindingContext is PrayerCardViewModel card)
        {
            border.Margin = CardMarginFor(card.IsExpanded);

            System.ComponentModel.PropertyChangedEventHandler handler = (_, ev) =>
            {
                if (ev.PropertyName == nameof(PrayerCardViewModel.IsExpanded) && border.BindingContext is PrayerCardViewModel c)
                    AnimateCardMargin(border, CardMarginFor(c.IsExpanded));
            };
            card.PropertyChanged += handler;

            // Unsubscribe on Unloaded so CollectionView recycling doesn't leak. MAUI
            // guarantees Loaded/Unloaded alternate — no double-subscription guard needed.
            void OnUnloaded(object? _, EventArgs __)
            {
                card.PropertyChanged -= handler;
                border.Unloaded -= OnUnloaded;
            }
            border.Unloaded += OnUnloaded;
        }

#if !ANDROID
        // iOS: TouchBehavior on the Border coexists with child tap gestures natively.
        if (BindingContext is not PrayerCardsViewModel vm) return;
        var behavior = border.Behaviors.OfType<CommunityToolkit.Maui.Behaviors.TouchBehavior>().FirstOrDefault();
        if (behavior is null)
        {
            behavior = new CommunityToolkit.Maui.Behaviors.TouchBehavior
            {
                LongPressDuration = 500,
                ShouldMakeChildrenInputTransparent = false
            };
            border.Behaviors.Add(behavior);
        }
        behavior.LongPressCommand = vm.LongPressCardCommand;
        behavior.SetBinding(CommunityToolkit.Maui.Behaviors.TouchBehavior.LongPressCommandParameterProperty,
            new Binding("BindingContext", source: border));
#endif
    }

    private static Thickness CardMarginFor(bool expanded)
        => expanded ? new Thickness(0, 8) : new Thickness(14, 0, 0, 0);

    private static void AnimateCardMargin(Border border, Thickness target)
    {
        // Android respects system animation settings automatically; no reduced-motion guard needed.
        var from = border.Margin;
        var tween = new Animation(v => border.Margin = new Thickness(
            from.Left   + (target.Left   - from.Left)   * v,
            from.Top    + (target.Top    - from.Top)    * v,
            from.Right  + (target.Right  - from.Right)  * v,
            from.Bottom + (target.Bottom - from.Bottom) * v), 0, 1);
        tween.Commit(border, "CardMarginTween", rate: 16, length: 200, easing: Easing.CubicInOut);
    }

    // BUG-60: On Android, MAUI TapGestureRecognizer and native GestureDetector conflict.
    // Handle both tap and long-press natively via GestureDetector on the header Grid,
    // following the proven pattern from TagDetailPage.
    private void OnCardHeaderLoaded(object? sender, EventArgs e)
    {
#if ANDROID
        if (sender is not Grid grid) return;
        // Handler may not be set at Loaded time — use HandlerChanged fallback
        if (grid.Handler is not null)
            AttachNativeCardGestures(grid);
        else
            grid.HandlerChanged += OnCardHeaderHandlerChanged;
#endif
    }

#if ANDROID
    private void OnCardHeaderHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is Grid grid && grid.Handler is not null)
        {
            grid.HandlerChanged -= OnCardHeaderHandlerChanged;
            AttachNativeCardGestures(grid);
        }
    }

    private void AttachNativeCardGestures(Grid grid)
    {
        if (grid.Handler?.PlatformView is not Android.Views.View nativeView) return;
        if (BindingContext is not PrayerCardsViewModel vm) return;

        // Remove MAUI's TapGestureRecognizer — it conflicts with native GestureDetector.
        // Both tap and long-press are handled natively via CardGestureListener.
        grid.GestureRecognizers.Clear();

        var listener = new CardGestureListener(grid, vm);
        var detector = new GestureDetector(nativeView.Context, listener);
        nativeView.Touch += (s, args) =>
        {
            args.Handled = detector.OnTouchEvent(args.Event!);
        };
    }

    /// <summary>
    /// Handles both tap and long-press natively on Android.
    /// MAUI's TapGestureRecognizer and native GestureDetector conflict when both are
    /// active, so we own the full touch sequence at the native level.
    /// The XAML TapGestureRecognizer on the header Grid is effectively bypassed on Android.
    /// </summary>
    private sealed class CardGestureListener : GestureDetector.SimpleOnGestureListener
    {
        private readonly Grid _grid;
        private readonly PrayerCardsViewModel _vm;

        public CardGestureListener(Grid grid, PrayerCardsViewModel vm)
        {
            _grid = grid;
            _vm = vm;
        }

        public override bool OnDown(MotionEvent? e) => true;

        public override bool OnSingleTapUp(MotionEvent? e)
        {
            if (_grid.BindingContext is PrayerCardViewModel card)
            {
                if (_vm.IsMultiSelectMode)
                    _vm.ToggleCardSelection(card);
                else
                    card.ToggleExpandedCommand.Execute(null);
            }
            return true;
        }

        public override void OnLongPress(MotionEvent? e)
        {
            if (_grid.BindingContext is PrayerCardViewModel card)
                MainThread.BeginInvokeOnMainThread(() => _vm.LongPressCardCommand.Execute(card));
        }
    }
#endif

    private void OnCardHeaderTapped(object? sender, TappedEventArgs e)
    {
        // On Android, tap is handled natively by CardGestureListener (BUG-60).
        // This handler still fires on iOS where the XAML TapGestureRecognizer is active.
        if (sender is not Grid grid || grid.BindingContext is not PrayerCardViewModel card) return;
        if (BindingContext is PrayerCardsViewModel vm && vm.IsMultiSelectMode)
            vm.ToggleCardSelection(card);
        else
            card.ToggleExpandedCommand.Execute(null);
    }

    /// <summary>
    /// The check circle (left of the card in multi-select mode) toggles selection.
    /// Card body tap already toggles via OnCardHeaderTapped / CardGestureListener; this
    /// handler covers users who aim at the circle specifically (iOS Mail pattern).
    /// </summary>
    private void OnCheckCircleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Image img && img.BindingContext is PrayerCardViewModel card
            && BindingContext is PrayerCardsViewModel vm && vm.IsMultiSelectMode)
        {
            vm.ToggleCardSelection(card);
        }
    }

    /// <summary>
    /// Overflow toolbar handler. In multi-select mode, tapping exits multi-select
    /// directly (fast path, no popup). Otherwise opens the CardsOverflowPopup with
    /// the three actions (Add Card / Manage Collections / Select Cards).
    /// </summary>
    private async void OnOverflowTapped(object? sender, EventArgs e)
    {
        if (BindingContext is not PrayerCardsViewModel vm) return;

        if (vm.IsMultiSelectMode)
        {
            vm.CancelMultiSelectCommand.Execute(null);
            return;
        }

        await this.ShowPopupAsync(new CardsOverflowPopup(vm), null, CancellationToken.None);
    }

    private void OnSearchButtonPressed(object? sender, EventArgs e)
        => searchBar.Unfocus();

    private void OnBackgroundTapped(object? sender, TappedEventArgs e)
    {
        if (searchBar.IsFocused) searchBar.Unfocus();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await App.InitTask; // ensure DB seeding is complete before loading data
        if (BindingContext is PrayerCardsViewModel vm)
        {
            if (!_loaded)
            {
                _loaded = true;
                await vm.LoadAsync();
            }
            else
            {
                // Subsequent visits — refresh data that may have changed on other tabs
                // (e.g. prayers added via QuickAdd on home page)
                await vm.RefreshAsync();
            }
        }
    }
}

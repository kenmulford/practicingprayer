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

    /// <summary>
    /// Hides the "Select" toolbar item when multi-select is already active.
    /// ToolbarItem has no IsVisible binding in MAUI Shell, so we toggle it from code-behind.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PrayerCardsViewModel.IsMultiSelectMode)) return;
        var vm = (PrayerCardsViewModel)sender!;
        var selectItem = ToolbarItems.FirstOrDefault(t => t.AutomationId == "Cards_Btn_Select");
        if (vm.IsMultiSelectMode)
        {
            if (selectItem != null) ToolbarItems.Remove(selectItem);
        }
        else
        {
            if (selectItem == null)
            {
                var item = new ToolbarItem
                {
                    Text = "Select",
                    AutomationId = "Cards_Btn_Select",
                    Order = ToolbarItemOrder.Primary,
                    Priority = 1
                };
                SemanticProperties.SetHint(item, "Enter multi-select mode to select multiple cards");
                item.SetBinding(MenuItem.CommandProperty, new Binding(nameof(PrayerCardsViewModel.EnterMultiSelectCommand)));
                ToolbarItems.Insert(1, item);
            }
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
#if !ANDROID
        // iOS: TouchBehavior on the Border coexists with child tap gestures natively.
        if (sender is not Border border || BindingContext is not PrayerCardsViewModel vm) return;
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

    private async void OnCollectionsTapped(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(Routes.BoxesPage);

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

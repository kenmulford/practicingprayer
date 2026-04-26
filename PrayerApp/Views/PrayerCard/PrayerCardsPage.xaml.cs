using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using PrayerApp.Helpers;
using PrayerApp.Services;
using PrayerApp.ViewModels;
#if ANDROID
using Android.Views;
#endif

namespace PrayerApp.Views.PrayerCard;

public partial class PrayerCardsPage : ContentPage
{
    public PrayerCardsPage(PrayerCardsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;

        // PERF-9 probes — confirm whether Shell pop re-handlers the page or the CollectionView.
        // Page-level firings without CV-level firings → page handler torn down + recreated.
        // CV-level firings without page-level → only the CollectionView is re-handlered.
        // Both → full view-tree teardown. Neither during Save→Cards → re-inflation has a different cause.
        // Strip with Slice 5 PerfLog cleanup.
        HandlerChanging += (_, e) => PerfLog.Log($"PrayerCardsPage.HandlerChanging old={(e.OldHandler != null ? "set" : "null")} new={(e.NewHandler != null ? "set" : "null")}");
        HandlerChanged  += (_, _) => PerfLog.Log($"PrayerCardsPage.HandlerChanged handler={(Handler != null ? "set" : "null")}");
        cardCollection.HandlerChanging += (_, e) => PerfLog.Log($"cardCollection.HandlerChanging old={(e.OldHandler != null ? "set" : "null")} new={(e.NewHandler != null ? "set" : "null")}");
        cardCollection.HandlerChanged  += (_, _) => PerfLog.Log($"cardCollection.HandlerChanged handler={(cardCollection.Handler != null ? "set" : "null")}");

        // PERF-10 probes — find the parent-layout-invalidation source that triggers the
        // RecyclerView re-inflate cascade. PERF-9 ruled out handler recycling; the cascade
        // must come from a layout-pass invalidation on a parent that survives. Candidates:
        // Shell pop animation re-measuring the page root, BoxSections reassignment hitting
        // a frame contended with animation, or a child SizeChanged propagating up. These
        // probes timestamp every relevant signal so the device log can show what fires
        // immediately before each OnCardBorderLoaded burst.
        // Strip with Slice 5 PerfLog cleanup.
        SizeChanged += (_, _) => PerfLog.Log($"PrayerCardsPage.SizeChanged w={Width:F0} h={Height:F0}");
        cardCollection.SizeChanged += (_, _) => PerfLog.Log($"cardCollection.SizeChanged w={cardCollection.Width:F0} h={cardCollection.Height:F0}");
        cardCollection.MeasureInvalidated += (_, _) => PerfLog.Log("cardCollection.MeasureInvalidated");
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
    /// Icon files are selected per-theme because Shell.ForegroundColor doesn't
    /// reliably tint ToolbarItem bitmaps on Android (the SVG-rasterized PNG is
    /// baked black); the *_dark variants have explicit light fills.
    /// </summary>
    private void ApplyMultiSelectToolbarState(PrayerCardsViewModel vm)
    {
        var item = ToolbarItems.FirstOrDefault();
        if (item is null) return;

        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

        if (vm.IsMultiSelectMode)
        {
            item.Text = "Cancel";
            item.IconImageSource = isDark ? "xmark_solid_full_dark.png" : "xmark_solid_full.png";
            SemanticProperties.SetDescription(item, "Cancel");
            SemanticProperties.SetHint(item, "Exit multi-select mode");
        }
        else
        {
            item.Text = "More";
            item.IconImageSource = isDark
                ? "ellipsis_vertical_solid_full_dark.png"
                : "ellipsis_vertical_solid_full.png";
            SemanticProperties.SetDescription(item, "More actions");
            SemanticProperties.SetHint(item, "Opens a menu with Add Card, Manage Collections, and Select");
        }
    }

    /// <summary>
    /// Scroll to a freshly-saved card. Must yield two dispatcher ticks first so the
    /// platform CollectionView adapter has committed the BoxSections rebuild —
    /// calling ScrollTo while the adapter snapshot is stale throws
    /// IllegalArgumentException "Invalid target position" on Android.
    /// </summary>
    /// <remarks>
    /// Slice 6g split: this method does the awaited part (layout drain + scroll +
    /// announce) so OnAppearing can lower IsAwaitingSavedCard immediately after.
    /// The 2.5 s highlight fade runs in the background via FadeHighlightAfterDelayAsync
    /// and must NOT block the overlay-off transition.
    /// </remarks>
    private async Task ScrollToSavedCardAsync(PrayerCardsViewModel vm, PrayerCardViewModel card)
    {
        PerfLog.Log("ScrollToSavedCardAsync.entry");
        await Dispatcher.DrainLayoutPassAsync();
        PerfLog.Log("ScrollToSavedCardAsync.after DrainLayoutPassAsync");

        try
        {
            var section = vm.BoxSections.FirstOrDefault(s => s.Contains(card));
            if (section != null)
                cardCollection.ScrollTo(card, section, ScrollToPosition.Center, animate: true);
            else
                cardCollection.ScrollTo(card, position: ScrollToPosition.Center, animate: true);
            PerfLog.Log("ScrollToSavedCardAsync.after ScrollTo");

            SemanticScreenReader.Announce($"New card: {card.Title}");
        }
        catch (Exception ex)
        {
            Diagnostics.ResolveLog()?.Log("PrayerCardsPage.ScrollToSavedCardAsync", ex);
        }
    }

    private static async Task FadeHighlightAfterDelayAsync(PrayerCardViewModel card)
    {
        await Task.Delay(2500);
        card.IsHighlighted = false;
        PerfLog.Log("FadeHighlightAfterDelayAsync.exit");
    }

    private void OnSectionHeaderTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is BoxSectionViewModel section)
        {
            section.IsExpanded = !section.IsExpanded;

            SemanticScreenReader.Announce(section.IsExpanded
                ? $"Expanded {section.Name}"
                : $"Collapsed {section.Name}");

            // Persist expansion state so it survives app restarts
            if (BindingContext is PrayerCardsViewModel vm)
                vm.SaveSectionExpansionState();
        }
    }

    private void OnCardBorderLoaded(object? sender, EventArgs e)
    {
        if (sender is not Border border) return;
        var initialId = (border.BindingContext as PrayerCardViewModel)?.Id ?? -1;
        PerfLog.Log($"OnCardBorderLoaded.entry id={initialId}");

        // Slice 6c real (PERF-10): The ExpandedSubtreeHost ContentView is realized
        // on demand from the page-level CardExpandedSubtreeTemplate. Reference is
        // captured once here — the same Border (and therefore the same host)
        // persists across CollectionView cell recycling.
        var expandedHost = border.FindByName<ContentView>("ExpandedSubtreeHost");

        // Margin is a Thickness (not animatable by FadeTo/TranslateTo) — tween via the low-level
        // Animation class. CollectionView recycles Borders by swapping BindingContext *without*
        // firing Loaded/Unloaded, so we must re-subscribe on BindingContextChanged — otherwise a
        // recycled Border stays subscribed to its previous card and animates margins driven by the
        // wrong card's IsExpanded changes (cards appear at the wrong indent after a tap).
        PrayerCardViewModel? subscribed = null;
        System.ComponentModel.PropertyChangedEventHandler handler = (_, ev) =>
        {
            if (ev.PropertyName != nameof(PrayerCardViewModel.IsExpanded) || subscribed is null) return;
            // Realize before the margin tween so chips/list have a layout pass concurrent
            // with the animation, not after — avoids "blank then content" flash on expand.
            if (subscribed.IsExpanded) RealizeExpandedSubtree(expandedHost, subscribed);
            AnimateCardMargin(border, CardMarginFor(subscribed.IsExpanded));
        };

        void Rebind()
        {
            var newId = (border.BindingContext as PrayerCardViewModel)?.Id ?? -1;
            PerfLog.Log($"Rebind.fire prev={subscribed?.Id ?? -1} new={newId}");
            if (subscribed is not null) subscribed.PropertyChanged -= handler;
            subscribed = border.BindingContext as PrayerCardViewModel;
            if (subscribed is not null)
            {
                subscribed.PropertyChanged += handler;
                // Snap (no tween) to the new card's state so recycled borders don't animate from
                // the previous card's margin. Skip the assignment if the value already matches —
                // every Margin write invalidates parent layout, and on Android that schedules
                // the next measure pass, which loads the next cell, which calls Rebind again
                // (cascade). The XAML default is the collapsed Margin so first-Loaded on a
                // collapsed card here is a no-op; only an expanded card or a recycled cell with
                // a state flip writes the property.
                border.AbortAnimation("CardMarginTween");
                var target = CardMarginFor(subscribed.IsExpanded);
                if (border.Margin != target)
                    border.Margin = target;
                // Rebind() is called both from OnCardBorderLoaded (fresh cell) and from
                // BindingContextChanged (recycled cell). For a fresh expanded cell —
                // the load path that matters most for save→Cards — host.Content is null
                // here and this realizes the chips/list/button before first paint.
                // For a recycled cell whose host already has Content, Realize is an
                // idempotent no-op and the inner bindings re-evaluate against the new
                // BindingContext. M1 fallback if BindableLayout misbehaves on
                // ItemsSource swap: prepend `expandedHost.Content = null;` to force
                // a rebuild rather than rely on inner-binding re-evaluation.
                if (subscribed.IsExpanded) RealizeExpandedSubtree(expandedHost, subscribed);
            }
            PerfLog.Log($"Rebind.exit id={newId}");
        }

        Rebind();
        border.BindingContextChanged -= OnBindingContextChanged;
        border.BindingContextChanged += OnBindingContextChanged;
        void OnBindingContextChanged(object? _, EventArgs __) => Rebind();

        void OnUnloaded(object? _, EventArgs __)
        {
            if (subscribed is not null) subscribed.PropertyChanged -= handler;
            border.BindingContextChanged -= OnBindingContextChanged;
            border.Unloaded -= OnUnloaded;
        }
        border.Unloaded -= OnUnloaded;
        border.Unloaded += OnUnloaded;

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

    // Const'd so a XAML rename surfaces at build-time instead of as a silent runtime no-op.
    private const string ExpandedSubtreeTemplateKey = "CardExpandedSubtreeTemplate";
    private DataTemplate? _expandedSubtreeTemplate;

    /// <summary>
    /// Lazily inflates the chips + prayer-list + add-prayer subtree from the
    /// page-level CardExpandedSubtreeTemplate resource. Idempotent — no-op if
    /// Content is already set. The explicit BindingContext write defends against
    /// propagation timing edge cases where the host's context might lead the
    /// inheritance chain by a frame on first realization.
    /// </summary>
    /// <remarks>
    /// `View` is fully qualified because the code-behind imports `Android.Views`
    /// (for native gesture handling on Android), which clashes on the bare name.
    /// </remarks>
    private void RealizeExpandedSubtree(ContentView? host, PrayerCardViewModel vm)
    {
        if (host is null || host.Content is not null) return;
        _expandedSubtreeTemplate ??= Resources[ExpandedSubtreeTemplateKey] as DataTemplate;
        if (_expandedSubtreeTemplate is null) return;
        var content = (Microsoft.Maui.Controls.View)_expandedSubtreeTemplate.CreateContent();
        content.BindingContext = vm;
        host.Content = content;
        PerfLog.Log($"ExpandedSubtree.realized id={vm.Id}");
    }

    private static Thickness CardMarginFor(bool expanded)
        => expanded ? new Thickness(0, 8) : new Thickness(14, 0, 0, 0);

    private static void AnimateCardMargin(Border border, Thickness target)
    {
        var id = (border.BindingContext as PrayerCardViewModel)?.Id ?? -1;
        PerfLog.Log($"AnimateCardMargin.entry id={id}");
        // Android respects system animation settings automatically; no reduced-motion guard needed.
        var from = border.Margin;
        var tween = new Animation(v => border.Margin = new Thickness(
            from.Left   + (target.Left   - from.Left)   * v,
            from.Top    + (target.Top    - from.Top)    * v,
            from.Right  + (target.Right  - from.Right)  * v,
            from.Bottom + (target.Bottom - from.Bottom) * v), 0, 1);
        tween.Commit(border, "CardMarginTween", rate: 16, length: 200, easing: Easing.CubicInOut);
        // Commit returns immediately — measures scheduling cost only, not the 200ms runtime.
        PerfLog.Log($"AnimateCardMargin.exit id={id} (Commit returned)");
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

        // Shape = null disables CT.Maui v14's default white-filled RoundRectangle
        // frame — our inner Border owns the rounded themed surface instead.
        var options = new PopupOptions { Shape = null };
        await this.ShowPopupAsync(new CardsOverflowPopup(vm), options, CancellationToken.None);
    }

    private void OnSearchButtonPressed(object? sender, EventArgs e)
        => searchBar.Unfocus();

    private void OnBackgroundTapped(object? sender, TappedEventArgs e)
    {
        if (searchBar.IsFocused) searchBar.Unfocus();
    }

    protected override async void OnAppearing()
    {
        PerfLog.Log("PrayerCardsPage.OnAppearing.entry");
        base.OnAppearing();
        if (BindingContext is not PrayerCardsViewModel vm) return;

        // Slice 6g — assert the cross-page busy flag immediately so the overlay
        // is up the moment the Cards page becomes the foreground page, masking
        // the SyncAsync→ScrollTo gap and the new-expanded-card lazy-realization
        // pop-in. Cleared in finally after ScrollTo (or immediately if no save
        // was pending). The 2.5s highlight fade runs in the background and does
        // NOT block the overlay-off transition.
        var hadPendingSave = !string.IsNullOrEmpty(vm.PendingSavedIdentifier);
        if (hadPendingSave) vm.IsAwaitingSavedCard = true;

        try
        {
            PerfLog.Log("OnAppearing.before PageSync");
            await PageSync.OnAppearingAsync(vm);
            PerfLog.Log("OnAppearing.after PageSync");

            PerfLog.Log("OnAppearing.before ConsumePendingSavedAsync");
            var savedCard = await vm.ConsumePendingSavedAsync();
            PerfLog.Log($"OnAppearing.after ConsumePendingSavedAsync (savedCard null? {savedCard == null})");
            if (savedCard != null)
            {
                await ScrollToSavedCardAsync(vm, savedCard);
                FadeHighlightAfterDelayAsync(savedCard).SafeFireAndForget();
            }
        }
        finally
        {
            if (hadPendingSave) vm.IsAwaitingSavedCard = false;
            PerfLog.Log("OnAppearing.exit");
        }
    }
}

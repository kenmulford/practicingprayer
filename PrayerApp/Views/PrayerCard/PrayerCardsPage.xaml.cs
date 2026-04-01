using PrayerApp.ViewModels;

namespace PrayerApp.Views.PrayerCard;

public partial class PrayerCardsPage : ContentPage
{
    private bool _loaded;

    public PrayerCardsPage(PrayerCardsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        vm.HighlightCardRequested += OnHighlightCardRequested;
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
        }
    }

    private void OnCardBorderLoaded(object? sender, EventArgs e)
    {
        // Wire LongPressCommand in code-behind to avoid XC0045 warning.
        // The command lives on PrayerCardsViewModel (the page VM), but the DataTemplate's
        // x:DataType is PrayerCardViewModel — XAML compiled bindings can't resolve across scopes.
        if (sender is not Border border || BindingContext is not PrayerCardsViewModel vm) return;
        var behavior = border.Behaviors.OfType<CommunityToolkit.Maui.Behaviors.TouchBehavior>().FirstOrDefault();
        if (behavior is null) return;
        behavior.LongPressCommand = vm.LongPressCardCommand;
        // Bind parameter to Border's BindingContext so it tracks recycled items correctly
        behavior.SetBinding(CommunityToolkit.Maui.Behaviors.TouchBehavior.LongPressCommandParameterProperty,
            new Binding("BindingContext", source: border));
    }

    private void OnCardHeaderTapped(object? sender, TappedEventArgs e)
    {
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

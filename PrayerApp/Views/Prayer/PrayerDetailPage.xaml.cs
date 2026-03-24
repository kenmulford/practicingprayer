using PrayerApp.Models;
using PrayerApp.ViewModels;

namespace PrayerApp.Views.Prayer;

public partial class PrayerDetailPage : ContentPage
{
    private bool _initialLoadComplete;

    public PrayerDetailPage()
    {
        InitializeComponent();
    }

    private void OnTagEntryCompleted(object? sender, EventArgs e)
    {
        // Dismiss keyboard after tag submission so Save button is accessible
        tagEntry.Unfocus();
    }

    private void OnSuggestionSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count == 0) return;

        if (sender is CollectionView cv && e.CurrentSelection.FirstOrDefault() is PrayerTag tag)
        {
            var tagId = tag.Id;
            cv.SelectedItem = null; // Reset selection first
            // Defer to next UI tick — AddSuggestedTagAsync clears SuggestedTags,
            // and iOS UICollectionView silently fails if its data source is mutated
            // during a SelectionChanged delegate callback.
            Dispatcher.DispatchAsync(() =>
            {
                if (BindingContext is PrayerRequestDetailViewModel vm)
                    vm.AddSuggestedTagCommand.Execute(tagId);
            });
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is PrayerRequestDetailViewModel vm)
        {
            if (_initialLoadComplete)
            {
                // Returning from a child page (e.g. tag edit) — reload to pick up changes
                vm.Reload();
            }
            else
            {
                _initialLoadComplete = true;
            }

            if (vm.IsEditable && string.IsNullOrEmpty(vm.Title))
            {
                Dispatcher.DispatchAsync(() => TitleEntry.Focus());
            }
        }
    }
}

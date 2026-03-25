using PrayerApp.Models;
using PrayerApp.ViewModels;

namespace PrayerApp.Views.Prayer;

public partial class PrayerDetailPage : ContentPage
{
    private bool _initialLoadComplete;
    private readonly ToolbarItem _editToolbarItem;
    private readonly ToolbarItem _saveToolbarItem;

    public PrayerDetailPage()
    {
        InitializeComponent();

        _editToolbarItem = new ToolbarItem { Text = "Edit" };
        _editToolbarItem.SetBinding(ToolbarItem.CommandProperty, nameof(PrayerRequestDetailViewModel.EditPrayerCommand));

        _saveToolbarItem = new ToolbarItem { Text = "Save" };
        _saveToolbarItem.SetBinding(ToolbarItem.CommandProperty, nameof(PrayerRequestDetailViewModel.SaveCommand));
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
                vm.PropertyChanged += OnViewModelPropertyChanged;
            }

            UpdateToolbarItems(vm);

            if (vm.IsEditable && string.IsNullOrEmpty(vm.Title))
            {
                Dispatcher.DispatchAsync(() => TitleEntry.Focus());
            }
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PrayerRequestDetailViewModel.IsReadOnly) &&
            sender is PrayerRequestDetailViewModel vm)
        {
            UpdateToolbarItems(vm);
        }
    }

    private void UpdateToolbarItems(PrayerRequestDetailViewModel vm)
    {
        ToolbarItems.Clear();
        ToolbarItems.Add(vm.IsReadOnly ? _editToolbarItem : _saveToolbarItem);
    }
}

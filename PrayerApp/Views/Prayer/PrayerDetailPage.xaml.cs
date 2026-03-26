using PrayerApp.Models;
using PrayerApp.ViewModels;

namespace PrayerApp.Views.Prayer;

public partial class PrayerDetailPage : ContentPage
{
    private bool _initialLoadComplete;
    private readonly ToolbarItem _editToolbarItem;
    private readonly ToolbarItem _saveToolbarItem;
    private readonly ToolbarItem _saveAndNewToolbarItem;

    public PrayerDetailPage(PrayerRequestDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        _editToolbarItem = new ToolbarItem { Text = "Edit" };
        _editToolbarItem.SetBinding(ToolbarItem.CommandProperty, nameof(PrayerRequestDetailViewModel.EditPrayerCommand));

        _saveToolbarItem = new ToolbarItem { Text = "Save" };
        _saveToolbarItem.SetBinding(ToolbarItem.CommandProperty, nameof(PrayerRequestDetailViewModel.SaveCommand));

        _saveAndNewToolbarItem = new ToolbarItem { Text = "Save +", Order = ToolbarItemOrder.Primary };
        _saveAndNewToolbarItem.SetBinding(ToolbarItem.CommandProperty, nameof(PrayerRequestDetailViewModel.SaveAndNewCommand));

        vm.FormResetRequested += (_, _) =>
            Dispatcher.DispatchAsync(() => TitleEntry.Focus());
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
        if (sender is PrayerRequestDetailViewModel vm &&
            (e.PropertyName == nameof(PrayerRequestDetailViewModel.IsReadOnly) ||
             e.PropertyName == nameof(PrayerRequestDetailViewModel.ShowSaveAndNew)))
        {
            UpdateToolbarItems(vm);
        }
    }

    private void UpdateToolbarItems(PrayerRequestDetailViewModel vm)
    {
        ToolbarItems.Clear();
        if (vm.IsReadOnly)
        {
            ToolbarItems.Add(_editToolbarItem);
        }
        else
        {
            if (vm.ShowSaveAndNew)
                ToolbarItems.Add(_saveAndNewToolbarItem);
            ToolbarItems.Add(_saveToolbarItem);
        }
    }

    private void OnBackgroundTapped(object? sender, TappedEventArgs e)
    {
        if (TitleEntry.IsFocused) TitleEntry.Unfocus();
        else if (DetailsEditor.IsFocused) DetailsEditor.Unfocus();
        else if (tagEntry.IsFocused) tagEntry.Unfocus();
    }
}

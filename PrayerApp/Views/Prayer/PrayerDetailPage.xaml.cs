using PrayerApp.Helpers;
using PrayerApp.ViewModels;
using PrayerApp.Views.Tags;

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

        _editToolbarItem = new ToolbarItem { Text = "Edit", AutomationId = "Edit" };
        _editToolbarItem.SetBinding(ToolbarItem.CommandProperty, nameof(PrayerRequestDetailViewModel.EditPrayerCommand));
        SemanticProperties.SetHint(_editToolbarItem, "Switch to edit mode");

        _saveToolbarItem = new ToolbarItem { Text = "Save", AutomationId = "Save" };
        _saveToolbarItem.SetBinding(ToolbarItem.CommandProperty, nameof(PrayerRequestDetailViewModel.SaveCommand));
        SemanticProperties.SetHint(_saveToolbarItem, "Save prayer request changes");

        _saveAndNewToolbarItem = new ToolbarItem { Text = "Save +", AutomationId = "Save +" };
        _saveAndNewToolbarItem.SetBinding(ToolbarItem.CommandProperty, nameof(PrayerRequestDetailViewModel.SaveAndNewCommand));
        SemanticProperties.SetDescription(_saveAndNewToolbarItem, "Save and add another prayer");
        SemanticProperties.SetHint(_saveAndNewToolbarItem, "Save this prayer and create a new one");
#if IOS || MACCATALYST
        // Secondary on iOS keeps Save as the sole Primary; UIKit auto-overflow would
        // drop a11y labels on the overflowed buttons.
        _saveAndNewToolbarItem.Order = ToolbarItemOrder.Secondary;
#endif

        vm.TagPickerRequested += async pickerVm =>
            await Shell.Current.Navigation.PushModalAsync(
                new TagPickerPage(pickerVm));

        vm.SelectedTags.CollectionChanged += (_, _) => RebuildEditTagChips(vm);
    }

    /// <summary>Rebuild tag chips in the FlexLayout, keeping the + button last.</summary>
    private void RebuildEditTagChips(PrayerRequestDetailViewModel vm)
    {
        var chipTextColor = (Color)Application.Current!.Resources["White"];

        editTagChips.Children.Clear();
        editTagChips.Children.Add(tagsLabel);

        foreach (var chip in vm.SelectedTags)
        {
            var label = new Label
            {
                Text = chip.Name,
                TextColor = chipTextColor,
                FontSize = Application.Current?.Resources.TryGetValue("FontCaption", out var captionSize) == true && captionSize is double captionFont
                    ? captionFont
                    : 12.0,
                VerticalOptions = LayoutOptions.Center
            };
            var removeBtn = new Button
            {
                Text = "\u00D7",
                Command = chip.RemoveCommand,
                TextColor = chipTextColor,
                BackgroundColor = Colors.Transparent,
                FontSize = Application.Current?.Resources.TryGetValue("FontBody", out var bodySize) == true && bodySize is double bodyFont
                    ? bodyFont
                    : 14.0,
                Padding = new Thickness(2, 0),
                BorderWidth = 0,
                MinimumWidthRequest = 24,
                MinimumHeightRequest = 24,
                VerticalOptions = LayoutOptions.Center
            };
            SemanticProperties.SetDescription(removeBtn, "Remove tag");
            var stack = new HorizontalStackLayout { Spacing = 4 };
            stack.Children.Add(label);
            stack.Children.Add(removeBtn);
            var border = new Border
            {
                BackgroundColor = chip.ChipColor,
                Stroke = Colors.Transparent,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(6, 4),
                Margin = new Thickness(2),
                Content = stack
            };
            editTagChips.Children.Add(border);
        }

        editTagChips.Children.Add(addTagButton);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
#if IOS
        // Disable swipe-back gesture on edit pages. Shell.Navigating does not fire
        // for iOS swipe-back (dotnet/maui#15813), so the unsaved changes guard is
        // bypassed. Disabling the gesture forces users to use the back button,
        // which fires Shell.Navigating → IEditGuard check → confirmation dialog.
        Platforms.iOS.Helpers.SwipeBackHelper.DisableSwipeBack(this);
#endif

        if (BindingContext is PrayerRequestDetailViewModel vm)
        {
            if (_initialLoadComplete)
            {
                vm.Reload();
            }
            else
            {
                _initialLoadComplete = true;
                vm.PropertyChanged += OnViewModelPropertyChanged;
            }

            UpdateToolbarItems(vm);

            // BUG-75 safety net: if the platform layer silently drops the toolbar
            // items during a modal-dismiss layout race (seen once on iPhone 17 after
            // TagPickerPage dismiss), retry on the next dispatcher tick once the
            // modal animation has settled.
            _ = Dispatcher.DispatchAsync(() =>
            {
                if (vm.IsEditable && ToolbarItems.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[BUG-75] Rebuilding empty ToolbarItems in edit mode");
                    UpdateToolbarItems(vm);
                }
            });

            if (vm.IsEditable)
            {
                await FocusTitleAsync();
            }
        }
    }

    /// <summary>
    /// Focus the Title entry. Drains the Shell push layout pass first so the platform
    /// Entry view is stable when Focus() runs (firing mid-animation silently no-ops —
    /// BUG-70). Harmless when called from PendingFocusTitle after Save &amp; Add Another
    /// (no Shell push to wait on, but the gate adds no observable latency).
    /// </summary>
    private async Task FocusTitleAsync()
    {
        await Dispatcher.DrainLayoutPassAsync();
        try
        {
            TitleEntry.Focus();
        }
        catch (Exception ex)
        {
            Diagnostics.ResolveLog()?.Log("PrayerDetailPage.FocusTitleAsync", ex);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not PrayerRequestDetailViewModel vm) return;

        if (e.PropertyName == nameof(PrayerRequestDetailViewModel.IsReadOnly) ||
            e.PropertyName == nameof(PrayerRequestDetailViewModel.ShowSaveAndNew))
        {
            UpdateToolbarItems(vm);
        }
        else if (e.PropertyName == nameof(PrayerRequestDetailViewModel.PendingFocusTitle)
                 && vm.PendingFocusTitle)
        {
            _ = FocusAfterResetAsync(vm);
        }
    }

    private async Task FocusAfterResetAsync(PrayerRequestDetailViewModel vm)
    {
        try
        {
            await FocusTitleAsync();
        }
        finally
        {
            vm.ConsumePendingFocusTitle();
        }
    }

    // Diff-update instead of unconditional Clear+Add. When the desired set already
    // matches, no platform mutation happens — which avoids a Shell chrome race on
    // iOS after modal dismiss where the re-add could silently no-op (BUG-75).
    private void UpdateToolbarItems(PrayerRequestDetailViewModel vm)
    {
        var desired = new List<ToolbarItem>(2);
        if (vm.IsReadOnly)
        {
            desired.Add(_editToolbarItem);
        }
        else
        {
            if (vm.ShowSaveAndNew)
                desired.Add(_saveAndNewToolbarItem);
            desired.Add(_saveToolbarItem);
        }

        if (ToolbarItems.SequenceEqual(desired)) return;

        ToolbarItems.Clear();
        foreach (var item in desired)
            ToolbarItems.Add(item);
    }

    protected override void OnDisappearing()
    {
#if IOS
        Platforms.iOS.Helpers.SwipeBackHelper.EnableSwipeBack(this);
#endif
        base.OnDisappearing();
    }

    private void OnBackgroundTapped(object? sender, TappedEventArgs e)
    {
        if (TitleEntry.IsFocused) TitleEntry.Unfocus();
        else if (DetailsEditor.IsFocused) DetailsEditor.Unfocus();
    }
}

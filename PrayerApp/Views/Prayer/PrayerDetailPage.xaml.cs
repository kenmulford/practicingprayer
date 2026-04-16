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

        _editToolbarItem = new ToolbarItem { Text = "Edit" };
        _editToolbarItem.SetBinding(ToolbarItem.CommandProperty, nameof(PrayerRequestDetailViewModel.EditPrayerCommand));
        SemanticProperties.SetHint(_editToolbarItem, "Switch to edit mode");

        _saveToolbarItem = new ToolbarItem { Text = "Save" };
        _saveToolbarItem.SetBinding(ToolbarItem.CommandProperty, nameof(PrayerRequestDetailViewModel.SaveCommand));
        SemanticProperties.SetHint(_saveToolbarItem, "Save prayer request changes");

        _saveAndNewToolbarItem = new ToolbarItem { Text = "Save +" };
        _saveAndNewToolbarItem.SetBinding(ToolbarItem.CommandProperty, nameof(PrayerRequestDetailViewModel.SaveAndNewCommand));
        SemanticProperties.SetDescription(_saveAndNewToolbarItem, "Save and add another prayer");
        SemanticProperties.SetHint(_saveAndNewToolbarItem, "Save this prayer and create a new one");

        vm.FormResetRequested += (_, _) =>
            Dispatcher.DispatchAsync(() => TitleEntry.Focus());

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
                FontSize = 12,
                VerticalOptions = LayoutOptions.Center
            };
            var removeBtn = new Button
            {
                Text = "\u00D7",
                Command = chip.RemoveCommand,
                TextColor = chipTextColor,
                BackgroundColor = Colors.Transparent,
                FontSize = 14,
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

    protected override void OnAppearing()
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

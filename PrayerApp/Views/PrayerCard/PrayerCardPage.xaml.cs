using PrayerApp.Helpers;
using PrayerApp.ViewModels;

namespace PrayerApp.Views.PrayerCard;

public partial class PrayerCardPage : ContentPage
{
    public PrayerCardPage(PrayerCardViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
#if IOS
        Platforms.iOS.Helpers.SwipeBackHelper.DisableSwipeBack(this);
#endif
        if (BindingContext is PrayerCardViewModel vm)
        {
            // Load boxes for the picker (new cards won't have ApplyQueryAttributes trigger this)
            if (vm.AvailableBoxes.Count == 0)
                await vm.LoadBoxPickerAsync();
            if (vm.IsNew)
            {
                // Drain the Shell push layout pass before Focus(); a single dispatcher
                // tick resolves before the platform Entry view is stable and Focus()
                // silently no-ops mid-animation (BUG-70).
                await Dispatcher.DrainLayoutPassAsync();
                try { TitleEntry.Focus(); }
                catch (Exception ex)
                {
                    Diagnostics.ResolveLog()?.Log("PrayerCardPage.OnAppearing focus", ex);
                }
            }
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
        // Dismiss keyboard when tapping outside input fields (iOS)
        if (TitleEntry.IsFocused)
            TitleEntry.Unfocus();
    }
}

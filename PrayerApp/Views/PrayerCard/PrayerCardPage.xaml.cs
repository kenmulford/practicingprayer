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
                // Delay past the Shell push animation (~220ms) so the platform Entry view
                // is stable when Focus() is called. Dispatcher.DispatchAsync alone resolves
                // immediately on the UI thread and fires Focus() mid-animation (BUG-70).
                await Task.Delay(300);
                TitleEntry.Focus();
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

using PrayerApp.ViewModels;

namespace PrayerApp.Views.PrayerCard;

public partial class PrayerCardPage : ContentPage
{
    public PrayerCardPage(PrayerCardViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
#if IOS
        Platforms.iOS.Helpers.SwipeBackHelper.DisableSwipeBack(this);
#endif
        if (BindingContext is PrayerCardViewModel vm && vm.IsNew)
            Dispatcher.DispatchAsync(() => TitleEntry.Focus());
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

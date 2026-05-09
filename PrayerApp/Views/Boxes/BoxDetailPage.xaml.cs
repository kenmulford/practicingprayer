using PrayerApp.ViewModels;

namespace PrayerApp.Views.Boxes;

public partial class BoxDetailPage : ContentPage
{
    public BoxDetailPage(BoxDetailViewModel vm)
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
        if (BindingContext is BoxDetailViewModel vm && !vm.IsSystem)
        {
            // Delay past the Shell push animation (~220ms) so the platform Entry view
            // is stable when Focus() is called. Dispatcher.DispatchAsync alone resolves
            // immediately on the UI thread and fires Focus() mid-animation (BUG-70).
            // 300ms also gives ApplyQueryAttributes → LoadAsync time to populate Name
            // in edit mode before focus fires.
            await Task.Delay(300);
            NameEntry.Focus();
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
        if (NameEntry.IsFocused)
            NameEntry.Unfocus();
    }
}

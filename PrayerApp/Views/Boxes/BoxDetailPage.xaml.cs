using PrayerApp.ViewModels;

namespace PrayerApp.Views.Boxes;

public partial class BoxDetailPage : ContentPage
{
    public BoxDetailPage(BoxDetailViewModel vm)
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
        if (BindingContext is BoxDetailViewModel vm && !vm.IsExisting)
            Dispatcher.DispatchAsync(() => NameEntry.Focus());
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

using PrayerApp.ViewModels;

namespace PrayerApp.Views.Prayer;

public partial class PrayerDetailPage : ContentPage
{
    private bool _initialLoadComplete;

    public PrayerDetailPage()
    {
        InitializeComponent();
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

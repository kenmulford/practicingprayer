using PrayerApp.ViewModels;

namespace PrayerApp.Views.Prayer;

public partial class PrayerDetailPage : ContentPage
{
    public PrayerDetailPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is PrayerRequestDetailViewModel vm
            && vm.IsEditable
            && string.IsNullOrEmpty(vm.Title))
        {
            Dispatcher.DispatchAsync(() => TitleEntry.Focus());
        }
    }
}

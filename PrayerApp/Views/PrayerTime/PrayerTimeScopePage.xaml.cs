using PrayerApp.ViewModels;

namespace PrayerApp.Views.PrayerTime;

public partial class PrayerTimeScopePage : ContentPage, IPageSheetModal
{
    public PrayerTimeScopePage(PrayerTimeScopeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

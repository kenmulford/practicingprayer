using PrayerApp.ViewModels;

namespace PrayerApp.Views.PrayerTime;

public partial class PrayerTimeScopePage : ContentPage
{
    public PrayerTimeScopePage(PrayerTimeScopeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

using PrayerApp.ViewModels;

namespace PrayerApp.Views.PrayerTime;

public partial class PrayerTimeBoxScopePage : ContentPage, IPageSheetModal
{
    public PrayerTimeBoxScopePage(PrayerTimeBoxScopeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

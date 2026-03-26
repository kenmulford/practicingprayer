using PrayerApp.ViewModels;

namespace PrayerApp.Views.Settings;

public partial class HelpPage : ContentPage
{
    public HelpPage(HelpViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

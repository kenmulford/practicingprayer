using PrayerApp.ViewModels;

namespace PrayerApp.Views;

public partial class QuickAddPage : ContentPage, IPageSheetModal
{
    public QuickAddPage(QuickAddViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

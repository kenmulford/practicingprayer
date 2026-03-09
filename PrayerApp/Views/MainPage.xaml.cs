namespace PrayerApp.Views;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BtnQuickAdd.Clicked += async (s, e) =>
            await Shell.Current.PushModalAsync(new QuickAddPage());
    }
}

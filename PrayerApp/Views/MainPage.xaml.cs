namespace PrayerApp.Views;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BtnQuickAdd.Clicked += async (s, e) =>
            await Shell.Current.Navigation.PushModalAsync(new QuickAddPage());
    }
}

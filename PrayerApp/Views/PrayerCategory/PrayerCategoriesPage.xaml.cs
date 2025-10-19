using PrayerApp.ViewModels;

namespace PrayerApp.Views.PrayerCategory;

public partial class PrayerCategoriesPage : ContentPage
{
    public PrayerCategoriesPage()
    {
        InitializeComponent();
    }

    private void ContentPage_NavigatedTo(object sender, NavigatedToEventArgs e)
    {
        categoryCollection.SelectedItem = null;
    }
}

namespace PrayerApp.Views.Prayer;

public partial class PrayerListPage : ContentPage
{
	public PrayerListPage()
	{
		InitializeComponent();
	}

	private void ContentPage_NavigatedTo(object sender, NavigatedToEventArgs e)
	{
		prayerCollection.SelectedItem = null;
	}
}
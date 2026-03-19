using PrayerApp.ViewModels;

namespace PrayerApp.Views.Prayer;

public partial class PrayerListPage : ContentPage
{
	private bool _loaded;

	public PrayerListPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (!_loaded && BindingContext is PrayerListViewModel vm)
		{
			_loaded = true;
			await vm.LoadAsync();
		}
	}

	private void ContentPage_NavigatedTo(object sender, NavigatedToEventArgs e)
	{
		prayerCollection.SelectedItem = null;
	}
}

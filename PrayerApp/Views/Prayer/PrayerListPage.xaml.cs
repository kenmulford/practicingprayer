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
		await App.InitTask; // ensure DB seeding is complete before loading data
		if (BindingContext is PrayerListViewModel vm)
		{
			if (!_loaded)
			{
				_loaded = true;
				await vm.LoadAsync();
			}
			else
			{
				// Subsequent visits — refresh data that may have changed on other tabs
				// (e.g. tags edited via Cards tab, prayers added via QuickAdd)
				await vm.RefreshAsync();
			}
		}
	}
}

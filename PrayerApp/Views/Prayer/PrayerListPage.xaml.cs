using PrayerApp.Helpers;
using PrayerApp.ViewModels;

namespace PrayerApp.Views.Prayer;

public partial class PrayerListPage : ContentPage
{
	public PrayerListPage(PrayerListViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (BindingContext is PrayerListViewModel vm)
			await PageSync.OnAppearingAsync(vm);
	}

	private void OnSearchButtonPressed(object? sender, EventArgs e)
	{
		searchBar.Unfocus();
	}

	private void OnBackgroundTapped(object? sender, TappedEventArgs e)
	{
		if (searchBar.IsFocused)
			searchBar.Unfocus();
	}
}

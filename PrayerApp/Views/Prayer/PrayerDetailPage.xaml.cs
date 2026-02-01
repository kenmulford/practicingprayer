using PrayerApp.ViewModels;
using System;
using System.Linq; // Add this using directive for ToList()
using Microsoft.Extensions.DependencyInjection;

using PrayerApp.Models;

namespace PrayerApp.Views.Prayer;

public partial class PrayerDetailPage : ContentPage
{
	public PrayerDetailPage()
	{
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is PrayerDetailViewModel vm)
        {
            try
            {
                await vm.LoadCategoriesAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Failed to load categories: {ex.Message}", "OK");
            }
        }
    }
}
using PrayerApp.ViewModels;
using System;
using System.Linq;
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
        // Tag selection only applies to card editing, not request editing
        // PrayerRequestDetailViewModel handles requests without tag selection UI
    }
}
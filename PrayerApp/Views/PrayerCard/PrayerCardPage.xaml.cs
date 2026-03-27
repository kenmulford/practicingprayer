using PrayerApp.Helpers;
using PrayerApp.ViewModels;

namespace PrayerApp.Views.PrayerCard;

public partial class PrayerCardPage : ContentPage
{
    public PrayerCardPage(PrayerCardViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        EditGuardHelper.AttachEditGuardBackButton(this);
    }

    private void OnBackgroundTapped(object? sender, TappedEventArgs e)
    {
        // Dismiss keyboard when tapping outside input fields (iOS)
        if (TitleEntry.IsFocused)
            TitleEntry.Unfocus();
    }
}

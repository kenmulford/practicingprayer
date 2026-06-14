using PrayerApp.ViewModels;

namespace PrayerApp.Views.PrayerTime;

public partial class PrayerTimeCardSelectPage : ContentPage, IPageSheetModal
{
    public PrayerTimeCardSelectPage(PrayerTimeCardSelectViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    // Tapping anywhere on a card row toggles its checkbox — larger hit target
    // than the CheckBox alone, consistent with the rest of the app's row taps.
    private void OnCardRowTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Element { BindingContext: SelectableCard card })
            card.IsSelected = !card.IsSelected;
    }
}

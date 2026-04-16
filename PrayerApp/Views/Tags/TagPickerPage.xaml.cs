using PrayerApp.Models;
using PrayerApp.ViewModels;

namespace PrayerApp.Views.Tags;

public partial class TagPickerPage : ContentPage, IPageSheetModal
{
    public TagPickerPage(TagPickerViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private void OnSuggestionTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Grid { BindingContext: PrayerTag tag } &&
            BindingContext is TagPickerViewModel vm)
        {
            vm.AddSuggestedTagCommand.Execute(tag.Id);
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Dispatcher.DispatchAsync(() => SearchEntry.Focus());
    }

    protected override void OnDisappearing()
    {
        // Safety net: if dismissed via back gesture instead of Done button,
        // ensure WaitForDismissAsync resolves so the caller doesn't hang.
        if (BindingContext is TagPickerViewModel vm)
            vm.SignalDismiss();
        base.OnDisappearing();
    }
}

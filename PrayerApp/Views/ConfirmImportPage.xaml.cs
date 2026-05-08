using PrayerApp.ViewModels;

namespace PrayerApp.Views;

public partial class ConfirmImportPage : ContentPage, IPageSheetModal
{
    public ConfirmImportPage(ConfirmImportViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is not ConfirmImportViewModel vm) return;

        // Sync work first so card title + prayer rows paint on the first
        // frame; the Collection picker populates a tick later. The other
        // order (await boxes, then consume) introduced a perceptible
        // cold-cache delay before primary content rendered. Both calls
        // are idempotent — modal OnAppearing fires on initial show AND
        // on resume from background; ConsumePending guards via _consumed
        // and LoadBoxesAsync via _boxesLoaded so the user's mid-flow
        // Collection pick survives backgrounding.
        vm.ConsumePending();
        await vm.LoadBoxesAsync();
    }

    private void OnRemovePrayerClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: EditablePrayer row } &&
            BindingContext is ConfirmImportViewModel vm)
        {
            vm.RemovePrayerCommand.Execute(row);
        }
    }

    private void OnCardItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border { BindingContext: CardPickerItem item } &&
            BindingContext is ConfirmImportViewModel vm)
            vm.SelectCardCommand.Execute(item);
    }
}

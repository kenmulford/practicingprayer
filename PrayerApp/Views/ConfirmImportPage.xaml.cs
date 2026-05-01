using PrayerApp.ViewModels;

namespace PrayerApp.Views;

public partial class ConfirmImportPage : ContentPage, IPageSheetModal
{
    public ConfirmImportPage(ConfirmImportViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ConfirmImportViewModel vm)
            vm.ConsumePending();
    }

    private void OnRemovePrayerClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: EditablePrayer row } &&
            BindingContext is ConfirmImportViewModel vm)
        {
            vm.RemovePrayerCommand.Execute(row);
        }
    }
}

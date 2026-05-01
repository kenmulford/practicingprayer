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
        if (BindingContext is ConfirmImportViewModel vm)
            vm.ConsumePending();

        // Focus + select-all the Card Title so the user can type to overwrite
        // the suggested "Imported {date}" immediately. 300 ms delay matches
        // BoxDetailPage's pattern — DispatchAsync alone fires Focus() before
        // the platform Entry view is stable on iOS.
        await Task.Delay(300);
        if (CardTitleEntry.Focus())
        {
            CardTitleEntry.CursorPosition = 0;
            CardTitleEntry.SelectionLength = CardTitleEntry.Text?.Length ?? 0;
        }
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

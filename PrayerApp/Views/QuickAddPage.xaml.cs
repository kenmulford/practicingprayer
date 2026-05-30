using PrayerApp.ViewModels;

namespace PrayerApp.Views;

public partial class QuickAddPage : ContentPage, IPageSheetModal
{
    public QuickAddPage(QuickAddViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is QuickAddViewModel vm)
            await vm.LoadDestinationAsync();

        Dispatcher.DispatchAsync(() => TitleEntry.Focus());
    }

    private void OnCardItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border { BindingContext: CardPickerItem item } &&
            BindingContext is QuickAddViewModel vm)
            vm.SelectCardCommand.Execute(item);
    }
}

using System.ComponentModel;
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
        {
            await vm.InitializeAsync();
            vm.PropertyChanged -= OnVmPropertyChanged;
            vm.PropertyChanged += OnVmPropertyChanged;
        }

        Dispatcher.DispatchAsync(() => TitleEntry.Focus());
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is QuickAddViewModel vm)
            vm.PropertyChanged -= OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (BindingContext is not QuickAddViewModel vm) return;

        if (e.PropertyName == nameof(QuickAddViewModel.IsExistingCardMode))
        {
            if (vm.IsExistingCardMode)
            {
                selectedCardSummary.IsVisible = vm.ShowSelectedCardSummary;
                cardGroupsList.IsVisible = vm.ShowCardPickerList;
            }
            else
            {
                cardGroupsList.IsVisible = false;
                selectedCardSummary.IsVisible = false;
            }
        }
    }

    private void OnCardItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border { BindingContext: CardPickerItem item } &&
            BindingContext is QuickAddViewModel vm)
            vm.SelectCardCommand.Execute(item);
    }

    private void OnChangeCardTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is QuickAddViewModel vm)
            vm.ShowCardPickerCommand.Execute(null);
    }
}

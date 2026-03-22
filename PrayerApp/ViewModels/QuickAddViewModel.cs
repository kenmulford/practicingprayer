using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PrayerApp.ViewModels;

public class QuickAddViewModel : ObservableObject
{
    private readonly ICardService _cardService;
    private readonly IPrayerService _prayerService;

    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    private PrayerCard? _selectedCard;
    public PrayerCard? SelectedCard
    {
        get => _selectedCard;
        set => SetProperty(ref _selectedCard, value);
    }

    public ObservableCollection<PrayerCard> Cards { get; } = new();

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public QuickAddViewModel()
    {
        _cardService = IPlatformApplication.Current!.Services.GetRequiredService<ICardService>();
        _prayerService = IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>();
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CancelCommand = new AsyncRelayCommand(CancelAsync);
        LoadCardsAsync().SafeFireAndForget();
    }

    private async Task LoadCardsAsync()
    {
        try
        {
            var cards = await _cardService.GetCardsAsync();
            Cards.Clear();
            foreach (var card in cards)
                Cards.Add(card);
            if (Cards.Count > 0)
                SelectedCard = Cards[0];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load cards: {ex.Message}");
            await Shell.Current.DisplayAlertAsync("Error", "Unable to load prayer cards.", "OK");
        }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            await Shell.Current.DisplayAlertAsync("Required", "Please enter a prayer title.", "OK");
            return;
        }
        if (SelectedCard == null)
        {
            await Shell.Current.DisplayAlertAsync("Required", "Please select a card.", "OK");
            return;
        }

        var prayer = new Prayer
        {
            Title = Title.Trim(),
            PrayerCardId = SelectedCard.Id
        };
        await _prayerService.SavePrayerAsync(prayer);
        _prayerService.InvalidateCache();
        await Shell.Current.Navigation.PopModalAsync();
    }

    private async Task CancelAsync()
    {
        await Shell.Current.Navigation.PopModalAsync();
    }
}

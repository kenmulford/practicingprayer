using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        _ = LoadCardsAsync();
    }

    private async Task LoadCardsAsync()
    {
        var cards = await _cardService.GetCardsAsync();
        Cards.Clear();
        foreach (var card in cards)
            Cards.Add(card);
        if (Cards.Count > 0)
            SelectedCard = Cards[0];
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Title) || SelectedCard == null)
            return;

        var prayer = new Prayer
        {
            Title = Title.Trim(),
            PrayerCardId = SelectedCard.Id
        };
        await _prayerService.SavePrayerAsync(prayer);
        await Shell.Current.Navigation.PopModalAsync();
    }

    private async Task CancelAsync()
    {
        await Shell.Current.Navigation.PopModalAsync();
    }
}

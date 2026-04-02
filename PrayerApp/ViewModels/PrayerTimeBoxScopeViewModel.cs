using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PrayerApp.ViewModels;

public class SelectableBox
{
    public CardBox Box { get; }

    public SelectableBox(CardBox box) => Box = box;
}

public class PrayerTimeBoxScopeViewModel : ObservableObject
{
    private readonly IBoxService _boxService;
    private readonly ICardService _cardService;
    private readonly IPrayerService _prayerService;
    private readonly INavigationService _navigationService;

    public ObservableCollection<SelectableBox> Boxes { get; } = new();
    public ICommand SelectCommand { get; }
    public ICommand CancelCommand { get; }

    public PrayerTimeBoxScopeViewModel(IBoxService boxService, ICardService cardService,
        IPrayerService prayerService, INavigationService navigationService)
    {
        _boxService = boxService;
        _cardService = cardService;
        _prayerService = prayerService;
        _navigationService = navigationService;
        SelectCommand = new AsyncRelayCommand<SelectableBox>(SelectBoxAsync);
        CancelCommand = new AsyncRelayCommand(CancelAsync);
        LoadBoxesAsync().SafeFireAndForget();
    }

    public PrayerTimeBoxScopeViewModel() : this(
        IPlatformApplication.Current!.Services.GetRequiredService<IBoxService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<ICardService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>())
    { }

    public async Task LoadBoxesAsync()
    {
        try
        {
            var boxesTask = _boxService.GetBoxesAsync();
            var cardsTask = _cardService.GetCardsAsync();
            var prayersTask = _prayerService.GetAllActivePrayersAsync();
            await Task.WhenAll(boxesTask, cardsTask, prayersTask);

            var allBoxes = boxesTask.Result;
            var cards = cardsTask.Result;
            var activePrayers = prayersTask.Result;

            // Build a set of card IDs that have at least one active prayer
            var cardIdsWithPrayers = activePrayers.Select(p => p.PrayerCardId).ToHashSet();

            // Only show user boxes that contain at least one card with active prayers
            var userBoxes = allBoxes
                .Where(b => !b.IsSystem)
                .Where(b => cards.Any(c => c.BoxId == b.Id && cardIdsWithPrayers.Contains(c.Id)));

            Boxes.Clear();
            foreach (var box in userBoxes)
                Boxes.Add(new SelectableBox(box));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load boxes: {ex.Message}");
            await _navigationService.DisplayAlertAsync("Error", "Unable to load collections.", "OK");
        }
    }

    public async Task SelectBoxAsync(SelectableBox? selectableBox)
    {
        if (selectableBox is null) return;

        await _navigationService.PopModalAsync();
        await _navigationService.GoToAsync($"{Routes.PrayerTimePage}?scope={Routes.ScopeBox}&boxId={selectableBox.Box.Id}");
    }

    public async Task CancelAsync()
    {
        await _navigationService.PopModalAsync();
    }
}
